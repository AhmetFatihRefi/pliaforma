using Microsoft.AspNetCore.Mvc;
using Pliaforma.Application.Common.Cqrs;
using Pliaforma.Application.Common.Models;

namespace Pliaforma.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly IDispatcher Dispatcher;

    protected ApiControllerBase(IDispatcher dispatcher) => Dispatcher = dispatcher;

    /// <summary>İsteği dispatcher'a gönderir ve sonucu ApiResponse zarfıyla döner.</summary>
    protected async Task<IActionResult> Send<T>(IRequest<T> request, string? message = null, CancellationToken ct = default)
    {
        var result = await Dispatcher.Send(request, ct);
        return Ok(ApiResponse<T>.Ok(result, message));
    }
}
