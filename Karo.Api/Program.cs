using System.Text.Json;
using System.Text.Json.Serialization;
using Karo.Api.Hubs;
using Karo.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var allowedCorsOrigins = builder.Configuration
    .GetSection("Karo:Cors:AllowedOrigins")
    .Get<string[]>()
    ?? ["http://localhost:5173", "http://127.0.0.1:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("KaroClient", policy =>
    {
        policy
            .WithOrigins(allowedCorsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddSingleton<LobbyService>();
var lifecycleOptions = builder.Configuration.GetSection(RoomLifecycleOptions.SectionName).Get<RoomLifecycleOptions>() ?? new RoomLifecycleOptions();
builder.Services.AddSingleton(lifecycleOptions);
builder.Services.AddSingleton<BoardIntegrityValidator>();
builder.Services.AddSingleton<BoardGenerator>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<RoomLifecycleService>();
builder.Services.AddSingleton<DebugGameService>();
builder.Services.AddHostedService<RoomCleanupHostedService>();

var app = builder.Build();

app.UseCors("KaroClient");

app.MapGet("/", () => Results.Ok(new
{
    name = "Karo.Api",
    status = "ready"
}));
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTimeOffset.UtcNow
}));
app.MapHub<GameLobbyHub>("/hubs/lobby");

app.Run();
