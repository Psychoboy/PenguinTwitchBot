namespace DotNetTwitchBot.Repository
{
    public interface IIpLogRepository : IGenericRepository<IpLogEntry>
    {
        Task<List<IpLogEntry>> GetKnownIpsForUser(string username);
        Task GetDuplicateIps();
    }
}
