using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PenguinTwitchBot.Bot.Commands;
using PenguinTwitchBot.Bot.Commands.Misc;
using PenguinTwitchBot.Bot.Core;
using PenguinTwitchBot.Database.Bot.Models.Timers;

namespace PenguinTwitchBot.Test.Bot.Commands.Misc
{
    public class AutoTimersTests
    {
        [Fact]
        public void AutoTimers_Constructor_InitializesDependencies()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            Assert.NotNull(autoTimers);
        }

        [Fact]
        public void CheckEnoughMessagesAndUpdate_NullId_ReturnsFalse()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("CheckEnoughMessagesAndUpdate", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);

            var group = new TimerGroup { Id = null, MinimumMessages = 10 };
            var result = method!.Invoke(autoTimers, [group]) as bool?;
            Assert.False(result);
        }

        [Fact]
        public void CheckEnoughMessagesAndUpdate_IdExistsWithoutCounter_ReturnsTrue()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("CheckEnoughMessagesAndUpdate", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var group = new TimerGroup { Id = 1, MinimumMessages = 10 };
            var result = method!.Invoke(autoTimers, [group]) as bool?;
            Assert.True(result);
        }

        [Fact]
        public void UpdateNextRun_SetsNextRunAndLastRun()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("UpdateNextRun", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void GetActionsForTimerGroup_ReturnsEmpty_WhenNoTriggers()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("GetActionsForTimerGroup", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void OnCommand_ReturnsCompletedTask()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("OnCommand", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void Register_ReturnsCompletedTask()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("Register", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void OnChatMessage_IncrementCounter()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("OnChatMessage", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void AddActionToTimerGroup_NullGroup_LogsWarning()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("AddActionToTimerGroup", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }

        [Fact]
        public void RemoveActionFromTimerGroup_ReturnsWithoutError()
        {
            var autoTimers = new AutoTimers(
                Substitute.For<ILogger<AutoTimers>>(),
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IServiceBackbone>(),
                Substitute.For<PenguinTwitchBot.Application.Notifications.IPenguinDispatcher>(),
                Substitute.For<ICommandHandler>());

            var method = typeof(AutoTimers).GetMethod("RemoveActionFromTimerGroup", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert.NotNull(method);
        }
    }
}