using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EncryptedChat
{
    public class ServerInfo
    {
        public string Ip { get; set; } = string.Empty;
        public string Name { get; set; } = "Unknown";
        public int Port { get; set; } = Configuration.CHAT_PORT;
        public int Users { get; set; } = 0;
        public DateTime LastSeen { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// UDP-based server discovery for LAN
    /// </summary>
    public class ServerDiscovery : IDisposable
    {
        private UdpClient? _udpClient;
        private CancellationTokenSource? _cancellationTokenSource;
        // Touched from the listen loop, the cleanup loop, and the UI thread — must be thread-safe.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ServerInfo> _servers = new();
        private bool _isRunning;

        public event Action<ServerInfo>? OnServerFound;
        public event Action<string>? OnServerLost;

        public IReadOnlyCollection<ServerInfo> Servers => _servers.Values.ToList();

        /// <summary>
        /// Start listening for server broadcasts
        /// </summary>
        public void StartListener()
        {
            if (_isRunning)
                return;

            try
            {
                _udpClient = new UdpClient(Configuration.DISCOVERY_PORT);
                _udpClient.EnableBroadcast = true;
                _cancellationTokenSource = new CancellationTokenSource();
                _isRunning = true;

                _ = Task.Run(() => ListenLoop(_cancellationTokenSource.Token));
                _ = Task.Run(() => CleanupLoop(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start discovery: {ex.Message}");
            }
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            while (_isRunning && _udpClient != null)
            {
                try
                {
                    var result = await _udpClient.ReceiveAsync(cancellationToken);
                    string json = Encoding.UTF8.GetString(result.Buffer);

                    var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (data != null && data.ContainsKey("type") &&
                        data["type"].GetString() == "server_announce")
                    {
                        var serverInfo = new ServerInfo
                        {
                            Ip = result.RemoteEndPoint.Address.ToString(),
                            Name = data.ContainsKey("name") ? data["name"].GetString() ?? "Unknown" : "Unknown",
                            Port = data.ContainsKey("port") ? data["port"].GetInt32() : Configuration.CHAT_PORT,
                            Users = data.ContainsKey("users") ? data["users"].GetInt32() : 0,
                            LastSeen = DateTime.Now
                        };

                        _servers[serverInfo.Ip] = serverInfo;
                        // Notify on every announce so the UI can refresh user counts / last-seen.
                        OnServerFound?.Invoke(serverInfo);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        private async Task CleanupLoop(CancellationToken cancellationToken)
        {
            while (_isRunning)
            {
                try
                {
                    await Task.Delay(5000, cancellationToken);

                    var now = DateTime.Now;
                    var staleServers = _servers.Where(kvp => (now - kvp.Value.LastSeen).TotalSeconds > 10).ToList();

                    foreach (var server in staleServers)
                    {
                        if (_servers.TryRemove(server.Key, out _))
                            OnServerLost?.Invoke(server.Key);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Ignore errors
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _udpClient?.Close();
            _servers.Clear();
        }

        public void Dispose()
        {
            Stop();
            _udpClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
