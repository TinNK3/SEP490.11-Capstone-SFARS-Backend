using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Middlewares;
using SFARS.Infrastructure;
using SFARS.Application;

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
app.MapControllers();
app.Run();