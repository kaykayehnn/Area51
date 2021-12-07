using System;
using System.Threading.Tasks;

namespace Area51.ElevatorCalls
{
    class AgentElevatorCall : IElevatorCall
    {
        public AgentElevatorCall(string floor, Agent agent, Func<string> chooseFloorFunc)
        {
            this.InitialFloor = floor;
            this.Agent = agent;
            this.ChooseFloorFunc = chooseFloorFunc;
            this.TaskCompletionSource = new TaskCompletionSource();
        }

        public string InitialFloor { get; }
        public string Floor => this.InitialFloor;
        public Agent Agent { get; }
        public Func<string> ChooseFloorFunc { get; }
        // DestinationFloor is the floor where the agent wants to get off at.
        // It is used later in the processing pipeline.
        public string DestinationFloor { get; set; }
        public TaskCompletionSource TaskCompletionSource { get; }
    }
}
