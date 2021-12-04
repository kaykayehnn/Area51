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
        private Queue<string> taskQueue;
        private Dictionary<string, TaskCompletionSource> taskDictionary;
        private List<Agent> intelInside; // hehe

        public Elevator()
        {
            this.Floors = new[] { "G", "S", "T1", "T2" };
            this.state = ElevatorState.Closed;
            this.@lock = new object();
            this.taskQueue = new Queue<string>();
            this.taskDictionary = new Dictionary<string, TaskCompletionSource>();
            this.intelInside = new List<Agent>();
        }

        public async void Start()
        {
            this.state = ElevatorState.Waiting;

            while (this.state != ElevatorState.Closed)
            {
                string nextFloor = null;
                lock (this.@lock)
                {
                    this.taskQueue.TryDequeue(out var task);
                }

                if (nextFloor != null)
                {
                    // Now we have to get from the current floor to the required floor.
                    // To do that, first we have to find the distance between the floors.
                    int currentFloorIndex = Array.IndexOf(this.Floors, this.currentFloor);
                    int nextFloorIndex = Array.IndexOf(this.Floors, nextFloor);

                    int distance = Math.Abs(currentFloorIndex - nextFloorIndex);

                    Console.WriteLine($"The elevator is travelling to floor {nextFloor}...");

                    await Task.Delay(distance * MS_PER_FLOOR);

                    Console.WriteLine($"The elevator arrived at floor {nextFloor}");

                    this.taskDictionary[nextFloor].SetResult();
                    // Execute task..
                }

                Task.Delay(1).Wait();
            }
        }

        public void Stop()
        {
            this.state = ElevatorState.Closed;
        }

        public Task Call(string floor, Agent agent, Func<string> onGettingIn)
        {
            lock (this.@lock)
            {
                if (this.taskDictionary.TryGetValue(floor, out var value))
                {
                    return value.Task;
                }

                this.taskQueue.Enqueue(floor);

                var taskCompletionSource = new TaskCompletionSource();

                this.taskDictionary.Add(floor, taskCompletionSource);

                return taskCompletionSource.Task;
            }
            //lock (this.@lock)
            //{
            //    while (true)
            //    {
            //        switch (this.state)
            //        {
            //            case ElevatorState.Waiting:
            //                await this.currentTask;
            //                break;
            //            case ElevatorState.Moving:
            //                break;
            //            default:
            //                throw new InvalidOperationException($"Unexpected elevator state: {this.state}");
            //        }
            //    }
            //}

            return null;
        }

        public string[] Floors { get; }
    }

    enum ElevatorState
    {
        Waiting,
        Moving,
        Closed
    }
}
