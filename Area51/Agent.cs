using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
                if (nextAction < 6)
                {
                    Console.WriteLine($"Agent {ConfidentialityLevel} is walking around the base...");
                }
                else
                {
                    // Get in elevator
                    Console.WriteLine($"Agent {ConfidentialityLevel} calls the elevator");
                    await elevator.Call(this.currentFloor);
                }
            }
        }

        public ConfidentialityLevel ConfidentialityLevel { get; }
    }
}
