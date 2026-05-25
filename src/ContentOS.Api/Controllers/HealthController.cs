using System;
using System.Threading;
using System.Threading.Tasks;
using ContentOS.Api.Responses;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace ContentOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IMediator _mediator;

    public HealthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET: api/health
    [HttpGet]
    public async Task<ActionResult<EmptyApiResult>> GetHealth()
    {
        return Ok(EmptyApiResult.Successful("Healthy"));
    }

    // GET: api/health/ready
    [HttpGet("ready")]
    public async Task<ActionResult<EmptyApiResult>> GetReadiness()
    {
        // In a real app, check database, dependencies, etc.
        return Ok(EmptyApiResult.Successful("Ready"));
    }

    // GET: api/health/live
    [HttpGet("live")]
    public async Task<ActionResult<EmptyApiResult>> GetLiveness()
    {
        return Ok(EmptyApiResult.Successful("Alive"));
    }
}