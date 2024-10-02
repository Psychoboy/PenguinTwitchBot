using DotNetTwitchBot.Repository;

namespace DotNetTwitchBot.Circuit
{
    public class IpLog(IServiceScopeFactory scopeFactory)
    {
        public async Task AddLogEntry(string username, string ipAddress)
        {
            if (username.Equals("anonymous", StringComparison.CurrentCultureIgnoreCase)) return;

            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await db.IpLogs.AddAsync(new IpLogEntry
            {
                Username = username,
                Ip = ipAddress
            });
            await db.SaveChangesAsync();
        }
    }
}
