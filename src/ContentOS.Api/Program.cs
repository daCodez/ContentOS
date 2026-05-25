using System.Text;
using ContentOS.Application;
using ContentOS.Infrastructure;
using ContentOS.Api;
using ContentOS.ServiceDefaults;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddApplicationServices();
var connectionString = builder.Configuration.GetConnectionString("ContentOs")
    ?? throw new InvalidOperationException("Connection string 'ContentOs' not found in configuration.");
builder.Services.AddInfrastructureServices(connectionString);
builder.Services.AddApiServices();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var jwtSection = builder.Configuration.GetSection("Authentication:Jwt");
var issuer = jwtSection["Issuer"] ?? "ContentOS";
var audience = jwtSection["Audience"] ?? "ContentOS.Clients";
var signingKey = Environment.GetEnvironmentVariable("CONTENTOS_JWT_SIGNING_KEY")
    ?? jwtSection["SigningKey"]
    ?? string.Empty;

if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
{
    throw new InvalidOperationException("CONTENTOS_JWT_SIGNING_KEY must be configured with a strong secret of at least 32 characters.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Ensure database schema exists (migrations) without seeding sample data
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ContentOS.Infrastructure.ContentOsDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    if (dbContext.Database.GetPendingMigrations().Any())
    {
        logger.LogInformation("Applying pending migrations...");
        await dbContext.Database.MigrateAsync();
    }
    else
    {
        await dbContext.Database.EnsureCreatedAsync();
    }

    // Seed workflow templates on startup if missing
    await ContentOS.Infrastructure.WorkflowTemplateSeeder.SeedAsync(dbContext);
    
    var dbPath = dbContext.Database.GetConnectionString() ?? "(unknown)";
    logger.LogInformation("[ContentOS] Database initialized. Resolved DB path: {DbPath}", dbPath);
}

app.UseForwardedHeaders();

app.MapGet("/ping", () => "pong").AllowAnonymous();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "ContentOS API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();

app.MapDefaultEndpoints();

app.Run();
