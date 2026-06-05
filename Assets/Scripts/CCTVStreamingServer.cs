using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CCTVStreamingServer : MonoBehaviour
{
    [Header("Port Configuration")]
    public int serverPort = 8080;

    [Header("Cameras")]
    public Camera camEntrance;
    public Camera camExit;
    public Camera camLanesMaster;

    [Header("Render Textures")]
    public RenderTexture rtEntrance;
    public RenderTexture rtExit;
    public RenderTexture rtLanesMaster;

    [Header("Stream Settings")]
    [Range(10, 100)] public int jpegQuality = 60; // 60% is perfect crisp balance for mobile

    private HttpListener _listener;
    private Thread _serverThread;
    private bool _isRunning = false;

    // Thread-safe dictionary to keep track of active stream paths and their viewer counts
    private Dictionary<string, int> _activeStreams = new Dictionary<string, int>()
    {
        { "/entrance", 0 },
        { "/exit", 0 },
        { "/lanes", 0 }
    };

    // Cached byte arrays containing the latest encoded frame for each stream
    private Dictionary<string, byte[]> _latestFrames = new Dictionary<string, byte[]>();
    private readonly object _lockObject = new object();

    private void Start()
    {
        // Force all streaming cameras completely off by default to save GPU power
        if (camEntrance != null) camEntrance.enabled = false;
        if (camExit != null) camExit.enabled = false;
        if (camLanesMaster != null) camLanesMaster.enabled = false;

        // Initialize frame slots
        _latestFrames["/entrance"] = null;
        _latestFrames["/exit"] = null;
        _latestFrames["/lanes"] = null;

        // Start the background network listener thread
        _isRunning = true;
        _serverThread = new Thread(StartHttpServer);
        _serverThread.IsBackground = true;
        _serverThread.Start();

        Debug.Log($"[CCTV Server] Streaming engine initialized on port {serverPort}");
    }

    private void StartHttpServer()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://*:{serverPort}/");
        
        try
        {
            _listener.Start();
            while (_isRunning)
            {
                HttpListenerContext context = _listener.GetContext();
                ThreadPool.QueueUserWorkItem(HandleClientConnection, context);
            }
        }
        catch (Exception ex)
        {
            if (_isRunning) Debug.LogError($"[CCTV Server] Listener Error: {ex.Message}");
        }
    }

    private void HandleClientConnection(object state)
    {
        HttpListenerContext context = (HttpListenerContext)state;
        HttpListenerResponse response = context.Response;
        string path = context.Request.Url.AbsolutePath.ToLower();

        // Validate that the requested path is one of our 3 streams
        if (!_activeStreams.ContainsKey(path))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        // Increment viewer count for this stream path
        lock (_lockObject) { _activeStreams[path]++; }

        // Set up the standard industrial MJPEG streaming network headers
        response.ContentType = "multipart/x-mixed-replace; boundary=--frame";
        response.StatusCode = (int)HttpStatusCode.OK;
        Stream outputStream = response.OutputStream;

        try
        {
            while (_isRunning && response.OutputStream.CanWrite)
            {
                byte[] currentFrame = null;

                lock (_lockObject)
                {
                    currentFrame = _latestFrames[path];
                }

                if (currentFrame != null)
                {
                    // Write frame boundaries for the browser engine to decode
                    string header = $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {currentFrame.Length}\r\n\r\n";
                    byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
                    
                    outputStream.Write(headerBytes, 0, headerBytes.Length);
                    outputStream.Write(currentFrame, 0, currentFrame.Length);
                    
                    byte[] footerBytes = System.Text.Encoding.ASCII.GetBytes("\r\n");
                    outputStream.Write(footerBytes, 0, footerBytes.Length);
                    outputStream.Flush();
                }

                // Match a smooth mobile viewing frame rate (approx ~15-20 FPS)
                Thread.Sleep(60); 
            }
        }
        catch (Exception)
        {
            // Client closed the app tab or disconnected
        }
        finally
        {
            // Client left: decrement viewers safely
            lock (_lockObject) 
            { 
                _activeStreams[path] = Math.Max(0, _activeStreams[path] - 1); 
            }
            try { response.Close(); } catch { }
        }
    }

    // LateUpdate handles gathering frames immediately after Unity finishes computing vehicle positions
    private void LateUpdate()
    {
        // Only render and encode IF someone is actively streaming a specific camera path
        if (_activeStreams["/entrance"] > 0) CaptureFrame("/entrance", camEntrance, rtEntrance);
        if (_activeStreams["/exit"] > 0) CaptureFrame("/exit", camExit, rtExit);
        if (_activeStreams["/lanes"] > 0) CaptureFrame("/lanes", camLanesMaster, rtLanesMaster);
    }

    private void CaptureFrame(string path, Camera cam, RenderTexture rt)
    {
        if (cam == null || rt == null) return;

        // 1. Manually force the specific camera to render a single frame into its RenderTexture asset
        cam.Render();

        // 2. Read the active pixels out of the texture
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // 3. Encode the pixels straight into a lightweight JPEG byte block
        byte[] jpgBytes = ImageConversion.EncodeToJPG(tex, jpegQuality);

        // 4. Update the frame buffer safely for our background server threads to broadcast
        lock (_lockObject)
        {
            _latestFrames[path] = jpgBytes;
        }

        // Clean up unmanaged texture data instantly to keep memory clear
        Destroy(tex);
    }

    private void OnDestroy()
    {
        _isRunning = false;
        if (_listener != null && _listener.IsListening)
        {
            _listener.Stop();
            _listener.Close();
        }
        if (_serverThread != null && _serverThread.IsAlive)
        {
            _serverThread.Abort();
        }
    }
}