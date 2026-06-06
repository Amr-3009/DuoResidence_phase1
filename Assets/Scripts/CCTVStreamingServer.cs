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
    [SerializeField] private int serverPort = 8080; 

    [Header("CCTV System Cameras")]
    public Camera camEntrance;
    public Camera camExit;
    public Camera camLanesMaster;

    [Header("Render Texture Targets")]
    public RenderTexture rtEntrance;
    public RenderTexture rtExit;
    public RenderTexture rtLanesMaster;

    [Header("Stream Quality")]
    [Range(10, 100)] [SerializeField] private int jpegQuality = 100; 

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
    
    // 🚀 PERFORMANCE FIX: Reusable frame buffer cache to eliminate Garbage Collector spikes
    private Dictionary<string, Texture2D> _allocatedTextureCache = new Dictionary<string, Texture2D>();
    
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

        Debug.Log($"[CCTV Server] Optimized streaming engine initialized on Inspector Port: {serverPort}");
    }

    private void StartHttpServer()
    {
        _listener = new HttpListener();
        
        // Configured for clean alignment with standard ngrok routing rules
        _listener.Prefixes.Add($"http://127.0.0.1:{serverPort}/");
        
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

        // 🚀 OPTIMIZATION: Check cache first to see if a matching texture container already exists
        if (!_allocatedTextureCache.TryGetValue(path, out Texture2D tex) || tex.width != rt.width || tex.height != rt.height)
        {
            if (tex != null) Destroy(tex);
            
            // Allocate memory strictly once per active camera path
            tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            _allocatedTextureCache[path] = tex;
        }

        // Read pixels directly into our recycled texture memory reference
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        byte[] jpgBytes = ImageConversion.EncodeToJPG(tex, jpegQuality);

        lock (_lockObject)
        {
            _latestFrames[path] = jpgBytes;
        }
        
        // 🚀 NOTE: Destroy(tex) removed from here! Reusable textures are preserved to protect runtime performance.
    }

    private void OnDestroy()
    {
        _isRunning = false;
        
        // 🚀 SAFE TEARDOWN: Close listener first to break background loop naturally
        if (_listener != null)
        {
            try 
            { 
                if (_listener.IsListening) _listener.Stop(); 
                _listener.Close(); 
            } 
            catch (Exception) { }
        }

        // Give the background worker thread a clean window to shut down completely
        if (_serverThread != null && _serverThread.IsAlive)
        {
            _serverThread.Join(250); 
            if (_serverThread.IsAlive) _serverThread.Abort();
        }

        // Clear out unmanaged texture memory frames explicitly on termination
        foreach (Texture2D tex in _allocatedTextureCache.Values)
        {
            if (tex != null) Destroy(tex);
        }
        _allocatedTextureCache.Clear();
    }
}