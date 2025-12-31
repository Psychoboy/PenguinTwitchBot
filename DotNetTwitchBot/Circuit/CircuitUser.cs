namespace DotNetTwitchBot.Circuit
{
    public class CircuitUser
    {
        public string UserName { get; set; } = "Unknown";
        public string CircuitId { get; set; } = "";
        public string LastPage { get; set; } = "";
        public string UserIp { get; set; } = "";
        public DateTime LastSeen { get; set; } = DateTime.Now;
    }
}
