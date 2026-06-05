using UnityEngine;
using System;
using System.Text;
using System.Collections.Concurrent; // <-- NEW: For thread-safe message dispatching
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

public class MQTTConnectionManager : MonoBehaviour
{
    public static MQTTConnectionManager Instance { get; private set; }

    // ======================================================================
    // THE MISSING LINK: Global static callback for inbound data routing
    // ======================================================================
    public static event Action<string, string> OnTelemetryMessageReceived;

    [Header("Global Public Broker Settings")] //
    [Tooltip("The public sandbox broker url.")] //
    public string brokerHost = "broker.hivemq.com"; //
    [Tooltip("Standard unencrypted MQTT port.")] //
    public int brokerPort = 1883; //

    public MqttClient Client { get; private set; } //

    // Local buffer queue to safely pass data from network threads to Unity's main graphics thread
    private readonly ConcurrentQueue<Action> _mainThreadExecutionQueue = new ConcurrentQueue<Action>();

    private void Awake() //
    {
        if (Instance == null) //
        {
            Instance = this; //
            DontDestroyOnLoad(gameObject); //
        }
        else //
        {
            Destroy(gameObject); //
        }
    }

    void Start() //
    {
        ConnectToPublicBroker(); //
    }

    void Update()
    {
        // Continuously drain background thread actions safely inside Unity's main frame loop
        while (_mainThreadExecutionQueue.TryDequeue(out Action action))
        {
            action?.Invoke();
        }
    }

    private void ConnectToPublicBroker() //
    {
        try //
        {
            // Set secure (third parameter) to false, and protocol to None to use clean TCP sockets
            Client = new MqttClient(brokerHost, brokerPort, false, null, null, MqttSslProtocols.None); //

            // NEW: Bind the low-level M2Mqtt incoming message listener event callback
            Client.MqttMsgPublishReceived += HandleRawBrokerMessageArrival;

            // Generate a highly specific Client ID so nobody else clashes with your twin instance
            string clientID = "Unity_DuoResidence_Sim_" + Guid.NewGuid().ToString().Substring(0, 8);

            // Handshake without usernames or passwords
            Client.Connect(clientID); //

            if (Client.IsConnected) //
            {
                Debug.Log($"<color=#00FF66><b>[MQTT Link]:</b></color> SUCCESS! Bidirectional infrastructure online via: {brokerHost}");
            }
        }
        catch (Exception ex) //
        {
            Debug.LogError($"<color=red><b>[MQTT Link Error]:</b></color> Connection breakdown. Exception: {ex.Message}"); //
        }
    }

    // ======================================================================
    // NEW: Global helper allowing scripts to subscribe to topics cleanly
    // ======================================================================
    public void SubscribeToTopic(string topic)
    {
        if (Client != null && Client.IsConnected)
        {
            // Register a subscription array with at-least-once Quality of Service tracking configurations
            Client.Subscribe(
                new string[] { topic }, 
                new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE }
            );
            Debug.Log($"<color=#9900FF><b>[MQTT Subscribed]:</b></color> Now listening to command channel: {topic}");
        }
        else
        {
            Debug.LogWarning($"[MQTT Subscription Voided]: Client is offline. Cannot process: {topic}");
        }
    }

    /// <summary>
    /// Global helper to send strings to our online topics
    /// </summary>
    public void PublishTopic(string topic, string payload, bool retain = false) //
    {
        if (Client != null && Client.IsConnected) //
        {
            byte[] rawData = Encoding.UTF8.GetBytes(payload); //
            Client.Publish(topic, rawData, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, retain); //
        }
        else //
        {
            Debug.LogWarning($"[MQTT Publish Voided]: Client is offline. Drop target: {topic}"); //
        }
    }

    // ======================================================================
    // NEW: Low-level background callback wrapper intercepts and queues packets
    // ======================================================================
    private void HandleRawBrokerMessageArrival(object sender, MqttMsgPublishEventArgs e)
    {
        // Safely extract the raw byte arrays back into clear readable strings
        string receivedTopic = e.Topic;
        string decodedPayload = Encoding.UTF8.GetString(e.Message);

        // Enqueue the broad execution block so it evaluates on the safe main Unity render thread
        _mainThreadExecutionQueue.Enqueue(() =>
        {
            OnTelemetryMessageReceived?.Invoke(receivedTopic, decodedPayload);
        });
    }

    private void OnApplicationQuit() //
    {
        if (Client != null)
        {
            Client.MqttMsgPublishReceived -= HandleRawBrokerMessageArrival;
            
            if (Client.IsConnected)
            {
                Client.Disconnect(); //
                Debug.Log("[MQTT Link]: Gracefully disconnected from sandbox cluster."); //
            }
        }
    }
}