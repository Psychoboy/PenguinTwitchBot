using System.Threading.Tasks;

namespace PenguinTwitchBot.TwitchApi.EventSub
{
    public delegate Task AsyncEventHandler<in TEventArgs>(object? sender, TEventArgs e);
    public delegate Task AsyncEventHandler(object? sender, System.EventArgs e);
}