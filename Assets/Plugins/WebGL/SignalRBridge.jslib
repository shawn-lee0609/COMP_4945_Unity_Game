mergeInto(LibraryManager.library, {

    // ══════════════════════════════════════════════════════════════
    // Initialize SignalR connection + register ALL receive handlers
    // 
    // BEFORE (broken): called window._signalRInit() which didn't exist
    //   → connection never established, no receive handlers registered
    //
    // AFTER (fixed): all logic is inline
    //   → connection created, handlers registered, JoinGame called
    // ══════════════════════════════════════════════════════════════
    SignalR_Init: function(urlPtr, playerNamePtr, gameObjectNamePtr) {
        var url = UTF8ToString(urlPtr);
        var playerName = UTF8ToString(playerNamePtr);
        var goName = UTF8ToString(gameObjectNamePtr);

        // Store GO name globally so other functions can reference it if needed
        window._signalRGameObject = goName;

        function startConnection() {
            var connection = new signalR.HubConnectionBuilder()
                .withUrl(url)
                .withAutomaticReconnect()
                .build();

            // Store globally so Send methods and IsConnected can access it
            window._hubConnection = connection;

            // ── Helper: safely call back into Unity C# ──
            // SendMessage is global in Unity WebGL runtime,
            // but we fall back to unityInstance if needed
            function sendToUnity(method, data) {
                try {
                    if (typeof SendMessage === 'function') {
                        SendMessage(goName, method, data);
                    } else if (window.unityInstance) {
                        window.unityInstance.SendMessage(goName, method, data);
                    } else {
                        console.error("[SignalR] No SendMessage available for: " + method);
                    }
                } catch (e) {
                    console.error("[SignalR] SendMessage error (" + method + "):", e);
                }
            }

            // ══════════════════════════════════════════════
            // RECEIVE HANDLERS
            // These replace the RegisterHandlers() method  
            // from SignalRComm.cs (desktop build)
            //
            // Server event name → C# method name on SignalRCommWebGL
            // Note: methods with "Msg" suffix pack multi-arg data
            //       with "|" delimiter because Unity SendMessage
            //       only accepts a single string parameter
            // ══════════════════════════════════════════════

            // Server assigns us a unique player ID
            connection.on("OnAssignedId", function(id) {
                window._playerId = id;
                console.log("[SignalR] Assigned ID: " + id);
                sendToUnity("OnAssignedId", id);
            });

            // Server tells us if we're the host
            connection.on("OnHostAssigned", function(isHost) {
                console.log("[SignalR] Host assigned: " + isHost);
                sendToUnity("OnHostAssigned", isHost.toString());
            });

            // Another player joined → pack (id, name) as "id|name"
            connection.on("OnPlayerJoined", function(id, name) {
                console.log("[SignalR] Player joined: " + id + " (" + name + ")");
                sendToUnity("OnPlayerJoinedMsg", id + "|" + name);
            });

            // A player left
            connection.on("OnPlayerLeft", function(id) {
                console.log("[SignalR] Player left: " + id);
                sendToUnity("OnPlayerLeftMsg", id);
            });

            // Host broadcast the game grid → all clients receive it
            connection.on("OnGameStart", function(gridData) {
                console.log("[SignalR] Game start received");
                sendToUnity("OnGameStart", gridData);
            });

            // A player moved → pack (id, x, y) as "id|x|y"
            connection.on("OnPlayerMoved", function(id, x, y) {
                sendToUnity("OnPlayerMovedMsg", id + "|" + x + "|" + y);
            });

            // A bomb was placed → pack (senderId, x, y) as "senderId|x|y"
            connection.on("OnBombPlaced", function(senderId, x, y) {
                sendToUnity("OnBombPlaced", senderId + "|" + x + "|" + y);
            });

            // A bomb exploded → pack (bombId, x, y, cells) as "bombId|x|y|cells"
            connection.on("OnBombExploded", function(bombId, x, y, cells) {
                sendToUnity("OnBombExploded", bombId + "|" + x + "|" + y + "|" + cells);
            });

            // A player died
            connection.on("OnPlayerDied", function(id) {
                sendToUnity("OnPlayerDied", id);
            });

            // Game over with winner
            connection.on("OnGameOver", function(winnerId) {
                sendToUnity("OnGameOver", winnerId);
            });

            // ── Start connection and join game ──
            connection.start().then(function() {
                console.log("[SignalR] Connected to hub at " + url);
                return connection.invoke("JoinGame", playerName);
            }).then(function() {
                console.log("[SignalR] JoinGame invoked for: " + playerName);
            }).catch(function(err) {
                console.error("[SignalR] Connection/Join failed:", err);
            });
        }

        // Load SignalR JS library from CDN if not already loaded, then connect
        if (typeof signalR === 'undefined') {
            var script = document.createElement('script');
            script.src = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/6.0.1/signalr.min.js';
            script.onload = function() {
                console.log("[SignalR] Library loaded from CDN");
                startConnection();
            };
            script.onerror = function() {
                console.error("[SignalR] Failed to load SignalR library from CDN");
            };
            document.head.appendChild(script);
        } else {
            startConnection();
        }
    },

    // ══════════════════════════════════════════════
    // SEND METHODS (unchanged — these already worked)
    // ══════════════════════════════════════════════

    SignalR_Disconnect: function() {
       if (window._hubConnection) {
           window._hubConnection.stop();
           window._hubConnection = null;
           window._playerId = "";
       }
   },

    SignalR_SendMove: function(x, y) {
        if (window._hubConnection) {
            window._hubConnection.invoke("PlayerMove", x, y).catch(function(err) {
                console.error("SendMove error:", err);
            });
        }
    },

    SignalR_SendPlaceBomb: function(x, y) {
        if (window._hubConnection) {
            window._hubConnection.invoke("PlaceBomb", x, y).catch(function(err) {
                console.error("SendPlaceBomb error:", err);
            });
        }
    },

    SignalR_SendBombExploded: function(bombIdPtr, x, y, cellsPtr) {
        if (window._hubConnection) {
            var bombId = UTF8ToString(bombIdPtr);
            var cells = UTF8ToString(cellsPtr);
            window._hubConnection.invoke("BombExploded", bombId, x, y, cells);
        }
    },

    SignalR_SendPlayerDied: function(playerIdPtr) {
        if (window._hubConnection) {
            var playerId = UTF8ToString(playerIdPtr);
            window._hubConnection.invoke("PlayerDied", playerId);
        }
    },

    SignalR_SendGameOver: function(winnerIdPtr) {
        if (window._hubConnection) {
            var winnerId = UTF8ToString(winnerIdPtr);
            window._hubConnection.invoke("GameOver", winnerId);
        }
    },

    SignalR_SendStartGame: function(gridDataPtr) {
        if (window._hubConnection) {
            var gridData = UTF8ToString(gridDataPtr);
            window._hubConnection.invoke("StartGame", gridData);
        }
    },

    SignalR_IsConnected: function() {
        return window._hubConnection &&
               window._hubConnection.state === signalR.HubConnectionState.Connected ? 1 : 0;
    },

    SignalR_GetPlayerId: function() {
        var id = window._playerId || "";
        var bufferSize = lengthBytesUTF8(id) + 1;
        var buffer = _malloc(bufferSize);
        stringToUTF8(id, buffer, bufferSize);
        return buffer;
    }
});
