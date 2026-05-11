using System.Globalization;
using PlayMe.Api.DependencyInjection;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog (CLAUDE.md §4.3): structured logging + 7-day rolling file sink.
builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .WriteTo.File(
        path: "logs/playme-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: CultureInfo.InvariantCulture));

builder.Services
    .AddDomain()
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddApi(builder.Configuration);

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();
app.MapControllers();
app.MapHub<PlayMe.Api.Hubs.RoomHub>("/hubs/room");

await app.RunAsync();

/// <summary>Exposed for WebApplicationFactory in integration tests.</summary>
public partial class Program;
