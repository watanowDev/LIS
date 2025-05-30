using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WATA.LIS.Heartbeat.Services
{
    public class HeartbeatService
    {
        private readonly ILogger<HeartbeatService> _logger;
        private readonly int _heartbeatInterval;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly HttpClient _httpClient;

        public HeartbeatService(ILogger<HeartbeatService> logger, int heartbeatInterval = 1000)
        {
            _logger = logger;
            _heartbeatInterval = heartbeatInterval;
            _cancellationTokenSource = new CancellationTokenSource();
            _httpClient = new HttpClient();
        }

        public void Start()
        {
            Task.Run(async () => await SendHeartbeat(_cancellationTokenSource.Token));
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
        }

        private async Task SendHeartbeat(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool isSent = await SendHeartbeatSignal();
                if (!isSent)
                {
                    _logger.LogWarning("Failed to send heartbeat signal.");
                }
                await Task.Delay(_heartbeatInterval, cancellationToken);
            }
        }

        private async Task<bool> SendHeartbeatSignal()
        {
            try
            {
                var content = new StringContent("{\"status\":\"alive\"}", Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("http://localhost:8080/heartbeat", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send heartbeat signal.");
                return false;
            }
        }

        private async Task HandleRequestsAsync(ProgramInfo program, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await program.HttpListener.GetContextAsync();
                if (context.Request.Url.AbsolutePath == "/heartbeat" && context.Request.HttpMethod == "POST")
                {
                    program.LastHeartbeatReceived = DateTime.Now;
                    Console.WriteLine($"Heartbeat received from program '{program.Name}' on port {program.Port} at: {_timestamp}");
                    _logger.LogInformation("Heartbeat received from program '{ProgramName}' on port {Port} at: {Time}", program.Name, program.Port, _timestamp);

                    var response = Encoding.UTF8.GetBytes("OK");
                    context.Response.ContentLength64 = response.Length;
                    await context.Response.OutputStream.WriteAsync(response, 0, response.Length, cancellationToken);
                    context.Response.OutputStream.Close();
                }
                else if (context.Request.Url.AbsolutePath == "/heartbeat-signal" && context.Request.HttpMethod == "POST" && program.Port == 8080)
                {
                    // 8080 포트에서 /heartbeat-signal 요청 처리
                    Console.WriteLine($"Heartbeat signal received on port 8080 at: {_timestamp}");
                    _logger.LogInformation("Heartbeat signal received on port 8080 at: {Time}", _timestamp);

                    var response = Encoding.UTF8.GetBytes("Signal received");
                    context.Response.ContentLength64 = response.Length;
                    await context.Response.OutputStream.WriteAsync(response, 0, response.Length, cancellationToken);
                    context.Response.OutputStream.Close();
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                }
            }
        }
        private string _timestamp => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        private class ProgramInfo
        {
            public string Name { get; }
            public int Port { get; }
            public HttpListener HttpListener { get; set; }
            public DateTime LastHeartbeatReceived { get; set; }

            public ProgramInfo(string name, int port)
            {
                Name = name;
                Port = port;
                LastHeartbeatReceived = DateTime.Now;
            }
        }
    }
}