using Serilog;
using IhsanAI.Api.Middleware;
using IhsanAI.Api.Extensions;
using IhsanAI.Api.Endpoints;
using IhsanAI.Application;
using IhsanAI.Infrastructure;
using DotNetEnv;

// Load .env file from application base directory
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
Console.WriteLine($"[ENV] Looking for .env at: {envPath}");
Console.WriteLine($"[ENV] File exists: {File.Exists(envPath)}");

if (File.Exists(envPath))
{
    Env.Load(envPath);
    Console.WriteLine("[ENV] Loaded from AppContext.BaseDirectory");
}
else
{
    // Try project directory
    var projectEnvPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
    Console.WriteLine($"[ENV] Trying project directory: {projectEnvPath}");
    Console.WriteLine($"[ENV] File exists: {File.Exists(projectEnvPath)}");

    if (File.Exists(projectEnvPath))
    {
        Env.Load(projectEnvPath);
        Console.WriteLine("[ENV] Loaded from CurrentDirectory");
    }
}

// Debug: Check if env vars loaded
var clientId = Environment.GetEnvironmentVariable("GoogleDrive__ClientId");
Console.WriteLine($"[ENV] GoogleDrive__ClientId loaded: {!string.IsNullOrEmpty(clientId)}");

var builder = WebApplication.CreateBuilder(args);

// Kestrel - Büyük dosya yüklemeleri için limit (50MB)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024; // 50MB
});

// Add environment variables to configuration
builder.Configuration.AddEnvironmentVariables();

// Create logs directory with absolute path (IIS fix)
var logsDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logsDirectory);
var logPath = Path.Combine(logsDirectory, "ihsanai-.log");

// Enable Serilog self-logging for debugging
Serilog.Debugging.SelfLog.Enable(msg => File.AppendAllText(
    Path.Combine(AppContext.BaseDirectory, "serilog-errors.txt"), msg + Environment.NewLine));

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(logPath, rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IhsanAI API V1");
        c.RoutePrefix = "swagger";
    });
}

// Handle OPTIONS preflight requests FIRST (before any middleware)
app.Use(async (context, next) =>
{
    if (context.Request.Method == "OPTIONS")
    {
        context.Response.StatusCode = 204;
        context.Response.Headers.Append("Access-Control-Allow-Origin", context.Request.Headers["Origin"].ToString());
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
        context.Response.Headers.Append("Access-Control-Allow-Credentials", "true");
        context.Response.Headers.Append("Access-Control-Max-Age", "86400");
        return;
    }
    await next();
});

// CORS must be FIRST
app.UseCors("AllowAll");

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map endpoints
app.MapGet("/", () => Results.Ok(new { Message = "IhsanAI API is running", Version = "1.0.0" }));
app.MapGet("/health", () => Results.Ok(new { Status = "Healthy", Timestamp = DateTime.UtcNow }));

// Map API endpoints
app.MapApiEndpoints();

try
{
    Log.Information("Starting IhsanAI API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
