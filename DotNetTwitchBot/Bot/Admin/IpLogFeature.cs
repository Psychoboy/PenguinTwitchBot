using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Bot.Admin
{
    public class IpLogFeature(IServiceScopeFactory scopeFactory) : IIpLogFeature
    {
        public async Task<List<IpLogsForUser>> GetIpLogsForUser(string username)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.IpLogs.GetKnownIpsForUser(username);
        }

        public async Task<List<IpLogsForUser>> GetDuplicateIpsForUser(string username)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.IpLogs.GetDuplicateIpsForUser(username);
        }

        public async Task<List<IpLogUsersWithSameIp>> GetAllDuplicateIps(int index, int offset)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.IpLogs.GetAllUsersWithDuplicateIps(index, offset);
        }
    }
}
