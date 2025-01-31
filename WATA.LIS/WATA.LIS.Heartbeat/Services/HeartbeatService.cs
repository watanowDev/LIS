using System;
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
    }
}