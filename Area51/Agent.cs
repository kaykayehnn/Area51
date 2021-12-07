using System;
using System.Threading.Tasks;

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
                // 40% chance to go to elevator
                if (nextAction < 6)
                {
                    try
                    {
                        Logger.WriteLine($"{this} is walking around the base...");
                        await Task.Delay(1000, elevator.CancellationToken);

                    }
                    catch (TaskCanceledException)
                    {
                        Logger.WriteLine($"{this} could not finish walking around, as the base closed.");
                        return;
                    }
                }
                else
                {
                    // Get in elevator
                    Logger.WriteLine($"{this} calls the elevator.");
                    try
                    {
                        var nextFloor = await elevator.Call(this.currentFloor, this, () =>
                        {
                            // Executed on getting in the elevator
                            Logger.WriteLine($"{this} gets in the elevator...");

                            // Choose which floor the agent wants to go to.
                            string nextFloor;
                            do
                            {
                                nextFloor = elevator.Floors[random.Next(elevator.Floors.Length)];
                            } while (nextFloor == this.currentFloor);

                            Logger.WriteLine($"{this} presses the button for floor {nextFloor}.");
                            return nextFloor;
                        });

                        Logger.WriteLine($"{this} reached floor {nextFloor} succesfully.");
                        this.currentFloor = nextFloor;
                    }
                    catch (InsufficientClearanceException e)
                    {
                        Logger.WriteLine($"{this} was not allowed to leave the elevator due to insufficient clearance.");
                        Logger.WriteLine($"{this} waits to get back to the floor they got on the elevator.");
                        await e.GetOffElevator;
                        Logger.WriteLine($"{this} got off the elevator.");
                    }
                    catch (ElevatorClosedException)
                    {
                        Logger.WriteLine($"{this} was kicked out of the elevator as it closed for the day.");
                        return;
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
