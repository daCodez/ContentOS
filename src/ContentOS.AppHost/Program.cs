SetIfMissing("ASPNETCORE_URLS", "http://localhost:17056");
SetIfMissing("ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL", "http://localhost:4317");
SetIfMissing("ASPIRE_DASHBOARD_OTLP_HTTP_ENDPOINT_URL", "http://localhost:4318");
SetIfMissing("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

var builder = DistributedApplication.CreateBuilder(args);

var jwtSigningKey = Environment.GetEnvironmentVariable("CONTENTOS_JWT_SIGNING_KEY")
 ?? "dev-contentos-signing-key-please-change-this-1234567890";

var adminBootstrapKey = Environment.GetEnvironmentVariable("CONTENTOS_ADMIN_BOOTSTRAP_KEY")
 ?? "dev-contentos-bootstrap-key-please-change-this-1234567890";

var api = builder.AddProject<Projects.ContentOS_Api>("contentos-api")
 .WithEnvironment("CONTENTOS_JWT_SIGNING_KEY", jwtSigningKey)
 .WithEnvironment("CONTENTOS_ADMIN_BOOTSTRAP_KEY", adminBootstrapKey)
 .WithEndpoint("https", e =>
 {
 e.Port = 5004;
 })
 .WithExternalHttpEndpoints()
 .WithUrlForEndpoint("https", url =>
 {
     url.Url = "https://srv1377835.tailb59890.ts.net:8444/swagger";
     url.DisplayText = "swagger";
 })
 .WithUrl("swagger", "https://srv1377835.tailb59890.ts.net:8444/swagger");

builder.AddProject<Projects.ContentOS_Web>("contentos-web")
 .WithReference(api)
 .WithEndpoint("http", e =>
 {
 e.Port = 5200;
 })
 .WithExternalHttpEndpoints()
 .WithUrlForEndpoint("http", url =>
 {
     url.Url = "https://srv1377835.tailb59890.ts.net:8445";
     url.DisplayText = "public";
 })
 .WithUrl("public", "https://srv1377835.tailb59890.ts.net:8445");

var app = builder.Build();
await app.RunAsync();

static void SetIfMissing(string key, string value)
{
    if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
    {
        Environment.SetEnvironmentVariable(key, value);
    }
}
