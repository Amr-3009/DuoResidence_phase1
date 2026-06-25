using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

[RequireComponent(typeof(Animator))]
public class UmbrellaTwinWebSocket : MonoBehaviour
{
    [Header("WebSocket Server Configuration")]
    [Tooltip("The WebSocket server URL (e.g., ws://127.0.0.1:8000 or your specific project port)")]
    [SerializeField] private string serverURL = "ws://localhost:8080";

    [Header("Telemetry UI Readouts")]
    [SerializeField] private TextMeshProUGUI windDisplay;
    [SerializeField] private TextMeshProUGUI rainDisplay;
    [SerializeField] private TextMeshProUGUI solarDisplay;

    [Header("Dashboard UI Controls")]
    [SerializeField] private Button forceOpenButton;
    [SerializeField] private Button forceCloseButton;

    private ClientWebSocket _webSocket = null;
    private CancellationTokenSource _cancellationTokenSource;
    private Animator _animator;

    // Local runtime variables to hold incoming data streams
    private float _currentWind = 0f;
    private float _currentRain = 0f;
    private float _currentSolar = 0f;

    void Start()
    {
        _animator = GetComponent<Animator>();
        _cancellationTokenSource = new CancellationTokenSource();

        // Bind interactive button clicks to outbound WebSocket network dispatches
        if (forceOpenButton != null) forceOpenButton.onClick.AddListener(() => SendControlCommand("COMMAND_OPEN"));
        if (forceCloseButton != null) forceCloseButton.onClick.AddListener(() => SendControlCommand("COMMAND_CLOSE"));

        // Establish connection thread
        ConnectToServerTask();
    }

    private async void ConnectToServerTask()
    {
        _webSocket = new ClientWebSocket();
        Uri serverUri = new Uri(serverURL);

        try
        {
            Debug.Log($"[WebSocket Twin] Attempting handshake with connection pool at: {serverURL}");
            await _webSocket.ConnectAsync(serverUri, _cancellationTokenSource.Token);
            Debug.Log("<color=#00FF88><b>[WebSocket Twin]: Connected successfully to twin backend!</b></color>");

            // Begin the continuous background parsing loop for incoming telemetry
            await ReceiveTelemetryLoop();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[WebSocket Twin] Connection handshake failed: {ex.Message}");
        }
    }

    private async Task ReceiveTelemetryLoop()
    {
        byte[] buffer = new byte[1024 * 4];

        while (_webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cancellationTokenSource.Token);
                Debug.Log("[WebSocket Twin] Connection pipe closed down by host framework.");
            }
            else
            {
                string incomingMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);
                
                // Process the string data on Unity's safe main thread execution context
                ParseAndProcessData(incomingMessage);
            }
        }
    }

    /// <summary>
    /// De-serializes stream strings and updates both UI readouts and local model posture.
    /// Expects formats like: "TELEMETRY:wind,rain,solar" or a clean JSON string.
    /// </summary>
    private void ParseAndProcessData(string data)
    {
        try
        {
            // Simple robust comma separated fallback parsing method (e.g., "5.4,20,600")
            string[] tokens = data.Split(',');
            if (tokens.Length >= 3)
            {
                if (float.TryParse(tokens[0], out _currentWind))
                    windDisplay.text = $"Wind: {_currentWind:F1} m/s";

                if (float.TryParse(tokens[1], out _currentRain))
                    rainDisplay.text = $"Rain: {_currentRain:F0}%";

                if (float.TryParse(tokens[2], out _currentSolar))
                    solarDisplay.text = $"Solar: {_currentSolar:F0} W/m²";

                // Evaluate animation updates to replicate Umbrella A's status
                UpdateTwinModelPosture();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[WebSocket Twin] Data parsing syntax discrepancy: {ex.Message} on raw payload: {data}");
        }
    }

    private void UpdateTwinModelPosture()
    {
        // Mirror identical weather decision metrics to show accurate digital twin synchronization
        bool shouldBeOpen = false;

        if (_currentWind >= 12.0f) shouldBeOpen = false; // Safety override
        else if (_currentRain >= 15.0f) shouldBeOpen = true; // Hydrophobic protection shelter mode
        else shouldBeOpen = _currentSolar >= 300.0f; // Comfort mode

        _animator.SetBool("IsOpen", shouldBeOpen);
    }

    /// <summary>
    /// Converts button interactions into outbound data payloads to switch physical states.
    /// </summary>
    private async void SendControlCommand(string commandPayload)
    {
        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            byte[] sendBuffer = Encoding.UTF8.GetBytes(commandPayload);
            await _webSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            
            Debug.Log($"<color=#FFFF00><b>[WebSocket Command Sent]:</b></color> Dispatched system payload: {commandPayload}");
        }
        else
        {
            Debug.LogWarning("[WebSocket Twin] Action aborted: Network pipe is currently offline.");
        }
    }

    private void OnDestroy()
    {
        // Safe disconnection thread cleanup routine when exit transitions trigger
        if (_webSocket != null)
        {
            _webSocket.Dispose();
        }
        _cancellationTokenSource?.Cancel();
    }
}