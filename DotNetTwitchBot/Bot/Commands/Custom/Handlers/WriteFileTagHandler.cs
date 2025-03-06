using DotNetTwitchBot.Bot.Commands.Custom.Tags;
using MediatR;

namespace DotNetTwitchBot.Bot.Commands.Custom.Handlers
{
    public class WriteFileTagHandler : IRequestHandler<WriteFileTag, CustomCommandResult>
    {
        public async Task<CustomCommandResult> Handle(WriteFileTag request, CancellationToken cancellationToken)
        {
            var args = request.Args;
            var parseResults = args.Split(",");
            if (parseResults.Length < 3) return new CustomCommandResult(args);
            var fileName = parseResults[0];
            var append = bool.Parse(parseResults[1]);
            var text = parseResults[2];
            if (!append)
            {
                await File.WriteAllTextAsync(fileName, text, cancellationToken);
            }
            else
            {
                await File.AppendAllTextAsync(fileName, "\n" + text, cancellationToken);
            }
            return new CustomCommandResult();
        }
    }
}
