using System.Runtime.CompilerServices;
using Bunit;

namespace PenguinTwitchBot.Test;

internal static class TestAssemblySetup
{
    // Parallel bUnit tests block threads in WaitFor*/Click, starving the renderer dispatcher on small CI runners.
    [ModuleInitializer]
    internal static void Initialize()
    {
        ThreadPool.GetMinThreads(out var workers, out var io);
        ThreadPool.SetMinThreads(Math.Max(workers, Environment.ProcessorCount * 4), io);
        BunitContext.DefaultWaitTimeout = TimeSpan.FromSeconds(15);
    }
}
