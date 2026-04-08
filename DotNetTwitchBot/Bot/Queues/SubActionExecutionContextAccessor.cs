namespace DotNetTwitchBot.Bot.Queues
{
    public interface ISubActionExecutionContextAccessor
    {
        ISubActionExecutionContext? ExecutionContext { get; set; }
        int CurrentSubActionIndex { get; set; }
    }

    public class SubActionExecutionContextAccessor : ISubActionExecutionContextAccessor
    {
        public ISubActionExecutionContext? ExecutionContext { get; set; }
        public int CurrentSubActionIndex { get; set; } = -1;
    }
}
