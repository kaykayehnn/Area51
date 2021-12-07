using Area51.ElevatorCalls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Area51
{
    class Elevator
    {
        private const string GROUND_FLOOR = "G";
        private const string SECRET_NUCLEAR_FLOOR = "S";
        private const string SECRET_EXPERIMENTAL_FLOOR = "T1";
        private const string SECRET_ALIEN_FLOOR = "T2";

        private const int MS_PER_FLOOR = 1000;
        private int currentFloorIndex;
        private ElevatorState state;
        private object @lock;

        // We use a linked list instead of a queue here, because we need to
        // traverse the queue and remove elements from its middle if there are
        // multiple agents calling for the same floor.
        private LinkedList<IElevatorCall> elevatorCallsQueue;
        // TODO: UPDATE THIS COMMENT This dictionary holds TaskCompletionSources, which we use to notify agents
        // when the elevator has arrived at their floor and they can get in.

        // We keep the entire calls until we have fully processed them.
        private List<AgentElevatorCall> agentsBeingProcessed;

        public Elevator()
        {
            this.Floors = new[]
            {
                GROUND_FLOOR,
                SECRET_NUCLEAR_FLOOR,
                SECRET_EXPERIMENTAL_FLOOR,
                SECRET_ALIEN_FLOOR
            };
            this.currentFloorIndex = 0;
            this.state = ElevatorState.Closed;
            this.@lock = new object();
            this.elevatorCallsQueue = new LinkedList<IElevatorCall>();
            this.agentsBeingProcessed = new List<AgentElevatorCall>();
        }

        public string[] Floors { get; }

        public async void Start()
        {
            this.state = ElevatorState.Waiting;

            while (this.state != ElevatorState.Closed)
            {
                IElevatorCall currentCall = null;
                lock (this.@lock)
                {
                    if (this.elevatorCallsQueue.Count > 0)
                    {
                        currentCall = this.elevatorCallsQueue.First.Value;
                        this.elevatorCallsQueue.RemoveFirst();
                    }
                }

                if (currentCall != null)
                {
                    if (!await this.HandleCall(currentCall))
                    {
                        // If the call failed for any reason, retry it and put it first
                        // in the queue.
                        lock (this.@lock)
                        {
                            this.elevatorCallsQueue.AddFirst(currentCall);
                        }
                    }
                }

                await Task.Delay(1);
            }
        }

        public void Stop()
        {
            this.state = ElevatorState.Closed;
        }

        public Task Call(string floor, Agent agent, Func<string> chooseFloorFunc)
        {
            var elevatorCall = new AgentElevatorCall(floor, agent, chooseFloorFunc);
            lock (this.@lock)
            {
                this.elevatorCallsQueue.AddLast(elevatorCall);
            }
            return elevatorCall.TaskCompletionSource.Task;
        }

        // This method returns true or false depending on if the call was successful or not.
        private async Task<bool> HandleCall(IElevatorCall call)
        {
            var nextFloor = call.Floor;
            // First we have to get to the requested floor.
            await this.GoTo(nextFloor);

            // Now we have to check if the agents inside the elevator have sufficient
            // security clearance.
            foreach (var caller in agentsBeingProcessed)
            {
                if (!this.HasSufficientClearance(caller.Agent, nextFloor))
                {
                    //Console.WriteLine($"An agent inside the elevator has insufficient clearance to access floor {nextFloor}.");
                    // There is an agent with insufficient clearance for this floor.
                    // We have to immediately go back to drop them off at their initial floor.

                    var getOffElevatorCompletionSource = new TaskCompletionSource();

                    var exception = new InsufficientClearanceException(getOffElevatorCompletionSource.Task);
                    caller.TaskCompletionSource.SetException(exception);

                    await this.GoTo(caller.InitialFloor);
                    ExitAgentFromElevator(caller.Agent, getOffElevatorCompletionSource);

                    return false;
                }
            }

            // If we reached this point, then all agents have sufficient security clearances.
            // We can check if any of them want to get off here.
            var i = 0;
            while (i < agentsBeingProcessed.Count)
            {
                var caller = agentsBeingProcessed[i];
                if (nextFloor == caller.DestinationFloor)
                {
                    ExitAgentFromElevator(caller.Agent, caller.TaskCompletionSource);
                }
                else
                {
                    i++;
                }
            }

            if (call is AgentElevatorCall agentCall)
            {
                Logger.WriteLine($"The elevator arrived at floor {nextFloor}");
                var pressedButtons = new HashSet<string>();
                var pressedButton = this.EnterAgentIntoElevator(agentCall);
                pressedButtons.Add(pressedButton);

                // Now we need to find if there are other callers waiting on the same floor.
                lock (this.@lock)
                {
                    var currentNode = elevatorCallsQueue.First;
                    while (currentNode != null)
                    {
                        if (currentNode.Value is AgentElevatorCall && currentNode.Value.Floor == nextFloor)
                        {
                            // We have found a matching call. Remove the caller from the queue
                            // and put them in the elevator.
                            var caller = (AgentElevatorCall)currentNode.Value;
                            pressedButton = this.EnterAgentIntoElevator(caller);
                            pressedButtons.Add(pressedButton);

                            var next = currentNode.Next;
                            // Remove the caller from the task queue, as we have already serviced them.
                            elevatorCallsQueue.Remove(currentNode);
                            currentNode = next;
                        }
                        else
                        {
                            currentNode = currentNode.Next;
                        }
                    }
                }

                // We have handled all agents on the current floor, and we now have to press their buttons.
                foreach (var button in pressedButtons)
                {
                    var newCall = new ButtonPress(button);
                    lock (this.@lock)
                    {
                        this.elevatorCallsQueue.AddFirst(newCall);
                    }
                }

                Logger.WriteLine("Elevator doors are closing...");
            }

            return true;
        }

        public async void DisplayState()
        {
            Console.CursorVisible = false;
            var DefaultWhiteSpaceOverwriter = new string(' ', 10);

            while (this.state != ElevatorState.Closed)
            {
                string nextStops;
                lock (this.@lock)
                {
                    if (this.elevatorCallsQueue.Count > 0)
                    {
                        var next5Stops = this.elevatorCallsQueue
                            .Take(5)
                            .Select(ec => ec.Floor);

                        nextStops = string.Join(", ", next5Stops);
                    }
                    else
                    {
                        nextStops = "None";
                    }
                }

                int logLinesToShow = 20;
                var allLines = Logger.GetLines();
                var lastLogLines = new StringBuilder();
                int firstLineIndex = Math.Max(0, allLines.Count - logLinesToShow);
                for (int i = firstLineIndex; i < allLines.Count; i++)
                {
                    var line = allLines[i];
                    var whiteSpaceOverwriter = new string(' ', Console.WindowWidth - line.Length - 4);
                    lastLogLines.AppendLine($"{i + 1,2}: {allLines[i]}{whiteSpaceOverwriter}");
                }

                Console.SetCursorPosition(0, 0);
                Console.WriteLine($@"
Elevator floor: {this.Floors[this.currentFloorIndex]}{DefaultWhiteSpaceOverwriter}
Elevator state: {this.state}{DefaultWhiteSpaceOverwriter}
Agents inside elevator: {this.agentsBeingProcessed.Count}{DefaultWhiteSpaceOverwriter}
Next stops: {nextStops}{DefaultWhiteSpaceOverwriter}

Log ({firstLineIndex} / {allLines.Count}):{DefaultWhiteSpaceOverwriter}
{lastLogLines}"
    .TrimStart());

                await Task.Delay(16);
            }

            Console.Clear();
        }

        private string EnterAgentIntoElevator(AgentElevatorCall call)
        {
            this.agentsBeingProcessed.Add(call);

            // Notify agent that they can get on the elevator
            // TODO: we should probably set this task when agent gets off of elevator?
            //call.TaskCompletionSource.SetResult();

            var chosenFloor = call.DestinationFloor;
            return chosenFloor;
        }

        private void ExitAgentFromElevator(Agent agent, TaskCompletionSource completionSource)
        {
            var index = this.agentsBeingProcessed.FindIndex(aec => aec.Agent == agent);
            this.agentsBeingProcessed.RemoveAt(index);
            completionSource.SetResult();
        }

        private bool HasSufficientClearance(Agent agent, string floor)
        {
            switch (agent.ConfidentialityLevel)
            {
                case ConfidentialityLevel.Confidential:
                    return floor == GROUND_FLOOR;
                case ConfidentialityLevel.Secret:
                    return floor == GROUND_FLOOR || floor == SECRET_NUCLEAR_FLOOR;
                case ConfidentialityLevel.TopSecret:
                    return true;
                default:
                    throw new InvalidOperationException(
                        $"Unexpected confidentiality level: {agent.ConfidentialityLevel}");
            }
        }

        private Task GoTo(string floor)
        {
            // We have to get from the current floor to the requested floor.
            // To do that, first we have to find the distance between the floors.
            int nextFloorIndex = Array.IndexOf(this.Floors, floor);
            int distance = Math.Abs(currentFloorIndex - nextFloorIndex);

            Logger.WriteLine($"The elevator is travelling to floor {floor}...");
            this.state = ElevatorState.Moving;
            return Task.Delay(distance * MS_PER_FLOOR)
                .ContinueWith((_) =>
                {
                    currentFloorIndex = nextFloorIndex;
                    this.state = ElevatorState.Waiting;
                });
        }
    }

    enum ElevatorState
    {
        Waiting,
        Moving,
        Closed
    }
}
