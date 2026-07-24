using FoodBridge.Application.Reports.Dtos;

namespace FoodBridge.Application.Abstractions;

/// <summary>
/// Read-only cross-aggregate analytics queries (Listings/Certificates/VolunteerPoints) —
/// deliberately not folded into IListingRepository/ICertificateRepository, since reporting
/// is a distinct concern from either aggregate's own CRUD.
/// </summary>
public interface IReportsReader
{
    Task<(int TotalListings, int TotalMealsDonated, int TotalCertificates)> GetDonorSummaryAsync(Guid donorId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChartPoint>> GetDonorMealsByMonthAsync(Guid donorId, CancellationToken cancellationToken = default);

    Task<(int TotalDeliveries, int TotalPoints)> GetVolunteerSummaryAsync(Guid volunteerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChartPoint>> GetVolunteerDeliveriesByMonthAsync(Guid volunteerId, CancellationToken cancellationToken = default);

    Task<(int TotalMealsReceived, int TotalDeliveriesReceived)> GetRecipientSummaryAsync(Guid recipientId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChartPoint>> GetRecipientMealsByMonthAsync(Guid recipientId, CancellationToken cancellationToken = default);
}
