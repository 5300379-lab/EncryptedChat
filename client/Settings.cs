using System;
using System.IO;
using System.Text.Json;

namespace EncryptedChat
{
    /// <summary>
    /// Last-used connection details, stored locally per user so they don't have to
    /// retype everything. Lives in the OS app-data folder, never in the repo.
    /// Windows: %APPDATA%\EncryptedChat\settings.json
    /// Linux/macOS: ~/.config/EncryptedChat/settings.json (XDG)
    /// </summary>
    public class Settings
    {
        public string ServerAddress { get; set; } = string.Empty;
        public int ServerPort { get; set; } = Configuration.DEFAULT_SERVER_PORT;
        public string Username { get; set; } = string.Empty;
        public string EncryptionKey { get; set; } = string.Empty;

        private static string Dir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EncryptedChat");
        private static string FilePath => Path.Combine(Dir, "settings.json");

        public static Settings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
            }
            catch { /* ignore corrupt/unreadable settings */ }
            return new Settings();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* non-fatal */ }
        }
    }
}
