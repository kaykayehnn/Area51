using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Area51
{
    class Elevator
    {
        private const int MS_PER_FLOOR = 1000;
        private string currentFloor;
        private ElevatorState state;
        private object @lock;
        // We use a linked list instead of a queue here, because we need to
        // traverse the queue and remove elements from its middle if there are
        // multiple calls from the same floor.
        private LinkedList<ElevatorCall> elevatorCallsQueue;
        private Dictionary<string, TaskCompletionSource> elevatorCallTasks;
        private List<Agent> agentsInside;

        public Elevator()
        {
            this.Floors = new[] { "G", "S", "T1", "T2" };
            this.state = ElevatorState.Closed;
            this.@lock = new object();
            this.elevatorCallsQueue = new LinkedList<ElevatorCall>();
            this.elevatorCallTasks = new Dictionary<string, TaskCompletionSource>();
            this.agentsInside = new List<Agent>();
        }

        public string[] Floors { get; }

        public async void Start()
        {
            this.state = ElevatorState.Waiting;

            while (this.state != ElevatorState.Closed)
            {
                ElevatorCall currentCall = null;
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
                    this.HandleCaller(currentCall);
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
            lock (this.@lock)
            {
                if (this.elevatorCallTasks.TryGetValue(floor, out var value))
                {
                    return value.Task;
                }

                var elevatorCall = new ElevatorCall(floor, agent, chooseFloorFunc);

                this.elevatorCallsQueue.AddLast(elevatorCall);

                var taskCompletionSource = new TaskCompletionSource();

                this.elevatorCallTasks.Add(floor, taskCompletionSource);

                return taskCompletionSource.Task;
            }
        }

        private async void HandleCaller(ElevatorCall call)
        {
            var nextFloor = call.Floor;
            // Now we have to get from the current floor to the required floor.
            // To do that, first we have to find the distance between the floors.
            int currentFloorIndex = Array.IndexOf(this.Floors, this.currentFloor);
            int nextFloorIndex = Array.IndexOf(this.Floors, nextFloor);

            int distance = Math.Abs(currentFloorIndex - nextFloorIndex);

            Console.WriteLine($"The elevator is travelling to floor {nextFloor}...");
            await Task.Delay(distance * MS_PER_FLOOR);

            Console.WriteLine($"The elevator arrived at floor {nextFloor}");
            var pressedButtons = new HashSet<string>();
            var pressedButton = this.EnterAgentIntoElevator(call.Agent, call.ChooseFloorFunc);
            pressedButtons.Add(pressedButton);

            // Now we need to find if there are other callers waiting on the same floor.
            var currentNode = elevatorCallsQueue.First;
            while (currentNode != null)
            {
                LinkedListNode<ElevatorCall> toRemove = null;
                if (currentNode.Value.Floor == nextFloor)
                {
                    // We have found a matching call. Remove the caller from the queue
                    // and put them in the elevator.
                    var caller = currentNode.Value;
                    pressedButton = this.EnterAgentIntoElevator(caller.Agent, caller.ChooseFloorFunc);
                    pressedButtons.Add(pressedButton);

                    toRemove = currentNode;
                }

                currentNode = currentNode.Next;
                if (toRemove != null)
                {
                    // Remove the caller from the task queue, as we have already serviced them.
                    elevatorCallsQueue.Remove(toRemove);
                }
            }

            // We have handled all agents on the current floor, and we now have to press their buttons.
            foreach (var button in pressedButtons)
            {

            }

            // Notify all agents that they have got into the elevator successfully.
            this.elevatorCallTasks[nextFloor].SetResult();
            Console.WriteLine("Elevator doors are closing...");


            // Execute task..
        }

        private string EnterAgentIntoElevator(Agent agent, Func<string> chooseFloorFunc)
        {
            this.agentsInside.Add(agent);

            return chooseFloorFunc();
        }
    }

    enum ElevatorState
    {
        Waiting,
        Moving,
        Closed
    }

    class ElevatorCall
    {
        public ElevatorCall(string floor, Agent agent, Func<string> chooseFloorFunc)
        {
            this.Floor = floor;
            this.Agent = agent;
            this.ChooseFloorFunc = chooseFloorFunc;
        }

        public string Floor { get; }
        public Agent Agent { get; }
        public Func<string> ChooseFloorFunc { get; set; }
    }
}
