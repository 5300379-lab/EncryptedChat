using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EncryptedChat
{
    /// <summary>
    /// Represents a chat message
    /// </summary>
    public class Message
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("msg_type")]
        public string MsgType { get; set; } = "text";

        [JsonPropertyName("reply_to")]
        public string? ReplyTo { get; set; }

        [JsonPropertyName("image_data")]
        public string? ImageData { get; set; }

        [JsonPropertyName("reactions")]
        public Dictionary<string, List<string>> Reactions { get; set; } = new();

        [JsonPropertyName("edited")]
        public bool Edited { get; set; } = false;

        [JsonPropertyName("edited_by")]
        public string? EditedBy { get; set; }

        [JsonPropertyName("deleted")]
        public bool Deleted { get; set; } = false;

        [JsonPropertyName("timestamp")]
        public double Timestamp { get; set; }

        public Message()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public Message(string id, string username, string content, string msgType = "text",
                      string? replyTo = null, string? imageData = null)
        {
            Id = id;
            Username = username;
            Content = content;
            MsgType = msgType;
            ReplyTo = replyTo;
            ImageData = imageData;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public DateTime GetDateTime()
        {
            return DateTimeOffset.FromUnixTimeSeconds((long)Timestamp).LocalDateTime;
        }
    }
}
