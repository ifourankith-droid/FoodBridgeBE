using FoodBridge.Application.Common;
using FoodBridge.Application.Geocoding;
using FoodBridge.Application.Geocoding.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Public utility endpoint — no auth required, since it's also useful before
/// registration completes (resolving an address to coordinates for the registration form).
/// </summary>
[Route("api/geocode")]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class GeocodeController : BaseController
{
    private readonly IGeocodingService _geocodingService;

    public GeocodeController(IGeocodingService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GeocodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GeocodeResponse>>> Geocode([FromQuery] string address, CancellationToken cancellationToken)
    {
        var result = await _geocodingService.GeocodeAsync(address, cancellationToken);
        return HandleResult(result);
    }
}
