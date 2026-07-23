using System.Collections.Concurrent;
using FoodBridge.Application.Abstractions;

namespace FoodBridge.Infrastructure.Auth;

public sealed class InMemoryTokenDenylist : ITokenDenylist
{
    private readonly ConcurrentDictionary<string, DateTime> _denylist = new();

    public void Add(string jti, DateTime expiresAtUtc)
    {
        _denylist[jti] = expiresAtUtc;
    }

    public bool IsDenylisted(string jti)
    {
        if (!_denylist.TryGetValue(jti, out var expiresAtUtc))
        {
            return false;
        }

        if (expiresAtUtc <= DateTime.UtcNow)
        {
            _denylist.TryRemove(jti, out _);
            return false;
        }

        return true;
    }
}
