namespace Area51.ElevatorCalls
{
    class ButtonPress : IElevatorCall
    {
        public ButtonPress(string floor)
        {
            this.Floor = floor;
        }

        public string Floor { get; }
    }
}
