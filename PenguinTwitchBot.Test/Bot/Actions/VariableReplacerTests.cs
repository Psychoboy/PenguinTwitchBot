using System;
using System.Collections.Concurrent;
using NSubstitute;
using PenguinTwitchBot.Bot.Actions.SubActions;
using PenguinTwitchBot.Bot.Core;
using Xunit;

namespace PenguinTwitchBot.Test.Bot.Actions
{
    public class VariableReplacerTests
    {
        [Fact]
        public void ReplaceVariables_SubstitutesDictionaryVariables()
        {
            var variables = new ConcurrentDictionary<string, string>();
            variables["user"] = "PenguinFan";
            variables["points"] = "150";

            var result = VariableReplacer.ReplaceVariables("Hello %user%, you have %points% points!", variables);

            Assert.Equal("Hello PenguinFan, you have 150 points!", result);
        }

        [Fact]
        public void ReplaceVariables_DictionaryOverridesSystemVariables()
        {
            var variables = new ConcurrentDictionary<string, string>();
            variables["bot"] = "CustomBotName";
            variables["date"] = "2000-01-01";

            var result = VariableReplacer.ReplaceVariables("Bot: %bot%, Date: %date%", variables);

            Assert.Equal("Bot: CustomBotName, Date: 2000-01-01", result);
        }

        [Fact]
        public void ReplaceVariables_SubstitutesSystemVariables_WithServiceBackbone()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BotName.Returns("SuperPenguinBot");
            backbone.BroadcasterName.Returns("CaptainPenguin");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                var result = VariableReplacer.ReplaceVariables("Bot: %bot%, Streamer: %streamer%, User: %user%", variables);

                Assert.Equal("Bot: SuperPenguinBot, Streamer: CaptainPenguin, User: CaptainPenguin", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_UserFromTrigger_OverridesBroadcasterFallback()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("StreamerBroadcaster");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                variables["User"] = "TriggerInvoker";

                var result = VariableReplacer.ReplaceVariables("Invoked by: %user%", variables);

                Assert.Equal("Invoked by: TriggerInvoker", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_NameOnlyInTrigger_OverridesBroadcasterFallback()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("StreamerBroadcaster");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                variables["Name"] = "TriggerViewerName";

                var result = VariableReplacer.ReplaceVariables("Invoked by: %user%", variables);

                Assert.Equal("Invoked by: TriggerViewerName", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_UserMissingFromTrigger_FallsBackToBroadcaster()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BroadcasterName.Returns("StreamerBroadcaster");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                // No User or Name in dictionary (e.g., timer trigger)

                var result = VariableReplacer.ReplaceVariables("Invoked by: %user%", variables);

                Assert.Equal("Invoked by: StreamerBroadcaster", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_SubstitutesDateAndTimeInLocalFormat()
        {
            var variables = new ConcurrentDictionary<string, string>();
            var expectedDate = DateTime.Now.ToShortDateString();

            var result = VariableReplacer.ReplaceVariables("Date: %date%, Time: %time%, Ticks: %ticks%", variables);

            Assert.Contains($"Date: {expectedDate}", result);
            Assert.DoesNotContain("%time%", result);
            Assert.DoesNotContain("%ticks%", result);
        }

        [Fact]
        public void ReplaceVariables_SubstitutesRandomDefaultAndRange()
        {
            var variables = new ConcurrentDictionary<string, string>();

            var result = VariableReplacer.ReplaceVariables("Roll: %random%, Die: %random(1, 6)%", variables);

            Assert.DoesNotContain("%random%", result);
            Assert.DoesNotContain("%random(1, 6)%", result);

            // Verify the values parsed as integers within bounds
            var parts = result.Split(',');
            var rollVal = int.Parse(parts[0].Replace("Roll: ", "").Trim());
            var dieVal = int.Parse(parts[1].Replace("Die: ", "").Trim());

            Assert.InRange(rollVal, 1, 100);
            Assert.InRange(dieVal, 1, 6);
        }

        [Fact]
        public void ReplaceVariables_EvaluatesMathExpressions()
        {
            var variables = new ConcurrentDictionary<string, string>();
            variables["points"] = "20";

            var result = VariableReplacer.ReplaceVariables("Result: $math(%points% + 5)$", variables);

            Assert.Equal("Result: 25", result);
        }

        [Fact]
        public void ReplaceVariables_EmptyUserInDictionary_FallsBackToName()
        {
            var variables = new ConcurrentDictionary<string, string>();
            variables["User"] = "";
            variables["Name"] = "FallbackViewer";

            var result = VariableReplacer.ReplaceVariables("User: %user%, Name: %name%", variables);

            Assert.Equal("User: FallbackViewer, Name: FallbackViewer", result);
        }

        [Fact]
        public void ReplaceVariables_EmptyUserAndNoNameInDictionary_ReplacesWithEmptyString()
        {
            var variables = new ConcurrentDictionary<string, string>();
            variables["User"] = "";

            var result = VariableReplacer.ReplaceVariables("User: [%user%]", variables);

            Assert.Equal("User: []", result);
        }

        [Fact]
        public void ReplaceVariables_DollarPrefixedNames_TreatedAsLiteralString()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BotName.Returns("$100Bot");
            backbone.BroadcasterName.Returns("$wagStreamer");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                var result = VariableReplacer.ReplaceVariables("Bot: %bot%, Streamer: %streamer%, User: %user%", variables);

                Assert.Equal("Bot: $100Bot, Streamer: $wagStreamer, User: $wagStreamer", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_SubstitutedValues_NotProcessedByLaterTokenPasses()
        {
            var backbone = Substitute.For<IServiceBackbone>();
            backbone.BotName.Returns("Bot %time% %random%");
            VariableReplacer.ServiceBackbone = backbone;

            try
            {
                var variables = new ConcurrentDictionary<string, string>();
                var result = VariableReplacer.ReplaceVariables("Bot: %bot%", variables);

                Assert.Equal("Bot: Bot %time% %random%", result);
            }
            finally
            {
                VariableReplacer.ServiceBackbone = null;
            }
        }

        [Fact]
        public void ReplaceVariables_RandomRangeWithIntMaxValue_HandlesUpperInclusiveBound()
        {
            var variables = new ConcurrentDictionary<string, string>();

            // Boundary test with min == max == int.MaxValue
            var singleValResult = VariableReplacer.ReplaceVariables("%random(2147483647, 2147483647)%", variables);
            Assert.Equal(int.MaxValue.ToString(), singleValResult);

            // Inclusive range containing int.MaxValue
            var rangeResult = VariableReplacer.ReplaceVariables("%random(2147483646, 2147483647)%", variables);
            var parsedVal = long.Parse(rangeResult);
            Assert.InRange(parsedVal, 2147483646L, (long)int.MaxValue);
        }
    }
}

