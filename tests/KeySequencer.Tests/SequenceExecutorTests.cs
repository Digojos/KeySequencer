using KeySequencer.Core.Domain;
using KeySequencer.Core.Services;

namespace KeySequencer.Tests;

public class SequenceExecutorTests
{
    private sealed class RecordingInputSender : IInputSender
    {
        public List<string> PressedKeys { get; } = [];

        public Task PressKeyAsync(string key)
        {
            PressedKeys.Add(key);
            return Task.CompletedTask;
        }
    }

    private sealed class GatedInputSender : IInputSender
    {
        private readonly TaskCompletionSource _gate = new();
        public int CallCount { get; private set; }

        public async Task PressKeyAsync(string key)
        {
            CallCount++;
            await _gate.Task;
        }

        public void Release() => _gate.TrySetResult();
    }

    [Fact]
    public async Task RunAsync_PressesKeysInConfiguredOrder()
    {
        var sender = new RecordingInputSender();
        var executor = new SequenceExecutor(sender);
        var config = new SequenceConfiguration(["F1", "F3", "F2"], new HotkeyConfig("SPACE"), PingMs: 0);

        await executor.RunAsync(config);

        Assert.Equal(["F1", "F3", "F2"], sender.PressedKeys);
    }

    [Fact]
    public async Task RunAsync_WhileAlreadyRunning_SecondCallIsNoOp()
    {
        var sender = new GatedInputSender();
        var executor = new SequenceExecutor(sender);
        var config = new SequenceConfiguration(["F1", "F2"], new HotkeyConfig("SPACE"), PingMs: 0);

        var firstRun = executor.RunAsync(config);
        await executor.RunAsync(config); // reentrant call while the first is still blocked - must return immediately

        Assert.True(executor.IsRunning);
        Assert.Equal(1, sender.CallCount); // only the first run's first step has fired so far

        sender.Release();
        await firstRun;

        Assert.False(executor.IsRunning);
    }
}
