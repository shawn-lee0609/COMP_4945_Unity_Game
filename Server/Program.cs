using Microsoft.AspNetCore.SignalR;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();
app.UseCors();
app.MapHub<GameHub>("/gamehub");
app.Run();

public class GameHub : Hub
{
    public async Task JoinGame(string playerName)
    {
        string playerId = Context.ConnectionId;
        await Clients.Caller.SendAsync("OnAssignedId", playerId);
        await Clients.Others.SendAsync("OnPlayerJoined", playerId, playerName);
    }

    public async Task StartGame(string gridData) =>
        await Clients.All.SendAsync("OnGameStartReceived", gridData);

    public async Task Move(int x, int y) =>
        await Clients.Others.SendAsync("OnPlayerMoved", Context.ConnectionId, x, y);

    public async Task PlaceBomb(int x, int y) =>
        await Clients.All.SendAsync("OnBombPlacedReceived", Context.ConnectionId, x, y);

    public async Task BombExploded(string bombId, int x, int y, string cells) =>
        await Clients.All.SendAsync("OnBombExplodedReceived", bombId, x, y, cells);

    public async Task PlayerDied(string playerId) =>
        await Clients.All.SendAsync("OnPlayerDiedReceived", playerId);

    public async Task GameOver(string winnerId) =>
        await Clients.All.SendAsync("OnGameOverReceived", winnerId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Clients.Others.SendAsync("OnPlayerLeft", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}