using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace EncryptedChat
{
    /// <summary>Small factory helpers for building themed controls in code.</summary>
    public static class Ui
    {
        public static Button AccentButton(string content, double width = double.NaN) => new Button
        {
            Content = content,
            Background = AppPalette.AccentBrush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 8),
            Width = width,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        public static Button FlatButton(string content, double width = double.NaN) => new Button
        {
            Content = content,
            Background = AppPalette.EdgeBrush,
            Foreground = AppPalette.TextBrush,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 8),
            Width = width,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        public static TextBlock Label(string text) => new TextBlock
        {
            Text = text,
            Foreground = AppPalette.TextBrush,
            Margin = new Thickness(0, 0, 0, 4)
        };

        public static TextBox Field(string? text = null, string? watermark = null) => new TextBox
        {
            Text = text ?? string.Empty,
            Watermark = watermark,
            Background = AppPalette.PanelBrush,
            Foreground = AppPalette.TextBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = AppPalette.EdgeBrush,
            Padding = new Thickness(8, 6)
        };

        public static TextBox Password(string? text = null)
        {
            var t = Field(text);
            t.PasswordChar = '•';
            return t;
        }
    }
}
