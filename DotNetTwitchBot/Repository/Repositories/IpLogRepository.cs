

namespace DotNetTwitchBot.Repository.Repositories
{
    public class IpLogRepository(ApplicationDbContext context) : GenericRepository<IpLogEntry>(context), IIpLogRepository
    {
        public Task GetDuplicateIps()
        {
            throw new NotImplementedException();
        }

        public Task<List<IpLogEntry>> GetKnownIpsForUser(string username)
        {
            throw new NotImplementedException();
        }
    }
}
