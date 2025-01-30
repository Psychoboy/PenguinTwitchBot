using DotNetTwitchBot.Bot.Models.Wheel;

namespace DotNetTwitchBot.Bot.Commands.WheelSpin
{
    public interface IWheelService
    {
        Task AddWheel(Wheel wheel);
        Task DeleteProperties(List<WheelProperty> properties);
        Task DeleteWheel(Wheel wheel);
        Task<List<Wheel>> GetWheels();
        void HideWheel();
        Task SaveWheel(Wheel wheel);
        void OpenNameWheel();
        void ShowNameWheel();
        void ShowWheel(Wheel wheel);
        void SpinWheel(Wheel wheel);
        Task ValidateAndProcessWinner(int index);
        void SpinNameWheel();
        void CloseNameWheel();
    }
}
