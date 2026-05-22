using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Bot.Models.IpLogs;
using PenguinTwitchBot.Models;
using PenguinTwitchBot.Repository;

namespace PenguinTwitchBot.Bot.Admin
{
    public class IpLogFeature(IServiceScopeFactory scopeFactory) : IIpLogFeature
    {
        public async Task<List<IpLogEntry>> GetIpLogsForUser(string username)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.IpLogs.GetKnownIpsForUser(UsernameNormalizer.Normalize(username));
        }

        public async Task<List<IpLogEntry>> GetDuplicateIpsForUser(string username)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            return await unitOfWork.IpLogs.GetDuplicateIpsForUser(UsernameNormalizer.Normalize(username));
        }

        public async Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps()
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var data = await unitOfWork.IpLogs.GetAllUsersWithDuplicateIps();
            return new PagedDataResponse<IpLogUsersWithSameIp>
            {
                TotalItems = data.Count,
                Data = data,
            };
        }
    }
}
