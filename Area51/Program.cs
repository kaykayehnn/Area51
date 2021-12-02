using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Area51
{
    class Program
    {
        static void Main(string[] args)
        {
            var random = new Random();

            var tasks = new List<Task>();

            var elevator = new Elevator();

            var agent1 = new Agent(ConfidentialityLevel.Confidential, elevator, random);
            var agent2 = new Agent(ConfidentialityLevel.Secret, elevator, random);
            var agent3 = new Agent(ConfidentialityLevel.TopSecret, elevator, random);

            tasks.Add(Task.Run(elevator.Start));

            tasks.Add(Task.Run(agent1.RoamAroundTheBase));
            tasks.Add(Task.Run(agent2.RoamAroundTheBase));
            tasks.Add(Task.Run(agent3.RoamAroundTheBase));



            Task.WhenAll(tasks).Wait();
        }
    }
}
