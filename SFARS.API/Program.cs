using Hangfire;
using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Middlewares;
using SFARS.Infrastructure;
using SFARS.Infrastructure.Hubs;
using SFARS.Application;
using SFARS.Domain.Common.Constants;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    // Add HttpClient
    .AddHttpClient()
   // Add CORS
   .AddCors(builder.Configuration, "SFARS_CORS")
    // Add Health Checks
    .AddHealthChecks()
    //Add Api Health check
    .AddApiHealthCheck()
    // Add SQL Server health check
    .AddSqlServerHealthCheck();

builder.Services
    // Configure endpoints, swagger, and controllers
    .ConfigureEndpoints()

    // Configure Serilog logging
    .ConfigureSerilog(builder)

    // Configure application settings
    .ConfigureAppSettings(builder, builder.Environment)

    // Configure authentication services
    .ConfigureHealthCheckServices(builder.Configuration);

// Configure infrastructure services
builder.Services
    // Configure application services
    .AddApplication(builder.Configuration)

    // Configure infrastructure services
    .AddInfrastructure(builder.Configuration);

builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddSwaggerFeature();

// Redis: IConnectionMultiplexer (singleton, shared across all services)
var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
    ?? "localhost:6379,abortConnect=false";
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

// In-memory cache for short-lived local caches (e.g., MedicalFacilityService)
builder.Services.AddMemoryCache();

// SignalR with Redis backplane for multi-instance support
builder.Services.AddSignalR()
    .AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix =
            RedisChannel.Literal(LocationConstants.SignalRRedisChannelPrefix);
    });

var app = builder.Build();

//app.UseHealthChecks();

// Initialize database synchronously before app starts serving requests
await app.InitializeDatabaseAsync();

//builder.Services
//    // Add swagger services
//    .AddSwagger();

// Configure swagger settings
if (app.Environment.IsDevelopment())
{
    app.WithSwagger();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("SFARS_CORS");
app.UseAuthentication();
app.UseAuthorization();

// Map SignalR hubs (must be after UseAuthorization)
app.MapHub<LocationTrackingHub>("/hubs/location-tracking");
app.MapHub<RescueDispatchHub>("/hubs/rescue");
app.MapHub<CommunityHub>("/hubs/community");

// Hangfire dashboard (admin access only)
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    // Restrict to localhost in production; swap for JWT-based filter when admin panel is added
    Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
});

app.MapControllers();
app.Run();