using DotNetTwitchBot.Bot.Core;
using DotNetTwitchBot.Bot.Events;

namespace DotNetTwitchBot.Bot.Commands
{
    public abstract class BaseCommand //: IHostedService
    {
        Dictionary<string, Dictionary<string, DateTime>> _coolDowns = new Dictionary<string, Dictionary<string, DateTime>>();
        Dictionary<string, DateTime> _globalCooldowns = new Dictionary<string, DateTime>();
        public BaseCommand(ServiceBackbone serviceBackbone)
        {
            _serviceBackbone = serviceBackbone;
            serviceBackbone.CommandEvent += OnCommand;
        }

        protected ServiceBackbone _serviceBackbone { get; }

        public async Task SendChatMessage(string message)
        {
            await _serviceBackbone.SendChatMessage(message);
        }

        public async Task SendChatMessage(string name, string message)
        {
            await _serviceBackbone.SendChatMessage(name, message);
        }

        // public virtual Task StartAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }
        // public virtual Task StopAsync(CancellationToken cancellationToken) { return Task.CompletedTask; }

        protected abstract Task OnCommand(object? sender, CommandEventArgs e);

        protected bool IsCoolDownExpired(string user, string command)
        {
            if (
                _globalCooldowns.ContainsKey(command) &&
                _globalCooldowns[command] > DateTime.Now)
            {
                return false;
            }
            if (_coolDowns.ContainsKey(user.ToLower()))
            {
                if (_coolDowns[user.ToLower()].ContainsKey(command))
                {
                    if (_coolDowns[user.ToLower()][command] > DateTime.Now)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected async Task<bool> IsCoolDownExpiredWithMessage(string user, string displayName, string command)
        {
            if (!IsCoolDownExpired(user, command))
            {
                await _serviceBackbone.SendChatMessage(displayName, string.Format("!{0} is still on cooldown: {1}", command, CooldownLeft(user, command)));
                return false;
            }
            return true;
        }

        protected string CooldownLeft(string user, string command)
        {

            var globalCooldown = DateTime.MinValue;
            var userCooldown = DateTime.MinValue;
            if (
               _globalCooldowns.ContainsKey(command) &&
               _globalCooldowns[command] > DateTime.Now)
            {
                globalCooldown = _globalCooldowns[command];
            }
            if (_coolDowns.ContainsKey(user.ToLower()))
            {
                if (_coolDowns[user.ToLower()].ContainsKey(command))
                {
                    if (_coolDowns[user.ToLower()][command] > DateTime.Now)
                    {
                        userCooldown = _coolDowns[user.ToLower()][command];
                    }
                }
            }

            if (globalCooldown == DateTime.MinValue && userCooldown == DateTime.MinValue)
            {
                return "";
            }

            if (globalCooldown > userCooldown)
            {
                var timeDiff = globalCooldown - DateTime.Now;
                // return FormatTimeSpan(timeDiff);
                return timeDiff.ToFriendlyString();
            }
            else if (userCooldown > globalCooldown)
            {
                var timeDiff = userCooldown - DateTime.Now;
                // return FormatTimeSpan(timeDiff);
                return timeDiff.ToFriendlyString();
            }
            return "";
        }

        private string FormatTimeSpan(TimeSpan timeDiff)
        {
            return timeDiff.ToString(@"d\d\ hh\hmm\mss\s")
            .TrimStart(' ', 'd', 'h', 'm', 's', '0');
        }

        protected void AddCoolDown(string user, string command, int cooldown)
        {
            AddCoolDown(user, command, DateTime.Now.AddSeconds(cooldown));
        }

        protected void AddCoolDown(string user, string command, DateTime cooldown)
        {
            if (!_coolDowns.ContainsKey(user.ToLower()))
            {
                _coolDowns[user.ToLower()] = new Dictionary<string, DateTime>();
            }

            _coolDowns[user.ToLower()][command] = cooldown;
        }

        protected void AddGlobalCooldown(string command, int cooldown)
        {
            AddGlobalCooldown(command, DateTime.Now.AddSeconds(cooldown));
        }

        protected void AddGlobalCooldown(string command, DateTime cooldown)
        {
            _globalCooldowns[command] = cooldown;
        }
    }
}
