using Microsoft.EntityFrameworkCore;
using PenguinTwitchBot.Database.Bot.Actions;
using PenguinTwitchBot.Database.Bot.Actions.SubActions.Types;
using PenguinTwitchBot.Database.Bot.Core.Database;
using PenguinTwitchBot.Database.Bot.Models.Credits;
using PenguinTwitchBot.Test.Helpers;

namespace PenguinTwitchBot.Test.Bot.Overlay.Credits
{
    public class CreditsMigrationTests
    {
        [Fact]
        public async Task SqliteMigrations_CreateAndRoundTripAllCreditsAndSubActionTables()
        {
            await using var connection = SqliteTestDatabase.CreateMigratedConnection();
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            await using (var ctx = new ApplicationDbContext(options))
            {
                var theme = new CreditTheme
                {
                    Name = "Migration Test Theme",
                    IsBuiltIn = false,
                    ThemeJson = "{}"
                };
                var profile = new CreditProfile
                {
                    Name = "Migration Profile",
                    IsDefault = true,
                    Theme = theme,
                    ConfigJson = "{}"
                };
                var session = new CreditStreamSession
                {
                    Title = "Migrated Stream",
                    StartedAtUtc = DateTime.UtcNow.AddHours(-2)
                };
                var ev = new CreditEvent
                {
                    EventType = CreditEventTypes.Subscribe,
                    UserId = "999",
                    Username = "MigUser",
                    DisplayName = "MigUser",
                    Amount = 1m,
                    StreamSession = session
                };
                ev.NormalizeUsername();

                var action = new ActionType
                {
                    Name = "Credits Action Migration Test",
                    SubActions =
                    [
                        new CreditsPlayType { ProfileName = "Default" },
                        new CreditsStopType { ProfileName = "Default" },
                        new CreditsPauseResumeType { ProfileName = "Default", ActionMode = CreditsPauseResumeType.Toggle },
                        new CreditsMarkStartType { ProfileName = "Default", Label = "Start" },
                        new CreditsAddEntryType { Username = "guest", SectionTag = "Manual" }
                    ]
                };

                ctx.CreditThemes.Add(theme);
                ctx.CreditProfiles.Add(profile);
                ctx.CreditStreamSessions.Add(session);
                ctx.CreditEvents.Add(ev);
                ctx.Actions.Add(action);
                await ctx.SaveChangesAsync();
            }

            await using var verify = new ApplicationDbContext(options);
            var loadedProfile = await verify.CreditProfiles.Include(p => p.Theme).SingleAsync();
            Assert.Equal("Migration Profile", loadedProfile.Name);
            Assert.Equal("Migration Test Theme", loadedProfile.Theme?.Name);

            var loadedEvent = await verify.CreditEvents.Include(e => e.StreamSession).SingleAsync();
            Assert.Equal("miguser", loadedEvent.Username);
            Assert.Equal("Migrated Stream", loadedEvent.StreamSession?.Title);

            var loadedAction = await verify.Actions.Include(a => a.SubActions).SingleAsync();
            Assert.Equal(5, loadedAction.SubActions.Count);
            Assert.Contains(loadedAction.SubActions, s => s is CreditsPlayType);
            Assert.Contains(loadedAction.SubActions, s => s is CreditsStopType);
            Assert.Contains(loadedAction.SubActions, s => s is CreditsPauseResumeType);
            Assert.Contains(loadedAction.SubActions, s => s is CreditsMarkStartType);
            Assert.Contains(loadedAction.SubActions, s => s is CreditsAddEntryType);
        }
    }
}
