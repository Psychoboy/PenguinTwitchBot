﻿namespace DotNetTwitchBot.CustomMiddleware
{
    public class BlazorAppContext
    {
        /// <summary>
        /// The IP for the current session
        /// </summary>
        public string? CurrentUserIP { get; set; }
    }
}
