using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEngine;
using NetMQ;
using NetMQ.Sockets;
using Newtonsoft.Json;

using Debug = UnityEngine.Debug;

public class MandelbrotZoom : MonoBehaviour
{
    private Process pythonProcess;
    private RequestSocket client;
    private Thread clientThread;
    private bool receivedReply;
    private string replyMessage;

    private float minRe;
    private float maxRe;
    private float minIm;
    private float maxIm;
    private float zoomLevel;

    private bool terminationMessageSent = false;

    void Start()
    {
/*        receivedReply = false;
        replyMessage = string.Empty;

        // Start the Python process
        if (!StartPythonProcess())
        {
            Debug.LogError("Failed to start Python process. Check if the script path is correct.");
            return;
        }

        // Initialize NetMQ client
        clientThread = new Thread(() =>
        {
            try
            {
                AsyncIO.ForceDotNet.Force();
                client = new RequestSocket(">tcp://localhost:5556");  // Matching port with Python script
            }
            catch (Exception ex)
            {
                Debug.LogError($"NetMQ initialization error: {ex.Message}");
            }
        });
        clientThread.Start();

        // Initial Mandelbrot set boundaries
        minRe = -2.5f;
        maxRe = 1.0f;
        minIm = -1.0f;
        maxIm = 1.0f;
        zoomLevel = 1.0f;*/
    }

    void Update()
    {
       /* if (Input.mouseScrollDelta.y != 0 && !receivedReply)
        {
            float zoomFactor = 1 + Input.mouseScrollDelta.y * 0.1f;
            Zoom(zoomFactor);
        }

        if (receivedReply)
        {
            var data = JsonConvert.DeserializeObject<MandelbrotBounds>(replyMessage);
            minRe = float.Parse(data.new_min_re);
            maxRe = float.Parse(data.new_max_re);
            minIm = float.Parse(data.new_min_im);
            maxIm = float.Parse(data.new_max_im);
            receivedReply = false;
            Debug.Log($"New Bounds: minRe={minRe}, maxRe={maxRe}, minIm={minIm}, maxIm={maxIm}");
        }*/
    }

    void Zoom(float zoomFactor)
    {
        zoomLevel *= zoomFactor;

        var message = new
        {
            min_re = minRe.ToString(),
            max_re = maxRe.ToString(),
            min_im = minIm.ToString(),
            max_im = maxIm.ToString(),
            zoom_level = zoomLevel.ToString()
        };
        string jsonMessage = JsonConvert.SerializeObject(message);

        if (client != null)
        {
            try
            {
                client.SendFrame(jsonMessage);
                if (client.TryReceiveFrameString(out replyMessage))
                {
                    receivedReply = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during NetMQ communication: {ex.Message}");
            }
        }
    }

    bool StartPythonProcess()
    {
        // Get the root path of the Unity project
        string projectRootPath = Directory.GetParent(Application.dataPath).FullName;
        // Construct the relative path to the Python script
        string pythonScriptPath = Path.Combine(projectRootPath, "Assets", "Scripts", "Python", "mandelbrot_calculations.py");

        if (!File.Exists(pythonScriptPath))
        {
            Debug.LogError($"Python script not found at: {pythonScriptPath}");
            return false;
        }

        pythonProcess = new Process();
        pythonProcess.StartInfo.FileName = "python"; // Ensure 'python' is in your PATH or provide the full path to python.exe
        pythonProcess.StartInfo.Arguments = $"\"{pythonScriptPath}\"";
        pythonProcess.StartInfo.UseShellExecute = false;
        pythonProcess.StartInfo.CreateNoWindow = true;
        pythonProcess.StartInfo.RedirectStandardOutput = true;
        pythonProcess.StartInfo.RedirectStandardError = true;

        pythonProcess.OutputDataReceived += new DataReceivedEventHandler(OnOutputDataReceived);
        pythonProcess.ErrorDataReceived += new DataReceivedEventHandler(OnErrorDataReceived);

        try
        {
            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start Python process: {ex.Message}");
            return false;
        }

        return true;
    }

    void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Debug.Log(e.Data);
        }
    }

    void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Data))
        {
            Debug.LogError(e.Data);
        }
    }

    private void OnDisable()
    {
        if (client != null)
        {
            // Send termination message to Python process
            if (!terminationMessageSent)
            {
                var message = new { terminate = true };
                string jsonMessage = JsonConvert.SerializeObject(message);

                try
                {
                    client.SendFrame(jsonMessage);
                    terminationMessageSent = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to send termination message to Python process:" + ex.Message);
                }
            }

            client.Close();
            client = null;
        }
        if (clientThread != null)
        {
            clientThread.Abort();
            clientThread = null;
        }
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
            pythonProcess = null;
        }
    }

    void OnDestroy()
    {
        if (client != null)
        {
            // Send termination message to Python process
            if (!terminationMessageSent)
            {
                var message = new { terminate = true };
                string jsonMessage = JsonConvert.SerializeObject(message);

                try
                {
                    client.SendFrame(jsonMessage);
                    terminationMessageSent = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError("Failed to send termination message to Python process:" + ex.Message);
                }
            }

            client.Close();
            client = null;
        }
        if (clientThread != null)
        {
            clientThread.Abort();
            clientThread = null;
        }
        if (pythonProcess != null && !pythonProcess.HasExited)
        {
            pythonProcess.Kill();
            pythonProcess.Dispose();
            pythonProcess = null;
        }
    }

    [Serializable]
    public class MandelbrotBounds
    {
        public string new_min_re;
        public string new_max_re;
        public string new_min_im;
        public string new_max_im;
    }
}