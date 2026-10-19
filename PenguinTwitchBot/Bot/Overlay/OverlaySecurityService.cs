using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PenguinTwitchBot.Database.Bot.Models;
using PenguinTwitchBot.Database.Repository;
using System.Security.Cryptography;

namespace PenguinTwitchBot.Bot.Overlay
{
    public class OverlaySecurityService(
        IServiceScopeFactory scopeFactory,
        ILogger<OverlaySecurityService> logger) : IOverlaySecurityService
    {
        public const string SettingKey = "overlay.security.key";

        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly string _fallbackKey = RandomNumberGenerator.GetHexString(32);
        private string? _cachedKey;

        public async Task<string> GetOverlayKeyAsync()
        {
            if (!string.IsNullOrWhiteSpace(_cachedKey))
            {
                return _cachedKey;
            }

            await _lock.WaitAsync();
            try
            {
                if (!string.IsNullOrWhiteSpace(_cachedKey))
                {
                    return _cachedKey;
                }

                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork != null)
                {
                    var setting = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
                    if (setting != null && !string.IsNullOrWhiteSpace(setting.StringSetting))
                    {
                        _cachedKey = setting.StringSetting.Trim();
                        return _cachedKey;
                    }

                    var newKey = RandomNumberGenerator.GetHexString(32);
                    _cachedKey = newKey;
                    await PersistKeyAsync(unitOfWork, newKey);
                    return _cachedKey;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load overlay security key from database; using in-memory fallback.");
            }
            finally
            {
                _lock.Release();
            }

            return _fallbackKey;
        }

        public async Task<string> RegenerateOverlayKeyAsync()
        {
            await _lock.WaitAsync();
            try
            {
                var newKey = RandomNumberGenerator.GetHexString(32);
                _cachedKey = newKey;

                using var scope = scopeFactory.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetService<IUnitOfWork>();
                if (unitOfWork != null)
                {
                    await PersistKeyAsync(unitOfWork, newKey);
                }

                return newKey;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to persist regenerated overlay security key to database.");
                return _cachedKey ?? _fallbackKey;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> ValidateKeyAsync(string? providedKey)
        {
            if (string.IsNullOrWhiteSpace(providedKey))
            {
                return false;
            }

            var expectedKey = await GetOverlayKeyAsync();
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                return false;
            }

            return string.Equals(providedKey.Trim(), expectedKey, StringComparison.Ordinal);
        }

        private static async Task PersistKeyAsync(IUnitOfWork unitOfWork, string key)
        {
            var existing = (await unitOfWork.Settings.GetAsync(x => x.Name == SettingKey)).FirstOrDefault();
            if (existing == null)
            {
                await unitOfWork.Settings.AddAsync(new Setting
                {
                    Name = SettingKey,
                    DataType = Setting.DataTypeEnum.String,
                    StringSetting = key
                });
            }
            else
            {
                existing.DataType = Setting.DataTypeEnum.String;
                existing.StringSetting = key;
                unitOfWork.Settings.Update(existing);
            }
            await unitOfWork.SaveChangesAsync();
        }
    }
}

