using Avalonia.Media;

namespace EncryptedChat
{
    /// <summary>Shared dark-theme palette (matches the XAML colours) for code-built UI.</summary>
    public static class AppPalette
    {
        public static readonly Color Bg      = Color.FromRgb(0x1a, 0x1a, 0x2e);
        public static readonly Color Panel   = Color.FromRgb(0x16, 0x21, 0x3e);
        public static readonly Color Edge    = Color.FromRgb(0x0f, 0x34, 0x60);
        public static readonly Color Accent  = Color.FromRgb(0xe9, 0x45, 0x60);
        public static readonly Color Green   = Color.FromRgb(0x00, 0xff, 0x88);
        public static readonly Color Text    = Color.FromRgb(0xea, 0xea, 0xea);
        public static readonly Color Muted   = Color.FromRgb(0x88, 0x88, 0x88);
        public static readonly Color SysBg   = Color.FromRgb(0x2f, 0x31, 0x36);
        public static readonly Color SysText = Color.FromRgb(0xb9, 0xbb, 0xbe);
        public static readonly Color Danger  = Color.FromRgb(0xf0, 0x47, 0x47);

        public static IBrush B(Color c) => new SolidColorBrush(c);

        public static readonly IBrush BgBrush     = B(Bg);
        public static readonly IBrush PanelBrush  = B(Panel);
        public static readonly IBrush EdgeBrush   = B(Edge);
        public static readonly IBrush AccentBrush = B(Accent);
        public static readonly IBrush GreenBrush  = B(Green);
        public static readonly IBrush TextBrush   = B(Text);
        public static readonly IBrush MutedBrush  = B(Muted);
    }
}
