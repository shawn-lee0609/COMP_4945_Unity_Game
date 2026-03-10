// Assets/Scripts/GameLogic/GridManager.cs
// NAMESPACE: GameLogic
// Pure C# — no Unity dependency. Works identically for both multicast and SignalR.
// Manages the grid state: wall types, walkability, destruction.

using System;
using System.Collections.Generic;
using System.Text;

namespace GameLogic
{
    public enum CellType
    {
        Empty = 0, // An empty space where players can walk around
        IndestructibleWall = 1, // A wall which never gets destroyed
        DestructibleWall = 2 // A wall that can be destroyed by bombs
    }

    public class GridManager
    {
        public const int DefaultWidth = 11;
        public const int DefaultHeight = 11;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public CellType[,] Grid { get; private set; }

        // Spawn points for up to 4 players (corners)
        public static readonly (int x, int y)[] SpawnPoints = new[]
        {
            (1, 1), (9, 1), (1, 9), (9, 9) // Initial location where 4 players would be located
        };

        /// <summary>
        /// Generate a new grid (called by the host player (The first player who joined the network))
        /// </summary>
        public void Generate(int width = DefaultWidth, int height = DefaultHeight)
        {
            Width = width;
            Height = height;
            Grid = new CellType[width, height];

            // Fill border walls with IndestructableWall cells
            for (int x = 0; x < width; x++)
            {
                Grid[x, 0] = CellType.IndestructibleWall;
                Grid[x, height - 1] = CellType.IndestructibleWall;
            }
            for (int y = 0; y < height; y++)
            {
                Grid[0, y] = CellType.IndestructibleWall;
                Grid[width - 1, y] = CellType.IndestructibleWall;
            }

            // Interior pillar walls at (even#,even#) positions
            for (int x = 2; x < width - 1; x += 2)
                for (int y = 2; y < height - 1; y += 2)
                    Grid[x, y] = CellType.IndestructibleWall;

            // Randomly fill destructible walls (~40%)
            Random rng = new Random();
            for (int x = 1; x < width - 1; x++)
            {
                for (int y = 1; y < height - 1; y++)
                {
                    if (Grid[x, y] != CellType.Empty) continue;
                    if (IsInSafeZone(x, y)) continue;
                    if (rng.NextDouble() < 0.40)
                        Grid[x, y] = CellType.DestructibleWall;
                }
            }
        }

        /// <summary>
        /// 3x3 clear zone around each spawn corner
        /// </summary>
        private bool IsInSafeZone(int x, int y)
        {
            foreach (var (sx, sy) in SpawnPoints)
                if (Math.Abs(x - sx) <= 1 && Math.Abs(y - sy) <= 1)
                    return true;
            return false;
        }

        /// <summary>
        /// Initialize from serialized string (received over network)
        /// The received string format information is restored
        /// To distinguish the border for width, height and cells ; is used
        /// Format: "width,height;cell0,cell1,cell2,..."
        /// </summary>
        public void InitializeFromString(string data)
        {
            string[] parts = data.Split(';');
            string[] dims = parts[0].Split(',');
            Width = int.Parse(dims[0]);
            Height = int.Parse(dims[1]);
            Grid = new CellType[Width, Height];

            string[] cells = parts[1].Split(',');
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    Grid[x, y] = (CellType)int.Parse(cells[y * Width + x]);
        }

        /// <summary>
        /// Serialize grid to string for sending over network
        /// </summary>
        public string SerializeToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"{Width},{Height};");
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    if (x > 0 || y > 0) sb.Append(',');
                    sb.Append((int)Grid[x, y]);
                }
            }
            return sb.ToString();
        }

        public CellType GetCell(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height)
                return CellType.IndestructibleWall;
            return Grid[x, y];
        }

        public bool IsWalkable(int x, int y)
        {
            return GetCell(x, y) == CellType.Empty;
        }

        public void DestroyCell(int x, int y)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
                Grid[x, y] = CellType.Empty;
        }
    }
}
