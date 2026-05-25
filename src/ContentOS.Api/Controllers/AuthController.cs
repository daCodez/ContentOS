using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace ContentOS.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/auth")]
[Produces("application/json")]
[Consumes("application/json")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpPost("token")]
    public IActionResult CreateToken([FromBody] TokenRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        var adminKey = Environment.GetEnvironmentVariable("CONTENTOS_ADMIN_BOOTSTRAP_KEY");
        if (string.IsNullOrWhiteSpace(adminKey) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(request.AdminKey ?? string.Empty),
                Encoding.UTF8.GetBytes(adminKey)))
        {
            return Unauthorized(new { message = "Invalid admin bootstrap key." });
        }

        var issuer = _configuration["Authentication:Jwt:Issuer"] ?? "ContentOS";
        var audience = _configuration["Authentication:Jwt:Audience"] ?? "ContentOS.Clients";
        var signingKey = Environment.GetEnvironmentVariable("CONTENTOS_JWT_SIGNING_KEY")
            ?? _configuration["Authentication:Jwt:SigningKey"]
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "JWT signing key is not configured." });
        }

        var expiresAt = DateTime.UtcNow.AddMinutes(request.ExpiresInMinutes is > 0 and <= 480 ? request.ExpiresInMinutes.Value : 60);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, request.Subject?.Trim() is { Length: > 0 } subject ? subject : "contentos-dev-admin"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Name, request.Subject?.Trim() is { Length: > 0 } named ? named : "contentos-dev-admin"),
            new(ClaimTypes.Role, "Admin")
        };

        if (!string.IsNullOrWhiteSpace(request.Scope))
        {
            claims.Add(new Claim("scope", request.Scope));
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        var serializedToken = new JwtSecurityTokenHandler().WriteToken(token);

        return Ok(new TokenResponse(
            serializedToken,
            expiresAt,
            issuer,
            audience,
            "Bearer"));
    }

    public sealed record TokenRequest(string AdminKey, string? Subject, string? Scope, int? ExpiresInMinutes);
    public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc, string Issuer, string Audience, string TokenType);
}
