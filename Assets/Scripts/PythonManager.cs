using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;
using UnityEngine;

public class PythonManager : MonoBehaviour
{
    public static PythonManager instance;
    Process pythonProcess;
    RequestSocket client;
    Thread clientThread;
    Thread pythonThread;
    float minRe;
    public float MinRe { get; set; }
    float maxRe;
    public float MaxRe { get; set; }
    float minIm;
    public float MinIm { get; set; }
    float maxIm;
    public float MaxIm { get; set; }
    float zoomLevel;
    public float ZoomLevel { get; set; }

    bool receivedReply = false;
    string replyMessage = string.Empty;
    string requestMessage = string.Empty;
    float prevZoomLevel;
    volatile bool _shouldStop;
    bool zoomOnce = false;
    bool terminationMessageSent = false;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this);
        }

    }
    private void Start()
    {
        minRe = -2.5f;
        maxRe = 1.5f;
        minIm = -2f;
        maxIm = 2f;
        zoomLevel = 1f;
        prevZoomLevel = 1f;

        receivedReply = false;
        replyMessage = string.Empty;

        pythonThread = new Thread(StartPythonProcess);
        pythonThread.Start();
        clientThread = new Thread(SendMessageToPython);
        clientThread.Start();

    }

    private void Update()
    {
        if (client != null)
        {
            if (!_shouldStop && !zoomOnce)
            {
                Zoom(1);
                zoomOnce = true;
                RequestStop();
            }
            if (receivedReply)
            {
                UnityEngine.Debug.Log("received reply");
                UnityEngine.Debug.Log(replyMessage);
                receivedReply = false;

                if (_shouldStop)
                {
                    UnityEngine.Debug.Log("waiting for clientThread to join");
                    clientThread.Join();
                    //UnityEngine.Debug.Log("thread joined");
                }
                /*            var data = JsonConvert.DeserializeObject<MandelbrotBounds>(message);

                            minRe = float.Parse(data.new_min_re);
                            maxRe = float.Parse(data.new_max_re);
                            minIm = float.Parse(data.new_min_im);
                            maxIm = float.Parse(data.new_max_im);*/

            }
        }


    }

    void SendMessageToPython()
    {
        UnityEngine.Debug.Log("send message thread started");
        try
        {
            UnityEngine.Debug.Log("setting client");
            AsyncIO.ForceDotNet.Force();
            client = new RequestSocket(">tcp://localhost:5555");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"NetMQ initialization error: {ex.Message}");
        }
        while (!_shouldStop)
        {
            if (client != null && requestMessage != String.Empty)
            {
                client.SendFrame(requestMessage);
                if (client.TryReceiveFrameString(out replyMessage))
                {
                    receivedReply = true;
                }
            }
        }

    }

    void RequestStop()
    {
        _shouldStop = true;
    }

    void RequestStart()
    {
        _shouldStop = false;
    }

    public void Zoom(float zoomFactor)
    {

        zoomLevel *= zoomFactor;

        /*        var message = new
                {
                    min_re = minRe.ToString(),
                    max_re = maxRe.ToString(),
                    min_im = minIm.ToString(),
                    max_im = maxIm.ToString(),
                    zoomLevel = zoomLevel.ToString()
                };
                string jsonMessage = JsonConvert.SerializeObject(message, Formatting.Indented);*/

        string message = "Hello World";
        requestMessage = JsonConvert.SerializeObject(message);

        UnityEngine.Debug.Log("zoom method called");

    }

    void StartPythonProcess()
    {
        UnityEngine.Debug.Log("begin start python process");
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;


        string pythonPath = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        string scriptPath = Path.Combine(Application.dataPath, "Scripts", "Python", "mandelbrot_calculations.py");

        if (!File.Exists(pythonPath))
        {
            UnityEngine.Debug.LogError($"Python executable not found at: {pythonPath}");
            return;
        }

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError($"Python script not found at: {scriptPath}");
            return;
        }

        pythonProcess = new Process();
        pythonProcess.StartInfo.FileName = pythonPath;
        pythonProcess.StartInfo.Arguments = scriptPath;
        pythonProcess.StartInfo.UseShellExecute = false;
        pythonProcess.StartInfo.CreateNoWindow = true;
        pythonProcess.StartInfo.RedirectStandardOutput = true;
        pythonProcess.StartInfo.RedirectStandardError = true;

        pythonProcess.OutputDataReceived += (sender, args) => UnityEngine.Debug.Log(args.Data);
        pythonProcess.ErrorDataReceived += (sender, args) => UnityEngine.Debug.LogError(args.Data);

        pythonProcess.Start();
        pythonProcess.BeginOutputReadLine();
        pythonProcess.BeginErrorReadLine();

        UnityEngine.Debug.Log("python process started");
    }

    [Serializable]
    public class MandelbrotBounds
    {
        public string new_min_re;
        public string new_max_re;
        public string new_min_im;
        public string new_max_im;
    }

    private void OnDestroy()
    {

        if (client != null && !terminationMessageSent)
        {
            var message = new { terminate = true };
            string jsonMessage = JsonConvert.SerializeObject(message);

            try
            {
                client.SendFrame(jsonMessage);
                terminationMessageSent = true;
            }

            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"Failed to send termination message: {e.Message}");
            }


            client.Close();
            client.Dispose();
            client = null;
        }
        if (clientThread != null)
        {
            clientThread.Join();
            clientThread = null;
        }
        if (pythonProcess != null)
        {
            UnityEngine.Debug.Log("kill python");
            pythonProcess.Kill();
            pythonProcess.Dispose();
            pythonProcess = null;
        }
        if (pythonThread != null)
        {
            pythonThread.Join();
            pythonThread = null;
        }
    }
}
