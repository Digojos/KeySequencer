using Avalonia;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace KeySequencer.UI;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Windows' default timer resolution (~15.6ms) makes short Task.Delay calls (key hold
        // time, delay between sequence steps) imprecise. Requesting 1ms resolution for the app's
        // lifetime keeps sequence timing consistent; must be paired with TimeEndPeriod.
        TimeBeginPeriod(1);

        // A demanding foreground app/game competing for CPU is the most likely reason a sequence
        // occasionally runs noticeably slower than usual (OS scheduling jitter, not a bug in the
        // sequence's own timing). Raising this process above Normal makes the Windows scheduler
        // favor it over other Normal-priority processes when CPU is contended. AboveNormal, not
        // High/Realtime - those can make the whole system feel unresponsive.
        try
        {
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch
        {
            // Best-effort only - some environments (e.g. restricted accounts) deny changing
            // process priority; the app must still run fine at the default priority.
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            TimeEndPeriod(1);
        }
    }

    [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
    private static extern uint TimeBeginPeriod(uint uMilliseconds);

    [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
    private static extern uint TimeEndPeriod(uint uMilliseconds);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
