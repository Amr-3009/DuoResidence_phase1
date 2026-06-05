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
    [SerializeField] private int serverPort = 8080; // 🚀 RESTORED: Visible in the Inspector

    [Header("CCTV System Cameras")]
    public Camera camEntrance;
    public Camera camExit;
    public Camera camLanesMaster;

    [Header("Render Texture Targets")]
    public RenderTexture rtEntrance;
    public RenderTexture rtExit;
    public RenderTexture rtLanesMaster;

    [Header("Stream Quality")]
    [Range(10, 100)] [SerializeField] private int jpegQuality = 60; 

    private HttpListener _listener;
    private Thread _serverThread;
    private bool _isRunning = false;

    // Tracking endpoints for all 3 stream paths
    private Dictionary<string, int> _activeStreams = new Dictionary<string, int>()
    {
        { "/entrance", 0 },
        { "/exit", 0 },
        { "/lanes", 0 }
    };

    private Dictionary<string, byte[]> _latestFrames = new Dictionary<string, byte[]>();
    private readonly object _lockObject = new object();

    private void Start()
    {
        // Prevent background rendering overhead when no clients are connected
        if (camEntrance != null) camEntrance.enabled = false;
        if (camExit != null) camExit.enabled = false;
        if (camLanesMaster != null) camLanesMaster.enabled = false;

        _latestFrames["/entrance"] = null;
        _latestFrames["/exit"] = null;
        _latestFrames["/lanes"] = null;

        _isRunning = true;
        _serverThread = new Thread(StartHttpServer);
        _serverThread.IsBackground = true;
        _serverThread.Start();

        Debug.Log($"[CCTV Server] Streaming engine initialized on Inspector Port: {serverPort}");
    }

    private void StartHttpServer()
    {
        _listener = new HttpListener();
        
        // 🚀 FIXED: Binds strictly to your serialized serverPort field
        _listener.Prefixes.Add($"http://localhost:{serverPort}/");
        
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

        if (!_activeStreams.ContainsKey(path))
        {
            response.StatusCode = (int)HttpStatusCode.NotFound;
            response.Close();
            return;
        }

        lock (_lockObject) { _activeStreams[path]++; }

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
                    string header = $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {currentFrame.Length}\r\n\r\n";
                    byte[] headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
                    
                    outputStream.Write(headerBytes, 0, headerBytes.Length);
                    outputStream.Write(currentFrame, 0, currentFrame.Length);
                    
                    byte[] footerBytes = System.Text.Encoding.ASCII.GetBytes("\r\n");
                    outputStream.Write(footerBytes, 0, footerBytes.Length);
                    outputStream.Flush();
                }

                Thread.Sleep(60); 
            }
        }
        catch (Exception)
        {
            // Client closed connection safely
        }
        finally
        {
            lock (_lockObject) 
            { 
                _activeStreams[path] = Math.Max(0, _activeStreams[path] - 1); 
            }
            try { response.Close(); } catch { }
        }
    }

    private void LateUpdate()
    {
        if (_activeStreams["/entrance"] > 0) CaptureFrame("/entrance", camEntrance, rtEntrance);
        if (_activeStreams["/exit"] > 0) CaptureFrame("/exit", camExit, rtExit);
        if (_activeStreams["/lanes"] > 0) CaptureFrame("/lanes", camLanesMaster, rtLanesMaster);
    }

    private void CaptureFrame(string path, Camera cam, RenderTexture rt)
    {
        if (cam == null || rt == null) return;

        cam.Render();

        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        // Uses the engine's local jpegQuality setting
        byte[] jpgBytes = ImageConversion.EncodeToJPG(tex, jpegQuality);

        lock (_lockObject)
        {
            _latestFrames[path] = jpgBytes;
        }

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