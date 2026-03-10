// Assets/Scripts/GameLogic/BombLogic.cs
// NAMESPACE: GameLogic
// Pure C# — handles bombs, explosions, player state, win detection.
// In multicast mode: each client runs this locally.
// In SignalR mode: server runs this authoritatively, client mirrors for rendering.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GameLogic
{
    public class PlayerData
    {
        public string PlayerId { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool HasBombActive { get; set; } = false;
    }

    public class BombData
    {
        public string BombId { get; set; } = "";
        public string OwnerId { get; set; } = "";
        public int X { get; set; } // X coordinate for the bomb location
        public int Y { get; set; } // Y coordinate for the bomb location
        public float PlacedAtTime { get; set; }  // Time.time when placed
        public bool Exploded { get; set; } = false;
    }

    public class BombLogic
    {
        public const float BombFuseSeconds = 3.0f; // Changable
        public const int ExplosionRange = 2; // Changable

        public Dictionary<string, PlayerData> Players { get; } = new();
        public Dictionary<string, BombData> ActiveBombs { get; } = new();

        private int _nextBombId = 0;

        // ── Player management ──

        public PlayerData AddPlayer(string id, string name, int x, int y)
        {
            var player = new PlayerData
            {
                PlayerId = id,
                PlayerName = name,
                X = x,
                Y = y
            };
            Players[id] = player;
            return player;
        }

        public void MovePlayer(string id, int x, int y)
        {
            // Try to find a Player using id, if there is a valid result,
            // store it in variable p and return true, if not return false
            // out var -> Search in the Dictionary for the vlaue and store it in the variable
            //         -> var: auto inference the type of the value
            //                 in this case the Dictionary knows that the type is PlayerData
            if (Players.TryGetValue(id, out var p))
            {
                p.X = x;
                p.Y = y;
            }
        }

        public void KillPlayer(string id)
        {
            if (Players.TryGetValue(id, out var p))
                p.IsAlive = false;
        }

        public void RemovePlayer(string id)
        {
            Players.Remove(id);
        }

        // ── Movement validation ──

        public bool IsValidMove(string playerId, int newX, int newY, GridManager grid)
        {
            if (!Players.TryGetValue(playerId, out var player)) return false; // Checks the ID exists
            if (!player.IsAlive) return false; // Checks the player is alive

            // Must be exactly 1 step in cardinal direction
            // Only ab.e to move either vertical or horizontal with a max 1 distance
            int dx = Math.Abs(newX - player.X);
            int dy = Math.Abs(newY - player.Y);
            if (dx + dy != 1) return false;

            // Must be walkable
            if (!grid.IsWalkable(newX, newY)) return false;

            // Can't walk into active bombs
            if (ActiveBombs.Values.Any(b => b.X == newX && b.Y == newY && !b.Exploded))
                return false;

            return true;
        }

        // ── Bomb placement ──

        public BombData TryPlaceBomb(string playerId, float currentTime)
        {
            if (!Players.TryGetValue(playerId, out var player)) return null;
            if (!player.IsAlive) return null;
            if (player.HasBombActive) return null;

            // Don't place on top of another bomb
            if (ActiveBombs.Values.Any(b => b.X == player.X && b.Y == player.Y && !b.Exploded))
                return null;

            var bomb = new BombData
            {
                BombId = $"bomb_{_nextBombId++}",
                OwnerId = playerId,
                X = player.X,
                Y = player.Y,
                PlacedAtTime = currentTime
            };
            ActiveBombs[bomb.BombId] = bomb;
            player.HasBombActive = true;
            return bomb;
        }

        // ── Explosion calculation ──

        /// <summary>
        /// Calculate which cells an explosion hits. Returns destroyed cells list.
        /// </summary>
        public List<(int x, int y)> CalculateExplosion(int bombX, int bombY, GridManager grid)
        {
            var destroyed = new List<(int x, int y)>();
            destroyed.Add((bombX, bombY)); // The location where the bomb is placed is initially added to the destroyed cell list

            int[][] directions = { new[] { 1, 0 }, new[] { -1, 0 }, new[] { 0, 1 }, new[] { 0, -1 } };
            foreach (var dir in directions)
            {
                for (int i = 1; i <= ExplosionRange; i++)
                {
                    int cx = bombX + dir[0] * i;
                    int cy = bombY + dir[1] * i;

                    if (cx < 0 || cx >= grid.Width || cy < 0 || cy >= grid.Height) break;
                    if (grid.GetCell(cx, cy) == CellType.IndestructibleWall) break;

                    destroyed.Add((cx, cy));

                    // Explosion stops after hitting a destructible wall (but destroys it)
                    if (grid.GetCell(cx, cy) == CellType.DestructibleWall)
                        break;
                }
            }
            // Returns the list of destroyed cells
            return destroyed;
        }

        /// <summary>
        /// Explode a bomb and apply effects. Returns destroyed cells and killed player IDs.
        /// </summary>
        public (List<(int x, int y)> cells, List<string> killed) ExplodeBomb(
            string bombId, GridManager grid)
        {
            var killed = new List<string>();
            // If there is no bombId in the ActiveBombs list
            // returns nothing on the destroyed cell list nor killed player list
            if (!ActiveBombs.TryGetValue(bombId, out var bomb))
                return (new List<(int, int)>(), killed);
            // If there is a bomb but was already exploded
            if (bomb.Exploded)
                return (new List<(int, int)>(), killed);

            bomb.Exploded = true;

            // Free owner's bomb slot so that the owner can place a new bomb
            if (Players.TryGetValue(bomb.OwnerId, out var owner))
                owner.HasBombActive = false;

            var destroyed = CalculateExplosion(bomb.X, bomb.Y, grid);

            // Destroy walls
            foreach (var (cx, cy) in destroyed)
                grid.DestroyCell(cx, cy);

            // Kill players in blast zone
            foreach (var p in Players.Values)
            {
                if (!p.IsAlive) continue;
                if (destroyed.Any(c => c.x == p.X && c.y == p.Y))
                {
                    p.IsAlive = false;
                    killed.Add(p.PlayerId);
                }
            }

            return (destroyed, killed);
        }

        // ── Timer check ──

        /// <summary>
        /// Returns bomb IDs whose fuse has expired
        /// </summary>
        public List<string> GetExpiredBombs(float currentTime)
        {
            return ActiveBombs.Values
                .Where(b => !b.Exploded && (currentTime - b.PlacedAtTime) >= BombFuseSeconds)
                .Select(b => b.BombId)
                .ToList();
        }

        // ── Win detection ──

        public string CheckWinner()
        {
            var alive = Players.Values.Where(p => p.IsAlive).ToList();
            if (Players.Count <= 1) return null;
            if (alive.Count == 1) return alive[0].PlayerId;
            if (alive.Count == 0) return "draw";
            return null;
        }

        // ── Serialization helpers for network messages ──

        /// <summary>
        /// Serialize destroyed cells list to string: "x1,y1:x2,y2:x3,y3"
        /// </summary>
        public static string SerializeCells(List<(int x, int y)> cells)
        {
            return string.Join(":", cells.Select(c => $"{c.x},{c.y}"));
        }

        /// <summary>
        /// Deserialize cells string back to list
        /// </summary>
        public static List<(int x, int y)> DeserializeCells(string data)
        {
            var cells = new List<(int x, int y)>();
            if (string.IsNullOrEmpty(data)) return cells;
            foreach (var pair in data.Split(':'))
            {
                var xy = pair.Split(',');
                cells.Add((int.Parse(xy[0]), int.Parse(xy[1])));
            }
            return cells;
        }
    }
}
