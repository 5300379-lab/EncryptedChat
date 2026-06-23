using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EncryptedChat
{
    /// <summary>
    /// Network helper methods for message framing
    /// Implements 4-byte length prefix framing protocol (compatible with Python)
    /// </summary>
    public static class NetworkHelpers
    {
        /// <summary>
        /// Send a message with 4-byte length prefix (big-endian)
        /// </summary>
        public static async Task SendMessageAsync(NetworkStream stream, string data, CancellationToken cancellationToken = default)
        {
            byte[] encoded = Encoding.UTF8.GetBytes(data);
            byte[] length = BitConverter.GetBytes(encoded.Length);

            // Convert to big-endian (network byte order) - Python uses big-endian
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(length);
            }

            // Send length (4 bytes) + data
            await stream.WriteAsync(length, 0, 4, cancellationToken);
            await stream.WriteAsync(encoded, 0, encoded.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }

        /// <summary>
        /// Receive a message with 4-byte length prefix (big-endian)
        /// </summary>
        public static async Task<string?> ReceiveMessageAsync(NetworkStream stream, CancellationToken cancellationToken = default)
        {
            try
            {
                // Read 4-byte length prefix
                byte[] lengthData = new byte[4];
                int bytesRead = 0;
                while (bytesRead < 4)
                {
                    int count = await stream.ReadAsync(lengthData, bytesRead, 4 - bytesRead, cancellationToken);
                    if (count == 0)
                        return null; // Connection closed
                    bytesRead += count;
                }

                // Convert from big-endian
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthData);
                }
                int messageLength = BitConverter.ToInt32(lengthData, 0);

                // Validate message length
                if (messageLength <= 0 || messageLength > Configuration.MAX_MESSAGE_LENGTH)
                {
                    return null;
                }

                // Read message data
                byte[] data = new byte[messageLength];
                bytesRead = 0;
                while (bytesRead < messageLength)
                {
                    int count = await stream.ReadAsync(data, bytesRead,
                        Math.Min(messageLength - bytesRead, 65536), cancellationToken);
                    if (count == 0)
                        return null; // Connection closed
                    bytesRead += count;
                }

                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
