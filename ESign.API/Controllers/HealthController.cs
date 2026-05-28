using Microsoft.AspNetCore.Mvc;
using ESign.API.Application.Services.Interfaces;
using ESign.API.Infrastructure.Logging;

namespace ESign.API.Controllers;


[ApiController]
[Route("api/v1/esign/health")]
public class HealthController : ControllerBase
{
    private readonly IHealthService _healthService;

    public HealthController(IHealthService healthService)
    {
        _healthService = healthService;
    }

 
    [HttpGet]
    public async Task<IActionResult> Health()
    {
        SafeLogger.App("[HEALTH CONTROLLER] GET /health");
        var result = await _healthService.GetHealthAsync();
        return Ok(result);
    }


    [HttpGet("database")]
    public async Task<IActionResult> DatabaseHealth()
    {
        SafeLogger.App("[HEALTH CONTROLLER] GET /health/database");
        var result = await _healthService.GetHealthReadyAsync();
        var status = result.GetType().GetProperty("status")?.GetValue(result)?.ToString();
        return status == "Healthy" ? Ok(result) : StatusCode(503, result);
    }
}