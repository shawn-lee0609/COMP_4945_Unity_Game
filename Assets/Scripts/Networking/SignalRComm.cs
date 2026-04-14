// Assets/Scripts/Networking/SignalRComm.cs
// NAMESPACE: NetworkAPI
//
// SignalR/WebSocket implementation — for WAN (internet) play
// Implements the SAME INetworkComm interface as MulticastComm
//
// To switch from LAN → WAN, change ONE line in GameController:
//   FROM: INetworkComm _network = new MulticastComm();
//   TO:   INetworkComm _network = new SignalRComm();
//
// The SignalR hub acts as the multicast group:
//   UDP multicast:  SendTo(multicastGroup)  →  all group members receive
//   SignalR hub:    InvokeAsync("Method")   →  hub calls Clients.All.SendAsync()

// The below line of code is for Phase 2 (WebSocket)
#if ENABLE_SIGNALR 

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using UnityEngine;

namespace NetworkAPI
{
    public class SignalRComm : INetworkComm
    {
        // ── Change this URL to your deployed server ──
        private const string SERVER_URL = "https://bomberman-slee.canadacentral.cloudapp.azure.com/gamehub";

        private HubConnection _connection;
        private string _myPlayerId = "";
        public string MyPlayerId => _myPlayerId;
        public bool IsConnected => _connection?.State == HubConnectionState.Connected;

        private bool _isHostFromServer = false;
        public bool IsHost => _isHostFromServer;

        // ── Events (same as MulticastComm — that's the whole point) ──
        public event Action<string, string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<string> OnGameStartReceived;
        public event Action<string, int, int> OnPlayerMoved;
        public event Action<string, int, int> OnBombPlacedReceived;
        public event Action<string, int, int, string> OnBombExplodedReceived;
        public event Action<string> OnPlayerDiedReceived;
        public event Action<string> OnGameOverReceived;

        /// <summary>
        /// Connect to SignalR hub and join the game.
        /// Replaces: joining multicast group + SendJoin
        /// </summary>
        public async Task ConnectAsync(string playerName)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(SERVER_URL)
                .WithAutomaticReconnect()
                .Build();

            // Register handlers (replaces the ReceiveLoop + DispatchMessage in MulticastComm)
            RegisterHandlers();

            try
            {
                await _connection.StartAsync();
                Debug.Log("[SignalRComm] Connected to hub");
                await _connection.InvokeAsync("JoinGame", playerName);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SignalRComm] Connection failed: {e.Message}");
            }
        }

        public async Task DisconnectAsync()
        {
            if (_connection != null)
                await _connection.StopAsync();
        }

        /// <summary>
        /// Register server → client event handlers.
        /// This replaces the while(true) ReceiveFrom loop in MulticastComm.
        /// SignalR handles threading internally — no manual Thread needed.
        /// </summary>
        private void RegisterHandlers()
        {
            _connection.On<string>("OnAssignedId", (id) =>
            {
                _myPlayerId = id;
            });

            _connection.On<bool>("OnHostAssigned", (isHost) =>
{
    _isHostFromServer = isHost;
    Debug.Log($"[SignalRComm] Host assigned: {isHost}");
});

            _connection.On<string, string>("OnPlayerJoined", (id, name) =>
            {
                OnPlayerJoined?.Invoke(id, name);
            });

            _connection.On<string>("OnPlayerLeft", (id) =>
            {
                OnPlayerLeft?.Invoke(id);
            });

            _connection.On<string>("OnGameStart", (gridData) =>
            {
                OnGameStartReceived?.Invoke(gridData);
            });

            _connection.On<string, int, int>("OnPlayerMoved", (id, x, y) =>
            {
                // Filter self (multicast version does this in ReceiveLoop)
                if (id == _myPlayerId) return;
                OnPlayerMoved?.Invoke(id, x, y);
            });

            _connection.On<string, int, int>("OnBombPlaced", (senderId, x, y) =>
            {
                OnBombPlacedReceived?.Invoke(senderId, x, y);
            });

            _connection.On<string, int, int, string>("OnBombExploded", (bombId, x, y, cells) =>
            {
                OnBombExplodedReceived?.Invoke(bombId, x, y, cells);
            });

            _connection.On<string>("OnPlayerDied", (id) =>
            {
                OnPlayerDiedReceived?.Invoke(id);
            });

            _connection.On<string>("OnGameOver", (winnerId) =>
            {
                OnGameOverReceived?.Invoke(winnerId);
            });
        }

        
        // SEND METHODS — same interface as MulticastComm
        // Instead of SendTo(multicastEP), we InvokeAsync on the hub
        // The hub then calls Clients.All.SendAsync (= multicast broadcast)
        

        public void SendJoin(string playerName)
        {
            // Already handled in ConnectAsync for SignalR
        }

        public void SendStartGame(string gridData)
        {
            _ = _connection.InvokeAsync("StartGame", gridData);
        }

        public void SendMove(int x, int y)
        {
            // Multicast: SendTo(multicastEP, "3|P_1|5|x,y")
            // SignalR:   InvokeAsync("PlayerMove", x, y) → hub broadcasts to all
            _ = _connection.InvokeAsync("PlayerMove", x, y);
        }

        public void SendPlaceBomb(int x, int y)
        {
            _ = _connection.InvokeAsync("PlaceBomb", x, y);
        }

        public void SendBombExploded(string bombId, int x, int y, string destroyedCells)
        {
            _ = _connection.InvokeAsync("BombExploded", bombId, x, y, destroyedCells);
        }

        public void SendPlayerDied(string playerId)
        {
            _ = _connection.InvokeAsync("PlayerDied", playerId);
        }

        public void SendGameOver(string winnerId)
        {
            _ = _connection.InvokeAsync("GameOver", winnerId);
        }
    }
}

// The below line of code is for Phase 2 (WebSocket)
#endif   
