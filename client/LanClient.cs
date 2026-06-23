using System;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EncryptedChat
{
    /// <summary>
    /// TCP client for LAN mode
    /// </summary>
    public class LanClient : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private AESCipher? _cipher;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isConnected;

        public event Action<string, string>? OnMessage;
        public event Action<string>? OnStatus;
        public event Action? OnDisconnected;

        public bool IsConnected => _isConnected;

        public async Task<bool> ConnectAsync(string host, int port, string username, string encryptionKey)
        {
            try
            {
                _cipher = new AESCipher(encryptionKey);
                _tcpClient = new TcpClient();
                _cancellationTokenSource = new CancellationTokenSource();

                OnStatus?.Invoke($"Connecting to {host}:{port}...");

                await _tcpClient.ConnectAsync(host, port);
                _stream = _tcpClient.GetStream();

                // Disable Nagle's algorithm for real-time chat
                _tcpClient.NoDelay = true;

                // Enable TCP keepalive to prevent disconnections
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                // Configure keepalive: time=20s, interval=5s
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, 20);
                _tcpClient.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, 5);

                // Send join message
                var joinMsg = new
                {
                    type = "join",
                    username = username
                };
                string joinJson = JsonSerializer.Serialize(joinMsg);
                string encrypted = _cipher.Encrypt(joinJson);
                await NetworkHelpers.SendMessageAsync(_stream, encrypted, _cancellationTokenSource.Token);

                _isConnected = true;
                OnStatus?.Invoke("Connected!");

                // Start receive loop
                _ = Task.Run(() => ReceiveLoop(_cancellationTokenSource.Token));

                return true;
            }
            catch (Exception ex)
            {
                OnStatus?.Invoke($"Connection failed: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        private async Task ReceiveLoop(CancellationToken cancellationToken)
        {
            try
            {
                while (_isConnected && _stream != null)
                {
                    string? encrypted = await NetworkHelpers.ReceiveMessageAsync(_stream, cancellationToken);

                    if (encrypted == null)
                    {
                        _isConnected = false;
                        OnDisconnected?.Invoke();
                        break;
                    }

                    if (string.IsNullOrEmpty(encrypted))
                        continue; // Corrupted data, skip

                    string? decrypted = _cipher?.Decrypt(encrypted);

                    if (decrypted != null)
                    {
                        try
                        {
                            var jsonDoc = JsonDocument.Parse(decrypted);
                            var type = jsonDoc.RootElement.TryGetProperty("type", out var t)
                                ? (t.GetString() ?? "message")
                                : "message";
                            OnMessage?.Invoke(type, decrypted);
                        }
                        catch
                        {
                            // Invalid JSON, ignore
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
            }
            catch (Exception ex)
            {
                OnStatus?.Invoke($"Receive error: {ex.Message}");
                _isConnected = false;
                OnDisconnected?.Invoke();
            }
        }

        public async Task SendEncryptedAsync(object data)
        {
            if (_stream == null || _cipher == null || !_isConnected)
                return;

            try
            {
                string json = JsonSerializer.Serialize(data);
                string encrypted = _cipher.Encrypt(json);
                await NetworkHelpers.SendMessageAsync(_stream, encrypted, _cancellationTokenSource?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                OnStatus?.Invoke($"Send error: {ex.Message}");
            }
        }

        public async Task SendMessageAsync(string content, string? replyTo = null, string? imageData = null)
        {
            var msg = new
            {
                type = "message",
                content = content,
                reply_to = replyTo,
                image_data = imageData
            };
            await SendEncryptedAsync(msg);
        }

        public async Task SendTypingAsync(bool isTyping)
        {
            var msg = new { type = "typing", typing = isTyping };
            await SendEncryptedAsync(msg);
        }

        public async Task EditMessageAsync(string messageId, string newContent)
        {
            var msg = new { type = "edit_message", message_id = messageId, content = newContent };
            await SendEncryptedAsync(msg);
        }

        public async Task DeleteMessageAsync(string messageId)
        {
            var msg = new { type = "delete_message", message_id = messageId };
            await SendEncryptedAsync(msg);
        }

        public async Task SendReactionAsync(string messageId, string emoji)
        {
            var msg = new { type = "reaction", message_id = messageId, emoji = emoji };
            await SendEncryptedAsync(msg);
        }

        public async Task AuthenticateAdminAsync(string password)
        {
            var msg = new { type = "admin_auth", password = password };
            await SendEncryptedAsync(msg);
        }

        public async Task SendAdminCommandAsync(string command)
        {
            var msg = new { type = "admin_command", command = command };
            await SendEncryptedAsync(msg);
        }

        public async Task DisconnectAsync()
        {
            _isConnected = false;
            _cancellationTokenSource?.Cancel();

            if (_stream != null)
            {
                try
                {
                    await _stream.DisposeAsync();
                }
                catch { }
            }

            _tcpClient?.Close();
        }

        public void Dispose()
        {
            // Non-blocking: never .Wait() on the UI thread. DisconnectAsync is awaited
            // by the window's Closed handler before Dispose is called.
            _isConnected = false;
            try { _cancellationTokenSource?.Cancel(); } catch { }
            _stream?.Dispose();
            _tcpClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
