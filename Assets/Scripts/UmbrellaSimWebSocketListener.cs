using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UmbrellaSimWebSocketListener : MonoBehaviour
{
    [Header("WebSocket Configuration")]
    [SerializeField] private string serverURL = "ws://localhost:8080";

    [Header("Core Engine Link")]
    [SerializeField] private UmbrellaControllerA umbrellaEngine;

    private ClientWebSocket _webSocket = null;
    private CancellationTokenSource _cts;
    
    // Thread-safe command buffer to pass strings from background network threads safely to Unity's main thread
    private string _pendingCommand = "";
    private readonly object _lockObject = new object();

    void Start()
    {
        if (umbrellaEngine == null)
        {
            umbrellaEngine = GetComponent<UmbrellaControllerA>();
        }

        _cts = new CancellationTokenSource();
        ConnectToServer();
    }

    private async void ConnectToServer()
    {
        _webSocket = new ClientWebSocket();
        try
        {
            Debug.Log($"[Sim Listener] Connecting to WebSocket relay at: {serverURL}");
            await _webSocket.ConnectAsync(new Uri(serverURL), _cts.Token);
            Debug.Log("<color=#00FF88><b>[Sim Listener]: Successfully linked to Twin override channel!</b></color>");
            
            await ReceiveLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Sim Listener Error]: Handshake dropped: {ex.Message}");
        }
    }

    private async Task ReceiveLoop()
    {
        byte[] buffer = new byte[1024];
        while (_webSocket.State == WebSocketState.Open)
        {
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cts.Token);
            }
            else
            {
                string command = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Safely lock and cache the incoming command string
                lock (_lockObject)
                {
                    _pendingCommand = command;
                }
            }
        }
    }

    void Update()
    {
        string commandToExecute = "";
        
        // Drain the thread-safe buffer smoothly on Unity's safe main loop thread
        lock (_lockObject)
        {
            if (!string.IsNullOrEmpty(_pendingCommand))
            {
                commandToExecute = _pendingCommand;
                _pendingCommand = ""; 
            }
        }

        if (!string.IsNullOrEmpty(commandToExecute))
        {
            ExecuteOverridePayload(commandToExecute);
        }
    }

    private void ExecuteOverridePayload(string command)
    {
        Debug.Log($"<color=#FFFF00><b>[Simulation Override]:</b></color> Intercepted remote packet payload: {command}");
        
        if (umbrellaEngine != null)
        {
            // Route the override command directly into your physical simulation engine logic
            umbrellaEngine.ApplyRemoteOverride(command);
        }
    }

    private void OnDestroy()
    {
        if (_webSocket != null) _webSocket.Dispose();
        _cts?.Cancel();
    }
}