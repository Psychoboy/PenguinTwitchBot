using PenguinTwitchBot.Bot.Commands.Games.Starship;

namespace PenguinTwitchBot.Test.Bot.Commands.Games;

public class StarshipExpeditionStoryTests
{
    [Theory]
    [InlineData("SalvageSite")]
    [InlineData("SignalDetected")]
    [InlineData("Hazard")]
    public void GenerateSubject_IsDeterministicAndNamed(string eventType)
    {
        var first = StarshipExpeditionStory.GenerateSubject(42, 3, eventType);

        Assert.StartsWith("the ", first, StringComparison.Ordinal);
        Assert.Equal(first, StarshipExpeditionStory.GenerateSubject(42, 3, eventType));
        Assert.Contains(Enumerable.Range(1, 40), sequence =>
            StarshipExpeditionStory.GenerateSubject(42, sequence, eventType) != first);
    }

    [Fact]
    public void GenerateSubject_UnknownEventHasNoSubject() =>
        Assert.Equal(string.Empty, StarshipExpeditionStory.GenerateSubject(1, 1, "RouteChoice"));

    [Fact]
    public void Render_UsesViewerNamesSubjectAndLoot()
    {
        var expeditionEvent = new StarshipExpeditionEventSnapshot
        {
            Sequence = 2, EventType = "SalvageSite", Sector = "Kepler Reach", Subject = "the MV Hollow Lantern"
        };

        Assert.Equal("Pilot boards the MV Hollow Lantern.",
            StarshipExpeditionStory.Render("{crew} boards {subject}.", expeditionEvent, 7, ["Pilot"], null));
        Assert.Equal("Pilot and Engineer lock on. The MV Hollow Lantern holds an intact cryo-vault.",
            StarshipExpeditionStory.Render("{crewlist} lock on. {Subject} holds {loot}.", expeditionEvent, 7, ["Pilot", "Engineer"], "Rare"));
        Assert.StartsWith("Pilot and 2 others", StarshipExpeditionStory.Render("{crewlist}", expeditionEvent, 7, ["Pilot", "Engineer", "Medic"], null), StringComparison.Ordinal);
        Assert.Equal("The crew boards the MV Hollow Lantern.",
            StarshipExpeditionStory.Render("{crew} boards {subject}.", expeditionEvent, 7, [], null));
        Assert.Equal("something valuable", StarshipExpeditionStory.Render("{loot}", expeditionEvent, 7, [], null));
    }
}
