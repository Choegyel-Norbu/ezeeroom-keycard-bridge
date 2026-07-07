using Ezeeroom.KeycardBridge.Config;
using Microsoft.Extensions.Options;

namespace Ezeeroom.KeycardBridge.Security;

/// <summary>
/// Sliding-window rate limit on card issuance (guide §1.4: "max N issuances/minute" —
/// the worst-case threat is local malware minting keys, so cap the blast radius).
/// </summary>
public sealed class IssueRateLimiter(IOptions<BridgeOptions> options)
{
    private readonly int _maxPerMinute = options.Value.MaxIssuesPerMinute;
    private readonly Queue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    public bool TryAcquire()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                _timestamps.Dequeue();

            if (_timestamps.Count >= _maxPerMinute) return false;

            _timestamps.Enqueue(DateTime.UtcNow);
            return true;
        }
    }
}
