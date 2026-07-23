namespace FoodBridge.Application.Abstractions;

/// <summary>
/// In-memory denylist of revoked JWT ids (jti), used to make logout effective
/// despite JWTs otherwise being stateless. Tradeoff: does not survive a restart
/// and does not scale across instances — acceptable for this project's scope.
/// </summary>
public interface ITokenDenylist
{
    void Add(string jti, DateTime expiresAtUtc);

    bool IsDenylisted(string jti);
}
