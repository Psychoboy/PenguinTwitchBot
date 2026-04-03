namespace DotNetTwitchBot.Bot.Actions.SubActions.UI
{
    /// <summary>
    /// Helper class to get available audio files for PlaySound SubAction.
    /// </summary>
    public static class AudioFileHelper
    {
        /// <summary>
        /// Gets a list of all audio files available in wwwroot/audio directory (without extensions).
        /// </summary>
        public static string[] GetAudioFiles()
        {
            try
            {
                if (!Directory.Exists("wwwroot/audio"))
                {
                    return Array.Empty<string>();
                }

                return Directory.GetFiles("wwwroot/audio")
                    .Select(f => Path.GetFileNameWithoutExtension(f))
                    .Where(f => !string.IsNullOrEmpty(f))
                    .Distinct()
                    .OrderBy(f => f)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
