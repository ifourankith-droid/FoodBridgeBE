using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

/// <summary>
/// Liveness endpoint for uptime checks and load balancer probes.
/// </summary>
[Route("api/health")]
public sealed class HealthController : BaseController
{
    /// <summary>
    /// Returns a simple healthy status.
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<string>> Get()
    {
        return Ok(ApiResponse<string>.Ok("Healthy", traceId: TraceId));
    }
}
