using DotNetTwitchBot.Bot.Models.IpLogs;
using DotNetTwitchBot.Models;
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

        public async Task<PagedDataResponse<IpLogUsersWithSameIp>> GetAllDuplicateIps(int offset, int limit)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var query = await unitOfWork.IpLogs.GetAllUsersWithDuplicateIps();
            return new PagedDataResponse<IpLogUsersWithSameIp>
            {
                TotalItems = await query.CountAsync(),
                Data = await query.Skip(offset * limit).Take(limit).ToListAsync(),
            };
        }
    }
}
