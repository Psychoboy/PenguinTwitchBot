namespace DotNetTwitchBot.Bot.Models.Wheel
{
    public class SpinWheel (int spinTo) : WheelBase
    {
        public override string Wheel => "spin";
        public int SpinTo { get; set; } = spinTo;
    }
}
