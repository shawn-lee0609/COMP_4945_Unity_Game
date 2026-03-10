// Assets/Scripts/GUI/GameController.cs
// NAMESPACE: GUI
// The ONLY file that uses Unity APIs. Everything else is pure C#.
//
// TO SWITCH FROM LAN (MULTICAST) TO WAN (SIGNALR):             
// Change the _useSignalR flag below, OR change in Inspector.    
// That's it. Everything else stays the same.                    

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using NetworkAPI;
using GameLogic;

namespace GUI
{
    public class GameController : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        // TRANSPORT SWITCH — flip this to swap networking
        // ══════════════════════════════════════════════
        [Header("Network Mode")]
        [Tooltip("Check for SignalR/WebSocket (WAN), uncheck for UDP Multicast (LAN)")]
        public bool useSignalR = false;

        [Header("Prefabs — assign in Inspector")]
        public GameObject wallIndestructiblePrefab;
        public GameObject wallDestructiblePrefab;
        public GameObject floorPrefab;
        public GameObject playerPrefab;
        public GameObject bombPrefab;
        public GameObject explosionPrefab;

        [Header("UI — assign in Inspector")]
        public InputField nameInput;
        public Button joinButton;
        public Button startButton;
        public Text statusText;
        public Text playerListText;
        public GameObject lobbyPanel;
        public GameObject hudPanel;
        public Text hudStatusText;

        [Header("Settings")]
        public float cellSize = 1.0f;
        public float moveRepeatDelay = 0.15f;

        // ── Core systems ──
        private INetworkComm _network;    // ← Interface, not concrete class, check INetworkComm file
        private GridManager _grid;
        private BombLogic _bombLogic;
        private bool _isHost = false;
        private bool _gameActive = false;

        // ── Rendering state ──
        private Dictionary<string, GameObject> _playerObjects = new();
        private Dictionary<string, GameObject> _bombObjects = new();
        private Dictionary<(int, int), GameObject> _wallObjects = new();

        // ── Input throttle ──
        private float _lastMoveTime = 0f;

        // ── Thread-safe queue (network callbacks come from background threads) ──
        private readonly Queue<System.Action> _mainThreadQueue = new();
        private readonly object _queueLock = new();

        // UNITY LIFECYCLE

        void Awake()
        {
            _grid = new GridManager();
            _bombLogic = new BombLogic();

            // ── THE SWAP: one line controls LAN vs WAN ──
#if ENABLE_SIGNALR
            if (useSignalR)
                _network = new SignalRComm();
            else
#endif
            _network = new MulticastComm();
        }

        void Start()
        {
            joinButton.onClick.AddListener(OnJoinClicked);
            startButton.onClick.AddListener(OnStartClicked);
            startButton.interactable = false;

            lobbyPanel.SetActive(true);
            hudPanel.SetActive(false);

            RegisterNetworkEvents();
        }

        void Update()
        {
            // Drain thread-safe queue
            lock (_queueLock)
            {
                while (_mainThreadQueue.Count > 0)
                    _mainThreadQueue.Dequeue()?.Invoke();
            }

            if (!_gameActive) return;

            // Handle player input
            HandleInput();

            // Host checks bomb fuse timers
            if (_isHost)
                CheckBombTimers();
        }

        void OnDestroy()
        {
            _ = _network.DisconnectAsync();
        }

        // NETWORK EVENT REGISTRATION
        // Same events regardless of MulticastComm or SignalRComm
        
        // Connect game logic and network events
        private void RegisterNetworkEvents()
        {
            _network.OnPlayerJoined += (id, name) => Enqueue(() =>
            {
                if (!_bombLogic.Players.ContainsKey(id))
                {
                    int spawnIndex = _bombLogic.Players.Count;
                    if (spawnIndex < GridManager.SpawnPoints.Length)
                    {
                        var (sx, sy) = GridManager.SpawnPoints[spawnIndex];
                        _bombLogic.AddPlayer(id, name, sx, sy);
                    }
                }
                UpdatePlayerListUI();
                statusText.text = $"{name} joined! ({_bombLogic.Players.Count} players)";
            });

            _network.OnPlayerLeft += (id) => Enqueue(() =>
            {
                _bombLogic.RemovePlayer(id);
                RemovePlayerVisual(id);
                UpdatePlayerListUI();
            });

            _network.OnGameStartReceived += (gridData) => Enqueue(() =>
            {
                HandleGameStart(gridData);
            });

            _network.OnPlayerMoved += (id, x, y) => Enqueue(() =>
            {
                _bombLogic.MovePlayer(id, x, y);
                UpdatePlayerVisual(id, x, y);
            });

            _network.OnBombPlacedReceived += (ownerId, x, y) => Enqueue(() =>
            {
                var bomb = new BombData
                {
                    BombId = $"bomb_{x}_{y}_{Time.time}",
                    OwnerId = ownerId,
                    X = x,
                    Y = y,
                    PlacedAtTime = Time.time
                };
                _bombLogic.ActiveBombs[bomb.BombId] = bomb;
                if (_bombLogic.Players.TryGetValue(ownerId, out var p))
                    p.HasBombActive = true;
                SpawnBombVisual(bomb.BombId, x, y);
            });

            _network.OnBombExplodedReceived += (bombId, x, y, cellsStr) => Enqueue(() =>
            {
                var cells = BombLogic.DeserializeCells(cellsStr);
                HandleExplosionVisual(bombId, cells);

                // Destroy walls in our local grid
                foreach (var (cx, cy) in cells)
                    _grid.DestroyCell(cx, cy);
            });

            _network.OnPlayerDiedReceived += (id) => Enqueue(() =>
            {
                _bombLogic.KillPlayer(id);
                HandlePlayerDeathVisual(id);
            });

            _network.OnGameOverReceived += (winnerId) => Enqueue(() =>
            {
                _gameActive = false;
                HandleGameOverVisual(winnerId);
            });
        }

        // UI HANDLERS
        private async void OnJoinClicked()
        {
            string name = nameInput.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                statusText.text = "Please enter a name.";
                return;
            }

            statusText.text = "Connecting...";
            joinButton.interactable = false;

            await _network.ConnectAsync(name);

            if (_network.IsConnected)
            {
                // Add ourselves locally
                int spawnIndex = _bombLogic.Players.Count;
                var (sx, sy) = GridManager.SpawnPoints[spawnIndex];
                _bombLogic.AddPlayer(_network.MyPlayerId, name, sx, sy);

                // First player is the host
                if (_bombLogic.Players.Count == 1)
                    _isHost = true;

                startButton.interactable = _isHost;
                statusText.text = _isHost
                    ? $"Connected as {name} (HOST). Wait for players, then Start."
                    : $"Connected as {name}. Waiting for host to start...";

                Storage.ScoreStorage.LastPlayerName = name;
                UpdatePlayerListUI();
            }
            else
            {
                statusText.text = "Connection failed.";
                joinButton.interactable = true;
            }
        }

        private void OnStartClicked()
        {
            if (!_isHost || _bombLogic.Players.Count < 2)
            {
                statusText.text = "Need at least 2 players.";
                return;
            }

            // Host generates the grid and broadcasts it
            _grid.Generate();
            string gridData = _grid.SerializeToString();

            // Add spawn positions to the grid data message
            string spawnsStr = string.Join(":",
                _bombLogic.Players.Values.Select(p => $"{p.PlayerId},{p.PlayerName},{p.X},{p.Y}"));
            string fullData = gridData + "#" + spawnsStr;

            _network.SendStartGame(fullData);

            // Host also handles the start locally
            HandleGameStart(fullData);
        }

        // GAME START — BUILD THE GRID
        private void HandleGameStart(string data)
        {
            string[] halves = data.Split('#');
            string gridData = halves[0];

            // Parse grid
            _grid.InitializeFromString(gridData);

            // Parse player spawns if included
            if (halves.Length > 1 && !string.IsNullOrEmpty(halves[1]))
            {
                foreach (var entry in halves[1].Split(':'))
                {
                    var parts = entry.Split(',');
                    string pid = parts[0];
                    string pname = parts[1];
                    int px = int.Parse(parts[2]);
                    int py = int.Parse(parts[3]);

                    if (!_bombLogic.Players.ContainsKey(pid))
                        _bombLogic.AddPlayer(pid, pname, px, py);
                    else
                    {
                        _bombLogic.Players[pid].X = px;
                        _bombLogic.Players[pid].Y = py;
                    }
                }
            }

            // Switch to game UI
            lobbyPanel.SetActive(false);
            hudPanel.SetActive(true);
            hudStatusText.text = "FIGHT!";
            _gameActive = true;

            // Center camera
            Camera.main.transform.position = new Vector3(
                _grid.Width * cellSize / 2f, _grid.Height * cellSize / 2f, -10f);
            Camera.main.orthographicSize = _grid.Height * cellSize / 2f + 1f;

            // Render grid
            for (int x = 0; x < _grid.Width; x++)
            {
                for (int y = 0; y < _grid.Height; y++)
                {
                    Vector3 pos = GridToWorld(x, y);

                    Instantiate(floorPrefab, pos, Quaternion.identity);

                    var cell = _grid.GetCell(x, y);
                    if (cell == CellType.IndestructibleWall)
                    {
                        var wall = Instantiate(wallIndestructiblePrefab, pos, Quaternion.identity);
                        _wallObjects[(x, y)] = wall;
                    }
                    else if (cell == CellType.DestructibleWall)
                    {
                        var wall = Instantiate(wallDestructiblePrefab, pos, Quaternion.identity);
                        _wallObjects[(x, y)] = wall;
                    }
                }
            }

            // Render players
            Color[] playerColors = { Color.green, Color.red, Color.blue, Color.yellow };
            int colorIdx = 0;
            foreach (var p in _bombLogic.Players.Values)
            {
                SpawnPlayerVisual(p.PlayerId, p.PlayerName, p.X, p.Y,
                    p.PlayerId == _network.MyPlayerId
                        ? Color.green
                        : playerColors[colorIdx % playerColors.Length]);
                colorIdx++;
            }
        }

        // INPUT
        private void HandleInput()
        {
            if (Time.time - _lastMoveTime < moveRepeatDelay) return;

            if (!_bombLogic.Players.TryGetValue(_network.MyPlayerId, out var me)) return;
            if (!me.IsAlive) return;

            int dx = 0, dy = 0;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) dy = 1;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) dy = -1;
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) dx = -1;
            else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) dx = 1;

            if (dx != 0 || dy != 0)
            {
                int newX = me.X + dx;
                int newY = me.Y + dy;

                // Client-side validation (prevents sending bad moves)
                if (_bombLogic.IsValidMove(_network.MyPlayerId, newX, newY, _grid))
                {
                    _lastMoveTime = Time.time;

                    // Apply locally immediately (responsive feel)
                    _bombLogic.MovePlayer(_network.MyPlayerId, newX, newY);
                    UpdatePlayerVisual(_network.MyPlayerId, newX, newY);

                    // Broadcast to other players
                    _network.SendMove(newX, newY);
                }
            }

            // Space to place bomb
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var bomb = _bombLogic.TryPlaceBomb(_network.MyPlayerId, Time.time);
                if (bomb != null)
                {
                    SpawnBombVisual(bomb.BombId, bomb.X, bomb.Y);
                    _network.SendPlaceBomb(bomb.X, bomb.Y);
                }
            }
        }

        // BOMB TIMER (host runs this)
        private void CheckBombTimers()
        {
            var expired = _bombLogic.GetExpiredBombs(Time.time);
            foreach (var bombId in expired)
            {
                if (!_bombLogic.ActiveBombs.TryGetValue(bombId, out var bomb)) continue;

                var (cells, killed) = _bombLogic.ExplodeBomb(bombId, _grid);

                // Broadcast explosion
                string cellsStr = BombLogic.SerializeCells(cells);
                _network.SendBombExploded(bombId, bomb.X, bomb.Y, cellsStr);

                // Render locally
                HandleExplosionVisual(bombId, cells);

                // Broadcast deaths
                foreach (var pid in killed)
                {
                    _network.SendPlayerDied(pid);
                    HandlePlayerDeathVisual(pid);
                }

                // Check win condition
                string winner = _bombLogic.CheckWinner();
                if (winner != null)
                {
                    _gameActive = false;
                    _network.SendGameOver(winner);
                    HandleGameOverVisual(winner);
                }
            }
        }

        // VISUAL HELPERS
        private Vector3 GridToWorld(int x, int y)
        {
            return new Vector3(x * cellSize, y * cellSize, 0f);
        }

        private void SpawnPlayerVisual(string id, string name, int x, int y, Color color)
        {
            var go = Instantiate(playerPrefab, GridToWorld(x, y), Quaternion.identity);
            go.name = $"Player_{name}";
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) sr.color = color;
            _playerObjects[id] = go;
        }

        private void UpdatePlayerVisual(string id, int x, int y)
        {
            if (_playerObjects.TryGetValue(id, out var go))
                go.transform.position = GridToWorld(x, y);
        }

        private void RemovePlayerVisual(string id)
        {
            if (_playerObjects.TryGetValue(id, out var go))
            {
                Destroy(go);
                _playerObjects.Remove(id);
            }
        }

        private void SpawnBombVisual(string bombId, int x, int y)
        {
            var go = Instantiate(bombPrefab, GridToWorld(x, y), Quaternion.identity);
            go.name = $"Bomb_{bombId}";
            _bombObjects[bombId] = go;
        }

        private void HandleExplosionVisual(string bombId, List<(int x, int y)> cells)
        {
            if (_bombObjects.TryGetValue(bombId, out var bombGo))
            {
                Destroy(bombGo);
                _bombObjects.Remove(bombId);
            }

            foreach (var (cx, cy) in cells)
            {
                if (_wallObjects.TryGetValue((cx, cy), out var wallGo))
                {
                    Destroy(wallGo);
                    _wallObjects.Remove((cx, cy));
                }
                var fx = Instantiate(explosionPrefab, GridToWorld(cx, cy), Quaternion.identity);
                Destroy(fx, 0.5f);
            }
        }

        private void HandlePlayerDeathVisual(string id)
        {
            if (_playerObjects.TryGetValue(id, out var go))
            {
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) sr.color = Color.gray;
                go.transform.localScale *= 0.5f;
            }
            if (id == _network.MyPlayerId)
                hudStatusText.text = "YOU DIED!";
        }

        private void HandleGameOverVisual(string winnerId)
        {
            if (winnerId == "draw")
            {
                hudStatusText.text = "DRAW!";
                Storage.ScoreStorage.RecordLoss();
            }
            else if (winnerId == _network.MyPlayerId)
            {
                hudStatusText.text = "YOU WIN!";
                Storage.ScoreStorage.RecordWin();
            }
            else
            {
                var w = _bombLogic.Players.GetValueOrDefault(winnerId);
                hudStatusText.text = $"{w?.PlayerName ?? "?"} WINS!";
                Storage.ScoreStorage.RecordLoss();
            }
        }

        private void UpdatePlayerListUI()
        {
            var names = _bombLogic.Players.Values.Select(p =>
                p.PlayerId == _network.MyPlayerId ? $"{p.PlayerName} (You)" : p.PlayerName);
            playerListText.text = "Players:\n" + string.Join("\n", names);
        }

        // THREAD-SAFE QUEUE
        private void Enqueue(System.Action action)
        {
            lock (_queueLock) { _mainThreadQueue.Enqueue(action); }
        }
    }
}
