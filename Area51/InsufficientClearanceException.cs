using System;
using System.Threading.Tasks;

namespace Area51
{
    class InsufficientClearanceException : Exception
    {
        public InsufficientClearanceException(Task getOffElevator)
        {
            this.GetOffElevator = getOffElevator;
        }

        public Task GetOffElevator { get; }
    }
}
