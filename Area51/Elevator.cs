using Area51.ElevatorCalls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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
        private object queueLock;
        private object stateLock;

        // We use a linked list instead of a queue here, because we need to
        // traverse the queue and remove elements from its middle if there are
        // multiple agents calling for the same floor.
        private LinkedList<IElevatorCall> elevatorCallsQueue;

        // We keep the entire call structures until we have fully processed them.
        private List<AgentElevatorCall> agentsBeingProcessed;

        private CancellationTokenSource cancellationTokenSource;

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
            this.queueLock = new object();
            this.stateLock = new object();
            this.elevatorCallsQueue = new LinkedList<IElevatorCall>();
            this.agentsBeingProcessed = new List<AgentElevatorCall>();
            this.cancellationTokenSource = new CancellationTokenSource();
        }

        public string[] Floors { get; }

        public CancellationToken CancellationToken => this.cancellationTokenSource.Token;

        public async void Start()
        {
            this.SetState(ElevatorState.Waiting);

            while (this.state != ElevatorState.Closed)
            {
                IElevatorCall currentCall = null;
                lock (this.queueLock)
                {
                    if (this.elevatorCallsQueue.Count > 0)
                    {
                        currentCall = this.elevatorCallsQueue.First.Value;
                        this.elevatorCallsQueue.RemoveFirst();
                    }
                }

                try
                {
                    if (currentCall != null)
                    {
                        await this.HandleCall(currentCall);
                    }

                    await Task.Delay(1, this.CancellationToken);
                }
                catch (TaskCanceledException) { }
            }

            // Eject all agents still inside the elevator.
            var i = 0;
            while (i < agentsBeingProcessed.Count)
            {
                var call = agentsBeingProcessed[i];
                ExitAgentFromElevator(call.Agent);
                // TrySet in case an agent call has errored once and it has already been handled.
                call.TaskCompletionSource.TrySetException(new ElevatorClosedException());
                i++;
            }
        }

        public void Stop()
        {
            this.SetState(ElevatorState.Closed);
            this.cancellationTokenSource.Cancel();
        }

        public Task<string> Call(string floor, Agent agent, Func<string> chooseFloorFunc)
        {
            var elevatorCall = new AgentElevatorCall(floor, agent, chooseFloorFunc);
            lock (this.queueLock)
            {
                this.elevatorCallsQueue.AddLast(elevatorCall);
            }
            return elevatorCall.TaskCompletionSource.Task;
        }

        private async Task HandleCall(IElevatorCall call)
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
                    // There is an agent with insufficient clearance for this floor.
                    // We have to immediately go back to drop them off at their initial floor.

                    var getOffElevatorCompletionSource = new TaskCompletionSource<string>();
                    var exception = new InsufficientClearanceException(getOffElevatorCompletionSource.Task);
                    caller.TaskCompletionSource.SetException(exception);

                    // Even though we're modifying the collection in the loop, we can get away
                    // with it because we return before any more iterations can occur.

                    // Set the caller's new completion source, in case the elevator gets closed
                    // amid returning the agent to their initial floor.
                    caller.TaskCompletionSource = getOffElevatorCompletionSource;
                    await this.GoTo(caller.InitialFloor);
                    ExitAgentFromElevator(caller.Agent);
                    getOffElevatorCompletionSource.SetResult(this.Floors[this.currentFloorIndex]);

                    var retryFloor = new ButtonPress(caller.DestinationFloor);
                    lock (this.queueLock)
                    {
                        this.elevatorCallsQueue.AddFirst(retryFloor);
                    }

                    return;
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
                    ExitAgentFromElevator(caller.Agent);
                    caller.TaskCompletionSource.SetResult(nextFloor);
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
                lock (this.queueLock)
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
                    lock (this.queueLock)
                    {
                        this.elevatorCallsQueue.AddFirst(newCall);
                    }
                }

                Logger.WriteLine("Elevator doors are closing...");
            }
        }

        public void DisplayState()
        {
            var DefaultWhiteSpaceOverwriter = new string(' ', 10);
            string nextStops;
            lock (this.queueLock)
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
            int padChars = 1 + (int)Math.Log10(allLines.Count);
            for (int i = firstLineIndex; i < allLines.Count; i++)
            {
                var line = allLines[i];
                var whiteSpaceOverwriter = new string(' ', Console.WindowWidth - (line.Length + padChars + 2));
                lastLogLines.AppendLine($"{(i + 1).ToString().PadLeft(padChars)}: {allLines[i]}{whiteSpaceOverwriter}");
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
        }

        public async void DisplayStateLoop()
        {
            // Console.CursorVisible.get is only accessible on windows.
            bool previousCursorVisible = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? Console.CursorVisible
                : true;
            Console.CursorVisible = false;

            try
            {
                while (this.state != ElevatorState.Closed)
                {
                    this.DisplayState();

                    await Task.Delay(16, this.cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException) { }

            Console.CursorVisible = previousCursorVisible;
        }

        private string EnterAgentIntoElevator(AgentElevatorCall call)
        {
            this.agentsBeingProcessed.Add(call);

            var chosenFloor = call.DestinationFloor;
            return chosenFloor;
        }

        private void ExitAgentFromElevator(Agent agent)
        {
            var index = this.agentsBeingProcessed.FindIndex(aec => aec.Agent == agent);
            this.agentsBeingProcessed.RemoveAt(index);
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
            this.SetState(ElevatorState.Moving, ElevatorState.Waiting);
            return Task.Delay(distance * MS_PER_FLOOR, this.CancellationToken)
                .ContinueWith((_) =>
                {
                    currentFloorIndex = nextFloorIndex;
                    this.SetState(ElevatorState.Waiting, ElevatorState.Moving);
                }, this.CancellationToken);
        }

        private void SetState(ElevatorState toState)
        {
            lock (this.stateLock)
            {
                this.state = toState;
            }
        }

        private void SetState(ElevatorState toState, ElevatorState fromState)
        {
            lock (this.stateLock)
            {
                if (this.state == fromState)
                {
                    this.state = toState;
                }
            }
        }

        enum ElevatorState
        {
            Waiting,
            Moving,
            Closed
        }
    }
}
