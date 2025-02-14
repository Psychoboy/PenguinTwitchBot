
namespace DotNetTwitchBot.Bot.ServiceTools
{
    public interface IServiceMaintenance
    {
        IEnumerable<Type> GetServiceTypes();
        Task RestartService(Type serviceType);
    }
}