namespace DotNetTwitchBot.Bot.Queues
{
    public interface ISubActionExecutionContextAccessor
    {
        ISubActionExecutionContext? ExecutionContext { get; set; }
    }

    public class SubActionExecutionContextAccessor : ISubActionExecutionContextAccessor
    {
        public ISubActionExecutionContext? ExecutionContext { get; set; }
    }
}
