namespace PenguinTwitchBot.TwitchApi.EventSub.Websockets.Extensions
{
    public static class AsyncEventHandlerExtensions
    {
        public static Task InvokeAsync<TEventArgs>(this AsyncEventHandler<TEventArgs>? asyncEventHandler, object? sender, TEventArgs e)
        {
            return asyncEventHandler != null ? asyncEventHandler(sender, e) : Task.CompletedTask;
        }

        public static Task InvokeAsync(this AsyncEventHandler? asyncEventHandler, object? sender, System.EventArgs e)
        {
            return asyncEventHandler != null ? asyncEventHandler(sender, e) : Task.CompletedTask;
        }
    }
}