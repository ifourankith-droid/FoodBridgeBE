using System.Data;

namespace FoodBridge.Application.Abstractions;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
