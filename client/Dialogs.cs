using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;

namespace EncryptedChat
{
    public enum DialogButtons { Ok, YesNo }

    /// <summary>Themed message box (Avalonia has no built-in MessageBox).</summary>
    public class MessageDialog : Window
    {
        public bool Result { get; private set; }

        public MessageDialog(string message, string title, DialogButtons buttons)
        {
            Title = title;
            Width = 400;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppPalette.BgBrush;

            var text = new TextBlock
            {
                Text = message,
                Foreground = AppPalette.TextBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 20, 20, 10)
            };

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Margin = new Thickness(0, 0, 0, 20)
            };

            if (buttons == DialogButtons.YesNo)
            {
                var yes = Ui.AccentButton("Yes", 90); yes.Click += (_, _) => { Result = true; Close(); };
                var no = Ui.FlatButton("No", 90); no.Click += (_, _) => { Result = false; Close(); };
                btnPanel.Children.Add(yes);
                btnPanel.Children.Add(no);
            }
            else
            {
                var ok = Ui.AccentButton("OK", 90); ok.Click += (_, _) => { Result = true; Close(); };
                btnPanel.Children.Add(ok);
            }

            Content = new StackPanel { Children = { text, btnPanel } };
        }
    }

    public static class Dialogs
    {
        public static async Task Info(Window owner, string message, string title = "Info")
            => await new MessageDialog(message, title, DialogButtons.Ok).ShowDialog(owner);

        public static async Task Error(Window owner, string message, string title = "Error")
            => await new MessageDialog(message, title, DialogButtons.Ok).ShowDialog(owner);

        public static async Task<bool> Confirm(Window owner, string message, string title = "Confirm")
        {
            var d = new MessageDialog(message, title, DialogButtons.YesNo);
            await d.ShowDialog(owner);
            return d.Result;
        }
    }

    /// <summary>Base for small OK/Cancel input dialogs.</summary>
    public abstract class InputDialog : Window
    {
        public bool Confirmed { get; protected set; }

        protected StackPanel Root(string title, double width, double height)
        {
            Title = title;
            Width = width;
            Height = height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = AppPalette.BgBrush;
            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            Content = stack;
            return stack;
        }

        protected StackPanel ButtonRow(string okText, Action? onOk = null, string cancelText = "Cancel")
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 10,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var ok = Ui.AccentButton(okText, 110);
            ok.Click += (_, _) => { onOk?.Invoke(); Confirmed = true; Close(); };
            var cancel = Ui.FlatButton(cancelText, 110);
            cancel.Click += (_, _) => { Confirmed = false; Close(); };
            row.Children.Add(ok);
            row.Children.Add(cancel);
            return row;
        }
    }

    public class EditMessageDialog : InputDialog
    {
        private readonly TextBox _box;
        public string NewContent => _box.Text ?? string.Empty;

        public EditMessageDialog(string currentContent)
        {
            var stack = Root("Edit Message", 420, 230);
            stack.Children.Add(Ui.Label("New content:"));
            _box = Ui.Field(currentContent);
            _box.AcceptsReturn = true;
            _box.TextWrapping = TextWrapping.Wrap;
            _box.Height = 90;
            stack.Children.Add(_box);
            stack.Children.Add(ButtonRow("Save"));
        }
    }

    public class ReactionPickerDialog : InputDialog
    {
        public string? SelectedEmoji { get; private set; }
        private static readonly string[] Emojis = { "👍", "❤️", "😂", "😮", "😢", "😡", "🔥", "✅", "❌", "🎉" };

        public ReactionPickerDialog()
        {
            var stack = Root("Pick a Reaction", 300, 200);
            var wrap = new WrapPanel();
            foreach (var emoji in Emojis)
            {
                var btn = new Button
                {
                    Content = emoji,
                    FontSize = 24,
                    Width = 50,
                    Height = 50,
                    Margin = new Thickness(4),
                    Background = AppPalette.PanelBrush
                };
                btn.Click += (_, _) => { SelectedEmoji = emoji; Confirmed = true; Close(); };
                wrap.Children.Add(btn);
            }
            stack.Children.Add(wrap);
        }
    }

    public class AdminAuthDialog : InputDialog
    {
        private readonly TextBox _box;
        public string Password => _box.Text ?? string.Empty;

        public AdminAuthDialog()
        {
            var stack = Root("Admin Authentication", 360, 200);
            stack.Children.Add(Ui.Label("Admin password:"));
            _box = Ui.Password();
            stack.Children.Add(_box);
            stack.Children.Add(ButtonRow("Authenticate"));
        }
    }

    public class AdminCommandDialog : InputDialog
    {
        private readonly TextBox _box;
        public string Command => _box.Text ?? string.Empty;

        public AdminCommandDialog()
        {
            var stack = Root("Admin Command", 460, 260);
            stack.Children.Add(new TextBlock
            {
                Text = "Commands: users, kick <user>, ban <user>, unban <user>, bans, broadcast <msg>, stats, export, clear",
                TextWrapping = TextWrapping.Wrap,
                Foreground = AppPalette.MutedBrush,
                Margin = new Thickness(0, 0, 0, 8)
            });
            stack.Children.Add(Ui.Label("Command:"));
            _box = Ui.Field();
            stack.Children.Add(_box);
            stack.Children.Add(ButtonRow("Send"));
        }
    }

    public class DirectConnectDialog : InputDialog
    {
        private readonly TextBox _ip, _port, _user, _key;
        public string IpAddress => _ip.Text ?? string.Empty;
        public int Port => int.TryParse(_port.Text, out var p) ? p : Configuration.CHAT_PORT;
        public string Username => _user.Text ?? string.Empty;
        public string EncryptionKey => _key.Text ?? string.Empty;

        public DirectConnectDialog()
        {
            var stack = Root("Direct Connect", 400, 360);
            stack.Children.Add(Ui.Label("IP address:"));
            _ip = Ui.Field(watermark: "192.168.1.100"); stack.Children.Add(_ip);
            stack.Children.Add(Ui.Label("Port:"));
            _port = Ui.Field(Configuration.CHAT_PORT.ToString()); stack.Children.Add(_port);
            stack.Children.Add(Ui.Label("Username:"));
            _user = Ui.Field(watermark: "Your name"); stack.Children.Add(_user);
            stack.Children.Add(Ui.Label("Encryption key:"));
            _key = Ui.Password(); stack.Children.Add(_key);
            stack.Children.Add(ButtonRow("Connect"));
        }
    }

    public class LanCredentialsDialog : InputDialog
    {
        private readonly TextBox _user, _key;
        public string Username => _user.Text ?? string.Empty;
        public string EncryptionKey => _key.Text ?? string.Empty;

        public LanCredentialsDialog()
        {
            var stack = Root("Enter Credentials", 400, 240);
            stack.Children.Add(Ui.Label("Username:"));
            _user = Ui.Field(watermark: "Your name"); stack.Children.Add(_user);
            stack.Children.Add(Ui.Label("Encryption key:"));
            _key = Ui.Password(); stack.Children.Add(_key);
            stack.Children.Add(ButtonRow("Connect"));
        }
    }
}
