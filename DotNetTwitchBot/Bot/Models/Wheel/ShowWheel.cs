using System.Text.Json.Serialization;

namespace DotNetTwitchBot.Bot.Models.Wheel
{
    public class ShowWheel : WheelBase
    {
        public override string Wheel => "show";
        public List<WheelProperty> Items { get; set; } = new();
        public List<string> WheelColors { get; set; } = ["#ffc93c", "#66bfbf", "#a2d5f2", "#515070", "#43658b", "#ed6663", "#d54062"];
        public List<string> TextColors { get; set; } = ["#fff"];
        public string LineColor { get; set; } = "#fff";
        public float ItemLabelBaselineOffset { get; set; } = -0.07f;
        public float Radius { get; set; } = 0.84f;
    }
}
