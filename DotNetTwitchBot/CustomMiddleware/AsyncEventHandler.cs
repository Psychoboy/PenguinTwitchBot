namespace DotNetTwitchBot.CustomMiddleware
{
    public delegate Task AsyncEventHandler<in TEventArgs>(object sender, TEventArgs args);
    public delegate Task AsyncEventHandler(object sender, EventArgs args);
}
