using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Liveness endpoint for uptime checks and load balancer probes.
/// </summary>
[Route("api/health")]
[ProducesResponseType(StatusCodes.Status500InternalServerError)]
public sealed class HealthController : BaseController
{
    /// <summary>
    /// Returns a simple healthy status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<string>> Get()
    {
        return Ok(ApiResponse<string>.Ok("Healthy", traceId: TraceId));
    }
}
