# Bomberman Multiplayer
A real-time multiplayer Bomberman game built with Unity and C#, using SignalR WebSocket for networking.

# Overview
Players navigate a grid-based arena, place bombs, and try to eliminate opponents through explosions. The last player standing wins.

# Gameplay

- Move your character across a grid (up, down, left, right)
- Place bombs that explode in a cross pattern after a short timer
- Explosions can chain-react into nearby bombs
- Destructible walls can be blown up to open new paths
- Indestructible walls act as permanent barriers
- Eliminated players are out for the round — last one alive wins

# How to Run

Server

1. Navigate to the Server/ directory
2. Run dotnet run
3. The SignalR hub will start on the configured port

Client (Unity)

1. Open the project in Unity
2. Set the server URL in the Networking settings
3. Press Play or build and run the executable
