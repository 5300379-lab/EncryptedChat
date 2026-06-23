namespace EncryptedChat
{
    /// <summary>
    /// Application constants. No server addresses or keys are baked in — the user
    /// supplies the server address, port and encryption key at connect time.
    /// </summary>
    public static class Configuration
    {
        // LAN mode defaults (used to pre-fill the Direct Connect dialog and for discovery)
        public const int CHAT_PORT = 5555;
        public const int DISCOVERY_PORT = 5556;

        // Suggested default port for the central-server connect form (just a pre-fill hint).
        public const int DEFAULT_SERVER_PORT = 8443;

        // Message limits (must stay in sync with the server)
        public const int MAX_IMAGE_SIZE = 5 * 1024 * 1024;          // 5MB raw image
        public const int MAX_MESSAGE_LENGTH = 100 * 1024 * 1024;    // 100MB framed (LAN), supports large images

        // UI
        public const int MESSAGE_HISTORY_LIMIT = 50;
        public const int TYPING_INDICATOR_TIMEOUT = 3000; // ms
    }
}
