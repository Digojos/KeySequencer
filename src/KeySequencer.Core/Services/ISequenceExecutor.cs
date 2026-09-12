using KeySequencer.Core.Domain;

namespace KeySequencer.Core.Services;

public interface ISequenceExecutor
{
    bool IsRunning { get; }

    Task RunAsync(SequenceConfiguration configuration, CancellationToken cancellationToken = default);
}
