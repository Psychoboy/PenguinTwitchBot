using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNetTwitchBot.Bot.Commands.Custom
{
    public class CustomCommandResult
    {
        public string Message { get; private set; } = "";
        public bool Cancel { get; private set; } = false;

        public CustomCommandResult()
        {

        }
        public CustomCommandResult(string message)
        {
            Message = message;
        }

        public CustomCommandResult(bool cancel)
        {
            Cancel = cancel;
        }

        public CustomCommandResult(string message, bool cancel)
        {
            Message = message;
            Cancel = cancel;
        }
    }
}