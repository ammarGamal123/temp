using POS.PrintAgent.Core.Interfaces;
using POS.PrintAgent.Core.Models;
using POS.PrintAgent.Service.Endpoints;
using POS.PrintAgent.Service.Middleware;
using POS.PrintAgent.Service.Services;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 1. Windows Service support
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "POS Print Agent";
});

// 2. Serilog configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(AppContext.BaseDirectory, "logs", "agent-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14)
    .CreateLogger();

builder.Host.UseSerilog();

// 3. Load settings
var agentSettings = builder.Configuration
    .GetSection("AgentSettings")
    .Get<AgentSettings>() ?? new AgentSettings();

builder.Services.AddSingleton(agentSettings);

// 4. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PosClient", policy =>
    {
        if (agentSettings.AllowedOrigins.Any())
        {
            policy.WithOrigins(agentSettings.AllowedOrigins.ToArray())
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => true)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        }
    });
});

// 5. Services registration
#pragma warning disable CA1416
builder.Services.AddSingleton<IHtmlReceiptService, HtmlReceiptService>();
builder.Services.AddTransient<IReceiptBuilder, ThermalReceiptBuilder>();
builder.Services.AddTransient<IPrinterService, WindowsPrinterService>();
#pragma warning restore CA1416

// 5b. OpenAPI + Scalar (dev docs) — Swagger + Scalar
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "POS Print Agent API",
        Version = "v1.3.0",
        Description = "Local printing agent for POS — HTML/CSS receipt rendering via Chrome/PuppeteerSharp, ESC/POS fallback, ZATCA QR, cash drawer. Base URL: http://localhost:5050 | Auth: X-Api-Key header (except /health, /swagger, /scalar)"
    });
    // Include XML comments if present
});

// 6. Check port BEFORE binding
static bool IsPortAvailable(int port)
{
    try
    {
        using var listener = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback, port);
        listener.Start();
        listener.Stop();
        return true;
    }
    catch { return false; }
}

if (!IsPortAvailable(agentSettings.Port))
{
    Log.Fatal("Port {Port} is already in use! Change port in appsettings.json",
        agentSettings.Port);
    Log.CloseAndFlush();
    return;
}

// 7. Configure URL
//builder.WebHost.UseUrls(
//    $"http://127.0.0.1:{agentSettings.Port}",
//    $"http://localhost:{agentSettings.Port}");
builder.WebHost.UseUrls($"http://localhost:{agentSettings.Port}");

var app = builder.Build();

// 8. Middleware pipeline
app.UseCors("PosClient");
app.UseMiddleware<ApiKeyMiddleware>();

// 8b. Swagger + Scalar endpoints (exposed even with ApiKey middleware — scalar is browser UI)
// Note: ApiKeyMiddleware allows /health, /swagger, /scalar without key for local dev
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "POS Print Agent v1.3.0");
    options.RoutePrefix = "swagger"; // -> /swagger
});
app.MapScalarApiReference(options =>
{
    options.Title = "POS Print Agent — Scalar";
    options.Theme = ScalarTheme.Mars;
    options.DefaultHttpClient = new(ScalarTarget.CSharp, ScalarClient.HttpClient);
    options.OpenApiRoutePattern = "/swagger/v1/swagger.json";
});

 // 9. Endpoints
#pragma warning disable CA1416
app.MapPrintEndpoints();
#pragma warning restore CA1416

Log.Information("POS Print Agent starting on port {Port}", agentSettings.Port);
Log.Information("Allowed origins: {Origins}",
    agentSettings.AllowedOrigins.Any()
        ? string.Join(", ", agentSettings.AllowedOrigins)
        : "ANY (not recommended for production)");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "POS Print Agent terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}