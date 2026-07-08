using System.Text.Json;
using System.Text.Json.Serialization;
using Karo.Api.Hubs;
using Karo.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("KaroClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
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
builder.Services.AddSingleton<BoardGenerator>();
builder.Services.AddSingleton<GameService>();
builder.Services.AddSingleton<DebugGameService>();

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
