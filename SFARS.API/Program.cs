using SFARS.API.Extension;
using SFARS.API.Extensions;
using SFARS.API.Middlewares;
using SFARS.Infrastructure;
using SFARS.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks()
    .AddSqlServerHealthCheck();

// Configure application services
builder.Services
    .ConfigureServices(builder.Configuration)
    .ConfigureSerilog(builder)
    .ConfigureAppSettings(builder.Configuration, builder.Environment)
    .ConfigureHealthCheckServices(builder.Configuration);

// Configure infrastructure services
builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

var app = builder.Build();

//app.UseHealthChecks();

// Register database initializer
app.Lifetime.ApplicationStarted.Register(() => Task.Run(async () =>
{
    await app.InitializeDatabaseAsync();
}));

// Configure swagger settings
if (app.Environment.IsDevelopment())
{
    app.WithSwagger();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();