namespace DotNetTwitchBot.Twitch.EventSub.Core.Extensions;

public static class AsyncEventHandlerExtensions
{
    public static Task InvokeAsync<TEventArgs>(this AsyncEventHandler<TEventArgs> asyncEventHandler, object sender, TEventArgs args)
    {
        return asyncEventHandler != null ? asyncEventHandler(sender, args) : Task.CompletedTask;
    }

    public static Task InvokeAsync(this AsyncEventHandler asyncEventHandler, object sender, EventArgs args)
    {
        return asyncEventHandler != null ? asyncEventHandler(sender, args) : Task.CompletedTask;
    }
}