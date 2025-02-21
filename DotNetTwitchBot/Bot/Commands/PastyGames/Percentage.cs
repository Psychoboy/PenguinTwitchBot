using System.ComponentModel;
using System.Globalization;

namespace DotNetTwitchBot.Bot.Commands.PastyGames
{
    [TypeConverter(typeof(PercentageConverter))]
    public struct Percentage
    {
        public double Value;

        public Percentage(double value)
        {
            Value = value;
        }

        public Percentage(string value)
        {
            var pct = (Percentage)TypeDescriptor.GetConverter(GetType()).ConvertFromString(value)!;
            Value = pct.Value;
        }

        public override string ToString()
        {
            return ToString(CultureInfo.InvariantCulture);
        }

        public string ToString(CultureInfo Culture)
        {
            return TypeDescriptor.GetConverter(GetType()).ConvertToString(null, Culture, this)!;
        }
    }
}
