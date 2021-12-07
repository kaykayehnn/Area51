using System;
using System.Threading.Tasks;

namespace Area51.ElevatorCalls
{
    class AgentElevatorCall : IElevatorCall
    {
        private string destinationFloor;
        private Func<string> chooseFloorFunc;
        public AgentElevatorCall(string floor, Agent agent, Func<string> chooseFloorFunc)
        {
            this.InitialFloor = floor;
            this.Agent = agent;
            this.chooseFloorFunc = chooseFloorFunc;
            this.TaskCompletionSource = new TaskCompletionSource();
        }

        public string InitialFloor { get; }
        public string Floor => this.InitialFloor;
        public Agent Agent { get; }
        
        // DestinationFloor is the floor where the agent wants to get off at.
        // It is used later in the processing pipeline.
        public string DestinationFloor
        {
            get
            {
                if(this.destinationFloor == null)
                {
                    this.destinationFloor = this.chooseFloorFunc();
                }

                return this.destinationFloor;
            }
        }
        public TaskCompletionSource TaskCompletionSource { get; }
    }
}
