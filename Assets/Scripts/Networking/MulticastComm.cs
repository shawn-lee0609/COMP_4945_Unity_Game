// Assets/Scripts/Networking/MulticastComm.cs
// NAMESPACE: NetworkAPI
//
// UDP Multicast implementation — for LAN play
//
// Original:  sendMessage("ID=1;" + x + "," + y + "," + z)  →  SendTo(multicastEP)
// New:       SendMove(x, y)  →  serialize GameMessage  →  SendTo(multicastEP)
//
// Key improvements over original:
//   1. Structured messages with MessageType enum (not raw string parsing)
//   2. Sequence numbers to detect lost/duplicate messages (UDP is unreliable)
//   3. Implements INetworkComm so it can be swapped with SignalRComm

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace NetworkAPI
{
    // Implements INetworkComm
    public class MulticastComm : INetworkComm
    {
        // ── Multicast group config (same as original) ──
        private const string MULTICAST_ADDR = "230.0.0.1";
        private const int MULTICAST_PORT = 11000;

        // ── Sockets (same pattern as original NetworkComm.cs) ──
        private Socket _sendSocket;
        private Socket _receiveSocket;
        private IPEndPoint _multicastEP;
        private Thread _receiveThread;
        private bool _running = false;

        // ── Player identity ──
        private string _myPlayerId;
        public string MyPlayerId => _myPlayerId;
        private string _myPlayerName;

        // ── Sequence number tracking for UDP reliability ──
        private int _sequenceNum = 0;
        private Dictionary<string, int> _lastSeenSeq = new();  // per-sender dedup

        // ── Connection state ──
        public bool IsConnected => _running;

        // ── Events Receiving (events raised when messages arrive from other players) (From INetworkComm) ──
        public event Action<string, string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<string> OnGameStartReceived;
        public event Action<string, int, int> OnPlayerMoved;
        public event Action<string, int, int> OnBombPlacedReceived;
        public event Action<string, int, int, string> OnBombExplodedReceived;
        public event Action<string> OnPlayerDiedReceived;
        public event Action<string> OnGameOverReceived;

        /// <summary>
        /// Connect to the multicast group.
        /// SignalCommR needs to wait until it connects to the Server, we use Task
        /// therefore, even if Multicast can directly get connected since both multicast and SignalCommR
        /// inherits from the same interface, we use Task.
        /// </summary>
        public Task ConnectAsync(string playerName)
        {
            // Generate a unique player ID (original used hardcoded "ID=1", "ID=2")
            _myPlayerId = $"P_{Guid.NewGuid().ToString().Substring(0, 8)}";
            _myPlayerName = playerName ;

            // Configure the destination address of the multicast data using the pre-config address & Port number
            _multicastEP = new IPEndPoint(IPAddress.Parse(MULTICAST_ADDR), MULTICAST_PORT);

            // ── Send socket (same as original sendMessage) ──
            // Creating a dedicated Socket
            // (AddressFamily.InterNetwork: IPv4 -> InterNetworkV6 means IPv6)
            _sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // ── Receive socket (same as original ReceiveMessages) ──
            _receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _receiveSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, 1);

            // The receiving point from all the other multicast data
            // IPAddress.Any: means that it will receive from any of the network interface of its device 
            // which sends to the MULTICAST_Port
            // So any of its Network Interface is bind with the specified port number
            // and now listening from an incoming data
            EndPoint localEP = new IPEndPoint(IPAddress.Any, MULTICAST_PORT);
            _receiveSocket.Bind(localEP);

            // Join multicast group (same as original)
            // IPAddress.Parse(MULTICAST_ADDR) -> The address where the Multicast data will be sent (Virtual Group)
            // IPAddress.Any -> Any physical IP address of its device
            MulticastOption mcastOption = new MulticastOption(
                IPAddress.Parse(MULTICAST_ADDR), IPAddress.Any);
            _receiveSocket.SetSocketOption(
                SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);

            // Start receive thread (same pattern as original: new Thread(ReceiveMessages).Start())
            _running = true;
            _receiveThread = new Thread(ReceiveLoop);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();

            Debug.Log($"[MulticastComm] Joined multicast group {MULTICAST_ADDR}:{MULTICAST_PORT}");

            // Announce ourselves to the group
            SendJoin(playerName);

            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _running = false;
            try
            {
                SendMessage(new GameMessage
                {
                    Type = MessageType.Leave,
                    SenderId = _myPlayerId,
                    SequenceNum = _sequenceNum++
                });
            }
            catch { }

            _sendSocket?.Close();
            _receiveSocket?.Close();
            return Task.CompletedTask;
        }

        // SEND METHODS
        // Original: sendMessage("ID=1;" + x + "," + y + "," + z)
        // New:      structured GameMessage → Serialize() → SendTo(multicastEP)
        private void SendMessage(GameMessage msg)
        {
            try
            {
                byte[] data = Encoding.ASCII.GetBytes(msg.Serialize());
                _sendSocket.SendTo(data, _multicastEP);
            }
            catch (Exception e)
            {
                Debug.LogError($"[MulticastComm] Send error: {e.Message}");
            }
        }

        public void SendJoin(string playerName)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.Join,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = playerName
            });
        }

        public void SendStartGame(string gridData)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.GameStart,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = gridData
            });
        }

        public void SendMove(int x, int y)
        {
            // Original: sendMessage("ID=1;" + x + "," + y + "," + z)
            // New:      structured with MessageType
            SendMessage(new GameMessage
            {
                Type = MessageType.PlayerMove,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = $"{x},{y}"
            });
        }

        public void SendPlaceBomb(int x, int y)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.BombPlaced,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = $"{x},{y}"
            });
        }

        public void SendBombExploded(string bombId, int x, int y, string destroyedCells)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.BombExploded,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = $"{bombId};{x},{y};{destroyedCells}"
            });
        }

        public void SendPlayerDied(string playerId)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.PlayerDied,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = playerId
            });
        }

        public void SendGameOver(string winnerId)
        {
            SendMessage(new GameMessage
            {
                Type = MessageType.GameOver,
                SenderId = _myPlayerId,
                SequenceNum = _sequenceNum++,
                Payload = winnerId
            });
        }

        // RECEIVE LOOP
        // Original: while(true) { ReceiveFrom → parse → MsgReceived(message) }
        // New:      while(true) { ReceiveFrom → Deserialize → filter self → dedup → dispatch event }
        private void ReceiveLoop()
        {
            byte[] buffer = new byte[4096];
            EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

            while (_running)
            {
                try
                {
                    int len = _receiveSocket.ReceiveFrom(buffer, ref remoteEP);
                    string raw = Encoding.ASCII.GetString(buffer, 0, len);

                    // Deserialize the string data sent from the other players and revert as a GameMessage Object
                    GameMessage msg = GameMessage.Deserialize(raw);
                    if (msg == null) continue;

                    // ── Filter out own messages (original: if (!msgParts[0].Contains("ID=1"))) ──
                    if (msg.SenderId == _myPlayerId) continue;

                    // ── Duplicate detection (accounting for unreliable UDP) ──
                    if (_lastSeenSeq.TryGetValue(msg.SenderId, out int lastSeq))
                    {
                        if (msg.SequenceNum <= lastSeq) continue;  // Already processed
                    }
                    _lastSeenSeq[msg.SenderId] = msg.SequenceNum;

                    // ── Dispatch to appropriate event based on MessageType ──
                    DispatchMessage(msg);
                }
                catch (SocketException) { if (!_running) break; }
                catch (Exception e) { Debug.LogError($"[MulticastComm] Receive error: {e.Message}"); }
            }
        }

        /// <summary>
        /// Route incoming message to the correct event handler.
        /// Original: processMsg parsed "ID=1;x,y,z" manually
        /// New:      typed MessageType dispatches to specific events
        /// </summary>
        private void DispatchMessage(GameMessage msg)
        {
            switch (msg.Type)
            {
                case MessageType.Join:
                    OnPlayerJoined?.Invoke(msg.SenderId, msg.Payload);
                    // Reply with our own join so the new player knows we exist
                    // Only reply to real joins (with a name), not to replies (empty payload)
                    if (!string.IsNullOrEmpty(msg.Payload))
                    {
                        SendMessage(new GameMessage
                        {
                            Type = MessageType.JoinReply,       
                            SenderId = _myPlayerId,
                            SequenceNum = _sequenceNum++,
                            Payload = _myPlayerName             
                        });
                    }
                    break;

                case MessageType.JoinReply:                     
                    OnPlayerJoined?.Invoke(msg.SenderId, msg.Payload);
                    
                    break;

                case MessageType.Leave:
                    OnPlayerLeft?.Invoke(msg.SenderId);
                    break;

                case MessageType.GameStart:
                    OnGameStartReceived?.Invoke(msg.Payload);
                    break;

                case MessageType.PlayerMove:
                    string[] moveCoords = msg.Payload.Split(',');
                    int mx = int.Parse(moveCoords[0]);
                    int my = int.Parse(moveCoords[1]);
                    OnPlayerMoved?.Invoke(msg.SenderId, mx, my);
                    break;

                case MessageType.BombPlaced:
                    string[] bombCoords = msg.Payload.Split(',');
                    int bx = int.Parse(bombCoords[0]);
                    int by = int.Parse(bombCoords[1]);
                    OnBombPlacedReceived?.Invoke(msg.SenderId, bx, by);
                    break;

                case MessageType.BombExploded:
                    // Payload: "bombId;x,y;cell1x,cell1y:cell2x,cell2y:..."
                    string[] explParts = msg.Payload.Split(';');
                    string eBombId = explParts[0];
                    string[] eCoords = explParts[1].Split(',');
                    int ex = int.Parse(eCoords[0]);
                    int ey = int.Parse(eCoords[1]);
                    string cells = explParts.Length > 2 ? explParts[2] : "";
                    OnBombExplodedReceived?.Invoke(eBombId, ex, ey, cells);
                    break;

                case MessageType.PlayerDied:
                    OnPlayerDiedReceived?.Invoke(msg.Payload);
                    break;

                case MessageType.GameOver:
                    OnGameOverReceived?.Invoke(msg.Payload);
                    break;
            }
        }
    }
}
