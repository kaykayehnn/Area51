using System;

namespace Area51
{
    class Agent
    {
        private Random random;
        private Elevator elevator;
        private string currentFloor;

        public Agent(ConfidentialityLevel confidentialityLevel, Elevator elevator, Random random)
        {
            this.ConfidentialityLevel = confidentialityLevel;
            this.elevator = elevator;
            this.random = random;
            this.currentFloor = elevator.Floors[0];
        }

        public async void RoamAroundTheBase()
        {
            while (true)
            {
                int nextAction = random.Next(10);
                if (nextAction < 6)
                {
                    Console.WriteLine($"{this} is walking around the base...");
                }
                else
                {
                    // Get in elevator
                    Console.WriteLine($"{this} calls the elevator");
                    try
                    {
                        await elevator.Call(this.currentFloor, this, () =>
                        {
                            // Execute on getting in the elevator
                            Console.WriteLine($"{this} gets in the elevator...");
                            var nextFloor = elevator.Floors[random.Next(elevator.Floors.Length)];
                            Console.WriteLine($"{this} presses the button for floor {nextFloor}");
                            return nextFloor;
                        });
                    }
                    catch (InsufficientClearanceException)
                    {
                        Console.WriteLine($"{this} was not allowed to leave the elevator");
                        // TODO: await until he gets back to a floor
                    }

                }
            }
        }

        public ConfidentialityLevel ConfidentialityLevel { get; }

        public override string ToString()
        {
            return $"Agent {ConfidentialityLevel}";
        }
    }
}
