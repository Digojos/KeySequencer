using KeySequencer.Core.Domain;

namespace KeySequencer.Core.Services;

public sealed class SequenceExecutor : ISequenceExecutor
{
    private readonly IInputSender _inputSender;
    private int _running;

    public SequenceExecutor(IInputSender inputSender)
    {
        _inputSender = inputSender;
    }

    public bool IsRunning => _running != 0;

    public async Task RunAsync(SequenceConfiguration configuration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return; // already executing - a second trigger while running is a silent no-op
        }

        try
        {
            var keys = configuration.Keys;
            for (var i = 0; i < keys.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await _inputSender.PressKeyAsync(keys[i]).ConfigureAwait(false);

                if (i < keys.Count - 1)
                {
                    await Task.Delay(configuration.PingMs, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
