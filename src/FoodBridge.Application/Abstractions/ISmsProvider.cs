namespace FoodBridge.Application.Abstractions;

public interface ISmsProvider
{
    Task SendOtpAsync(string mobile, string code, CancellationToken cancellationToken = default);
}
