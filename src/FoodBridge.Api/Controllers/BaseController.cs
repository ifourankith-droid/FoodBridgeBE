using FoodBridge.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FoodBridge.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected string TraceId => HttpContext.TraceIdentifier;

    protected ActionResult<ApiResponse<T>> HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<T>.Ok(result.Data!, result.Message, TraceId));
        }

        return UnprocessableEntity(ApiResponse<T>.Fail(result.Message, result.Errors, TraceId));
    }

    protected ActionResult<ApiResponse<object?>> HandleResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok(ApiResponse<object?>.Ok(null, result.Message, TraceId));
        }

        return UnprocessableEntity(ApiResponse<object?>.Fail(result.Message, result.Errors, TraceId));
    }

    protected ActionResult<PagedResponse<T>> HandlePagedResult<T>(Result<PagedResult<T>> result)
    {
        if (result.IsSuccess)
        {
            var data = result.Data!;
            return Ok(PagedResponse<T>.Create(data.Items, data.Page, data.PageSize, data.TotalCount, result.Message, TraceId));
        }

        return UnprocessableEntity(PagedResponse<T>.Fail(result.Message, result.Errors, TraceId));
    }
}
