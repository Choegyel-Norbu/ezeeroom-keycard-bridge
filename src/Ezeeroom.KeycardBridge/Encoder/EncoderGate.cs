namespace Ezeeroom.KeycardBridge.Encoder;

/// <summary>
/// Serializes all encoder access (guide §1.2: "two tabs / two requests must queue,
/// never interleave DLL calls"). One physical device — one queue.
/// GET /v1/status deliberately bypasses this gate via IEncoder.GetStatus().
/// </summary>
public sealed class EncoderGate
{
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public async Task<T> RunAsync<T>(Func<T> operation, CancellationToken ct = default)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            // DLL calls are blocking; keep them off the request thread pool path.
            return await Task.Run(operation, ct);
        }
        finally
        {
            _mutex.Release();
        }
    }
}
