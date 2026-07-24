using Dapper;
using FoodBridge.Application.Abstractions;
using FoodBridge.Domain.Entities;
using FoodBridge.Infrastructure.Common;

namespace FoodBridge.Infrastructure.Repositories;

public sealed class CertificateRepository : BaseRepository, ICertificateRepository
{
    private const string SelectSql = "SELECT Id, CertificateNumber, DonorId, ListingId, MealsCount, IssuedAtUtc, PdfUrl, CreatedAtUtc, UpdatedAtUtc FROM Certificates";

    public CertificateRepository(IDbConnectionFactory connectionFactory) : base(connectionFactory)
    {
    }

    public async Task<(IReadOnlyList<Certificate> Items, int TotalCount)> GetForDonorAsync(Guid donorId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();

        const string whereSql = " WHERE DonorId = @DonorId";
        var parameters = new { DonorId = donorId, Offset = (page - 1) * pageSize, PageSize = pageSize };

        var totalCount = await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM Certificates" + whereSql, parameters, cancellationToken: cancellationToken));

        var itemsCommand = new CommandDefinition(
            SelectSql + whereSql + " ORDER BY IssuedAtUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            parameters,
            cancellationToken: cancellationToken);
        var items = (await connection.QueryAsync<Certificate>(itemsCommand)).ToList();

        return (items, totalCount);
    }

    public async Task<Certificate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var connection = ConnectionFactory.CreateConnection();
        var command = new CommandDefinition(SelectSql + " WHERE Id = @Id", new { Id = id }, cancellationToken: cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<Certificate>(command);
    }

    public async Task UpdatePdfUrlAsync(Guid id, string pdfUrl, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Certificates SET PdfUrl = @PdfUrl, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @Id;";
        using var connection = ConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(sql, new { Id = id, PdfUrl = pdfUrl }, cancellationToken: cancellationToken));
    }
}
