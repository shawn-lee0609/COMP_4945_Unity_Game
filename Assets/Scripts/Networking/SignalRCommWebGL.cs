#if UNITY_WEBGL && ENABLE_SIGNALR

using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

namespace NetworkAPI
{
    public class SignalRCommWebGL : MonoBehaviour, INetworkComm
    {
        // DllImport connects C# to the .jslib functions
        [DllImport("__Internal")]
        private static extern void SignalR_Init(string url, string playerName, string gameObjectName);

        [DllImport("__Internal")]
        private static extern void SignalR_SendMove(int x, int y);

        [DllImport("__Internal")]
        private static extern void SignalR_SendPlaceBomb(int x, int y);

        [DllImport("__Internal")]
        private static extern void SignalR_SendBombExploded(string bombId, int x, int y, string cells);

        [DllImport("__Internal")]
        private static extern void SignalR_SendPlayerDied(string playerId);

        [DllImport("__Internal")]
        private static extern void SignalR_SendGameOver(string winnerId);

        [DllImport("__Internal")]
        private static extern void SignalR_SendStartGame(string gridData);

        [DllImport("__Internal")]
        private static extern int SignalR_IsConnected();

        [DllImport("__Internal")]
        private static extern void SignalR_Disconnect();

        [DllImport("__Internal")]
        private static extern string SignalR_GetPlayerId();

        private const string SERVER_URL = "/gamehub";

        public string MyPlayerId => SignalR_GetPlayerId();
        public bool IsConnected => SignalR_IsConnected() == 1;
        private bool _isHostFromServer = false;
        public bool IsHost => _isHostFromServer;

        // ── TaskCompletionSource for ConnectAsync ──
        // Replaces Task.Delay polling which HANGS in WebGL
        private TaskCompletionSource<bool> _connectTcs;

        public event Action<string, string> OnPlayerJoined;
        public event Action<string> OnPlayerLeft;
        public event Action<string> OnGameStartReceived;
        public event Action<string, int, int> OnPlayerMoved;
        public event Action<string, int, int> OnBombPlacedReceived;
        public event Action<string, int, int, string> OnBombExplodedReceived;
        public event Action<string> OnPlayerDiedReceived;
        public event Action<string> OnGameOverReceived;

        /// <summary>
        /// Connect to SignalR hub.
        /// 
        /// OLD (broken in WebGL):
        ///   while (!IsConnected) { await Task.Delay(100); }  ← HANGS forever
        ///
        /// NEW (WebGL-safe):
        ///   TaskCompletionSource completed by coroutine polling with WaitForSeconds
        ///   Coroutine provides the timeout instead of Task.Delay
        /// </summary>
        public Task ConnectAsync(string playerName)
        {
            _connectTcs = new TaskCompletionSource<bool>();

            SignalR_Init(SERVER_URL, playerName, gameObject.name);

            // Coroutine-based timeout (5 seconds) — works in WebGL unlike Task.Delay
            StartCoroutine(ConnectTimeoutCoroutine(5f));

            return _connectTcs.Task;
        }

        public Task DisconnectAsync()
        {
            SignalR_Disconnect();
            return Task.CompletedTask;
        }

        private IEnumerator ConnectTimeoutCoroutine(float timeoutSeconds)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (IsConnected)
                {
                    // Wait a bit for OnHostAssigned callback to arrive too
                    yield return new WaitForSeconds(0.5f);
                    _connectTcs?.TrySetResult(true);
                    yield break;
                }
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            // Timeout
            Debug.LogWarning("[SignalRCommWebGL] Connection timed out after 5 seconds");
            _connectTcs?.TrySetResult(false);
        }

        /// <summary>
        /// WebGL-safe replacement for Task.Delay.
        /// 
        /// Task.Delay HANGS in Unity WebGL (single-threaded environment).
        /// This uses a Unity coroutine internally but returns a Task
        /// so it can be awaited just like Task.Delay.
        /// 
        /// Usage in GameController:
        ///   #if UNITY_WEBGL && ENABLE_SIGNALR
        ///       await ((SignalRCommWebGL)_network).DelayAsync(1.5f);
        ///   #else
        ///       await Task.Delay(1500);
        ///   #endif
        /// </summary>
        public Task DelayAsync(float seconds)
        {
            var tcs = new TaskCompletionSource<bool>();
            StartCoroutine(DelayCoroutine(seconds, tcs));
            return tcs.Task;
        }

        private IEnumerator DelayCoroutine(float seconds, TaskCompletionSource<bool> tcs)
        {
            yield return new WaitForSeconds(seconds);
            tcs.TrySetResult(true);
        }

        public void SendMove(int x, int y) => SignalR_SendMove(x, y);
        public void SendPlaceBomb(int x, int y) => SignalR_SendPlaceBomb(x, y);
        public void SendBombExploded(string bombId, int x, int y, string cells) =>
            SignalR_SendBombExploded(bombId, x, y, cells);
        public void SendPlayerDied(string playerId) => SignalR_SendPlayerDied(playerId);
        public void SendGameOver(string winnerId) => SignalR_SendGameOver(winnerId);
        public void SendStartGame(string gridData) => SignalR_SendStartGame(gridData);
        public void SendJoin(string playerName) { } // Handled in ConnectAsync

        // ══════════════════════════════════════════════
        // CALLBACKS FROM JAVASCRIPT (via SendMessage)
        // ══════════════════════════════════════════════

        public void OnAssignedId(string id)
        {
            Debug.Log($"[SignalRCommWebGL] Assigned ID: {id}");
        }

        public void OnHostAssigned(string isHostStr)
        {
            _isHostFromServer = isHostStr == "true" || isHostStr == "True";
            Debug.Log($"[SignalRCommWebGL] Host assigned: {_isHostFromServer}");
        }

        public void OnPlayerJoinedMsg(string data)
        {
            var parts = data.Split('|');
            if (parts.Length >= 2)
            {
                Debug.Log($"[SignalRCommWebGL] Player joined: {parts[0]} ({parts[1]})");
                OnPlayerJoined?.Invoke(parts[0], parts[1]);
            }
        }

        public void OnPlayerLeftMsg(string id)
        {
            Debug.Log($"[SignalRCommWebGL] Player left: {id}");
            OnPlayerLeft?.Invoke(id);
        }

        public void OnGameStart(string gridData)
        {
            Debug.Log("[SignalRCommWebGL] Game start received");
            OnGameStartReceived?.Invoke(gridData);
        }

        public void OnPlayerMovedMsg(string data)
        {
            var parts = data.Split('|');
            if (parts.Length >= 3)
                OnPlayerMoved?.Invoke(parts[0], int.Parse(parts[1]), int.Parse(parts[2]));
        }

        public void OnBombPlaced(string data)
        {
            var parts = data.Split('|');
            if (parts.Length >= 3)
                OnBombPlacedReceived?.Invoke(parts[0], int.Parse(parts[1]), int.Parse(parts[2]));
        }

        public void OnBombExploded(string data)
        {
            var parts = data.Split('|');
            if (parts.Length >= 4)
                OnBombExplodedReceived?.Invoke(parts[0], int.Parse(parts[1]),
                    int.Parse(parts[2]), parts[3]);
        }

        public void OnPlayerDied(string id) => OnPlayerDiedReceived?.Invoke(id);
        public void OnGameOver(string winnerId) => OnGameOverReceived?.Invoke(winnerId);
    }
}
#endif
