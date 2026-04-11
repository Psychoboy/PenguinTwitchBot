namespace DotNetTwitchBot.Bot.Commands
{
    public static class CooldownHelper
    {
        /// <summary>
        /// Calculates a cooldown value. If max is set and greater than or equal to min,
        /// returns a random value between min and max (inclusive). Otherwise, returns min.
        /// </summary>
        /// <param name="min">Minimum cooldown value in seconds</param>
        /// <param name="max">Maximum cooldown value in seconds (0 or less to use only min)</param>
        /// <returns>Calculated cooldown in seconds</returns>
        public static int CalculateCooldown(int min, int max)
        {
            // If max is not set or is less than min, use min value
            if (max <= 0 || max < min)
            {
                return min;
            }

            // Return random value between min and max (inclusive) using cryptographic random
            return StaticTools.RandomRange(min, max);
        }
    }
}
