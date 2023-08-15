namespace DotNetTwitchBot.Bot.Alerts
{
    public interface ISendAlerts
    {
        void QueueAlert(BaseAlert alert);
        void QueueAlert(string alert);
    }
}