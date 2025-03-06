using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;
using System.Security.Cryptography;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class RandomIntTagHandler : IRequestHandler<RandomIntTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(RandomIntTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            if (string.IsNullOrEmpty(args)) return new CustomCommandResult();
            var vals = args.Split(',');
            if (vals.Length < 2) return new CustomCommandResult();
            var val1 = int.Parse(vals[0]);
            var val2 = int.Parse(vals[1]);
            return await Task.Run(() => new CustomCommandResult(RandomNumberGenerator.GetInt32(val1, val2).ToString()));
        }
    }
}
