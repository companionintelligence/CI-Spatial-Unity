using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace CompanionScan.Replay.Tests
{
    /// <summary>
    /// A CV server on a loopback port that answers the three image routes with canned results and
    /// records every request, so a test can see what the app sent (and that it sent nothing). It speaks
    /// the same JSON as cv-server: boxes are corners as fractions of the image.
    /// </summary>
    internal sealed class MockCvServer : IDisposable
    {
        internal sealed class Request
        {
            public string Path;
            public string Authorization;
            public string Body;

            public JObject Json => JObject.Parse(Body);
        }

        private readonly HttpListener _listener = new HttpListener();
        private readonly object _lock = new object();
        private readonly List<Request> _requests = new List<Request>();
        private readonly string _token;
        private readonly Thread _thread;
        private volatile bool _stopped;
        private int _detectCalls;

        public MockCvServer(string requiredToken = null)
        {
            _token = requiredToken;
            int port = FreePort();
            Url = "http://127.0.0.1:" + port;
            _listener.Prefixes.Add(Url + "/");
            _listener.Start();
            _thread = new Thread(Serve) { IsBackground = true, Name = "MockCvServer" };
            _thread.Start();
        }

        public string Url { get; }

        /// <summary>What <c>/v1/detect</c> returns, by how many detect calls came before it (0 for the first).</summary>
        public Func<int, DetectionReply[]> Detections { get; set; } = _ => Array.Empty<DetectionReply>();

        /// <summary>What Florence's object detection (<c>&lt;OD&gt;</c>) lists, which becomes GroundingDINO's prompt.</summary>
        public string[] FlorenceLabels { get; set; } = Array.Empty<string>();

        public List<Request> Requests
        {
            get
            {
                lock (_lock)
                {
                    return new List<Request>(_requests);
                }
            }
        }

        public List<string> Paths => Requests.ConvertAll(request => request.Path);

        public void Dispose()
        {
            _stopped = true;
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (Exception)
            {
                // Already closed.
            }
        }

        private void Serve()
        {
            while (!_stopped)
            {
                HttpListenerContext context;
                try
                {
                    context = _listener.GetContext();
                }
                catch (Exception)
                {
                    return; // the listener was stopped
                }

                try
                {
                    Handle(context);
                }
                catch (Exception)
                {
                    try
                    {
                        context.Response.StatusCode = 500;
                        context.Response.Close();
                    }
                    catch (Exception)
                    {
                        // The client went away.
                    }
                }
            }
        }

        private void Handle(HttpListenerContext context)
        {
            string body;
            using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            var request = new Request
            {
                Path = context.Request.Url.AbsolutePath,
                Authorization = context.Request.Headers["Authorization"],
                Body = body
            };
            lock (_lock)
            {
                _requests.Add(request);
            }

            if (_token != null && request.Authorization != "Bearer " + _token)
            {
                Reply(context, 401, new JObject { ["detail"] = "bad token" });
                return;
            }

            switch (request.Path)
            {
                case "/v1/florence":
                    Reply(context, 200, FlorenceReply(request.Json));
                    break;
                case "/v1/detect":
                    int call = Interlocked.Increment(ref _detectCalls) - 1;
                    Reply(context, 200, DetectReply(Detections(call)));
                    break;
                case "/v1/segment":
                    Reply(context, 200, SegmentReply(Detections(Math.Max(0, _detectCalls - 1))));
                    break;
                case "/v1/health":
                    Reply(context, 200, new JObject { ["status"] = "ok" });
                    break;
                default:
                    Reply(context, 404, new JObject { ["detail"] = "no such route" });
                    break;
            }
        }

        private JObject FlorenceReply(JObject request)
        {
            var task = (string)request["task"];
            if (task == "<OD>")
            {
                return new JObject
                {
                    ["task"] = task,
                    ["results"] = new JObject { ["labels"] = new JArray(FlorenceLabels), ["bboxes"] = new JArray() }
                };
            }

            return new JObject { ["task"] = task, ["text"] = "an empty room", ["results"] = new JObject() };
        }

        private static JObject DetectReply(DetectionReply[] detections)
        {
            var reply = new JObject { ["bboxes"] = new JArray(), ["labels"] = new JArray(), ["scores"] = new JArray() };
            foreach (var detection in detections)
            {
                ((JArray)reply["bboxes"]).Add(new JArray(detection.Box));
                ((JArray)reply["labels"]).Add(detection.Label);
                ((JArray)reply["scores"]).Add(detection.Score);
            }

            return reply;
        }

        private static JObject SegmentReply(DetectionReply[] detections)
        {
            var annotations = new JArray();
            foreach (var detection in detections)
            {
                annotations.Add(new JObject
                {
                    ["label"] = detection.Label,
                    ["score"] = detection.Score,
                    ["bbox"] = new JArray(detection.Box),
                    ["mask"] = null
                });
            }

            return new JObject { ["annotations"] = annotations, ["image_size"] = new JArray(480, 640) };
        }

        private static void Reply(HttpListenerContext context, int status, JObject json)
        {
            var bytes = Encoding.UTF8.GetBytes(json.ToString(Newtonsoft.Json.Formatting.None));
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }

        private static int FreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }
    }

    /// <summary>One detection the mock server reports, with its box in pixels of a 640 x 480 image.</summary>
    internal sealed class DetectionReply
    {
        public string Label;
        public float Score;
        public float[] Box;

        public static DetectionReply InPixels(string label, float score, float x1, float y1, float x2, float y2)
        {
            return new DetectionReply
            {
                Label = label,
                Score = score,
                // The CV server's boxes are corners as fractions of the image.
                Box = new[] { x1 / 640f, y1 / 480f, x2 / 640f, y2 / 480f }
            };
        }
    }
}
