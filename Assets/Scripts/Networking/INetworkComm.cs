// Assets/Scripts/Networking/INetworkComm.cs
// NAMESPACE: NetworkAPI
//
// This interface is the KEY to swapping between UDP multicast (LAN) and SignalR (WAN).
// Both MulticastComm and SignalRComm implement this same interface.
// GameController only depends on INetworkComm — it never knows which transport is used.
//
// To switch from multicast → SignalR, you change ONE line in GameController:
//   FROM: INetworkComm _network = new MulticastComm();
//   TO:   INetworkComm _network = new SignalRComm();

using System;
using System.Threading.Tasks;

namespace NetworkAPI
{
    /// <summary>
    /// Message types sent over the network (same protocol for both transports)
    /// </summary>
    public enum MessageType
    {
        Join,           // Player joining the game
        Leave,          // Player leaving
        GameStart,      // Host broadcasting grid data
        PlayerMove,     // Player moved to new cell
        BombPlaced,     // Player placed a bomb
        BombExploded,   // Bomb exploded (with affected cells)
        PlayerDied,     // Player was killed
        GameOver        // Game ended with winner
    }

    /// <summary>
    /// Structured game message — serialized to string for UDP, or sent as typed args for SignalR
    /// All needed information to send to other players either using multicast (LAN) or SignalRComm (WAN)
    /// </summary>
    public class GameMessage
    {
        public MessageType Type { get; set; }
        public string SenderId { get; set; } = "";
        public string Payload { get; set; } = "";   // Semicolon-separated data (like original "ID=1;x,y,z")
                                                    // indicates the actual data (ex. coordinates)
        public int SequenceNum { get; set; } = 0;    // For UDP dedup/loss detection

        /// <summary>
        /// Serialize to string for UDP multicast (same pattern as original "ID=1;x,y,z")
        /// Converts the GameMessage object into a "string" format which includes the information of the user
        /// (ex. `"3|P_PC1_1234|5|3,7"` )
        /// </summary>
        public string Serialize()
        {
            return $"{(int)Type}|{SenderId}|{SequenceNum}|{Payload}";
        }

        /// <summary>
        /// Deserialize from string received via UDP multicast
        /// </summary>
        public static GameMessage Deserialize(string raw)
        {
            string[] parts = raw.TrimEnd('\0').Split('|');
            if (parts.Length < 4) return null;
            return new GameMessage
            {
                Type = (MessageType)int.Parse(parts[0]),
                SenderId = parts[1],
                SequenceNum = int.Parse(parts[2]),
                Payload = parts[3]
            };
        }
    }

    /// <summary>
    /// Network communication interface — implemented by MulticastComm (LAN) and SignalRComm (WAN)
    /// 
    /// By implementing as an interface, in GameController, we can switch between LAN and WAN 
    /// with one line of code using Polymorphism.
    /// </summary>
    public interface INetworkComm
    {
        // ── Identity ──
        // The player's id
        string MyPlayerId { get; }

        // ── Lifecycle ──
        Task ConnectAsync(string playerName); // Connectes to the Network
        Task DisconnectAsync(); // Disconnect from the Network
        bool IsConnected { get; } // Confir, network connectivity

        // ── Sending (called by GameController) ──
        void SendJoin(string playerName);
        void SendStartGame(string gridData);
        void SendMove(int x, int y);
        void SendPlaceBomb(int x, int y);
        void SendBombExploded(string bombId, int x, int y, string destroyedCells);
        void SendPlayerDied(string playerId);
        void SendGameOver(string winnerId);

        // ── Receiving (events raised when messages arrive from other players) ──
        event Action<string, string> OnPlayerJoined;          // senderId, playerName
        event Action<string> OnPlayerLeft;                     // senderId
        event Action<string> OnGameStartReceived;              // gridData (serialized)
        event Action<string, int, int> OnPlayerMoved;          // senderId, x, y
        event Action<string, int, int> OnBombPlacedReceived;   // senderId, x, y
        event Action<string, int, int, string> OnBombExplodedReceived;  // bombId, x, y, cells
        event Action<string> OnPlayerDiedReceived;             // playerId
        event Action<string> OnGameOverReceived;               // winnerId
    }
}
