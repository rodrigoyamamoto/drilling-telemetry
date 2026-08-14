namespace DrillingTelemetry.DeviceSimulator.Runtime;

/// <summary>
/// Converts the console cancellation signal into a cancellation token.
/// </summary>
internal sealed class ConsoleCancellationHandler : IDisposable
{
    private readonly CancellationTokenSource
        _cancellationTokenSource = new();

    /// <summary>
    /// Initializes the handler and starts listening for Ctrl+C.
    /// </summary>
    public ConsoleCancellationHandler()
    {
        Console.CancelKeyPress += HandleCancelKeyPress;
    }

    /// <summary>
    /// Gets the token cancelled when Ctrl+C is pressed.
    /// </summary>
    public CancellationToken Token =>
        _cancellationTokenSource.Token;

    /// <inheritdoc />
    public void Dispose()
    {
        Console.CancelKeyPress -= HandleCancelKeyPress;
        _cancellationTokenSource.Dispose();
    }

    /// <summary>
    /// Handles the console cancellation signal.
    /// </summary>
    /// <param name="sender">Source of the console event.</param>
    /// <param name="eventArgs">Cancellation event arguments.</param>
    private void HandleCancelKeyPress(
        object? sender,
        ConsoleCancelEventArgs eventArgs)
    {
        eventArgs.Cancel = true;
        _cancellationTokenSource.Cancel();
    }
}
