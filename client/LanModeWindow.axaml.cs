using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace EncryptedChat
{
    public partial class LanModeWindow : Window
    {
        public bool ChatOpened { get; private set; }

        private readonly Window _menu;
        private ServerDiscovery? _discovery;

        public LanModeWindow(Window menu)
        {
            InitializeComponent();
            _menu = menu;

            IpLabel.Text = $"📡 Your LAN IP: {GetLocalIP()}";

            StartDiscovery();
            Closed += (_, _) => _discovery?.Dispose();
        }

        private void StartDiscovery()
        {
            _discovery = new ServerDiscovery();
            _discovery.OnServerFound += Discovery_OnServerFound;
            _discovery.StartListener();
        }

        private void Discovery_OnServerFound(ServerInfo server)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var existing = ServerListBox.Items.Cast<ServerInfo>().FirstOrDefault(s => s.Ip == server.Ip);
                if (existing != null) ServerListBox.Items.Remove(existing);
                ServerListBox.Items.Add(server);
            });
        }

        private static string GetLocalIP()
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 80);
                return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString() ?? "127.0.0.1";
            }
            catch { return "127.0.0.1"; }
        }

        private void Back_Click(object? sender, RoutedEventArgs e) => Close();

        private void Refresh_Click(object? sender, RoutedEventArgs e)
        {
            ServerListBox.Items.Clear();
            _discovery?.Dispose();
            StartDiscovery();
        }

        private void ServerList_DoubleTapped(object? sender, TappedEventArgs e)
        {
            if (ServerListBox.SelectedItem is ServerInfo server)
                _ = ConnectToServer(server);
        }

        private async void DirectConnect_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new DirectConnectDialog();
            await dialog.ShowDialog(this);
            if (!dialog.Confirmed) return;
            var server = new ServerInfo { Ip = dialog.IpAddress, Port = dialog.Port, Name = "Direct Connection" };
            await ConnectToServer(server, dialog.Username, dialog.EncryptionKey);
        }

        private async System.Threading.Tasks.Task ConnectToServer(ServerInfo server, string? username = null, string? key = null)
        {
            if (username == null || key == null)
            {
                var creds = new LanCredentialsDialog();
                await creds.ShowDialog(this);
                if (!creds.Confirmed) return;
                username = creds.Username;
                key = creds.EncryptionKey;
            }

            if (string.IsNullOrWhiteSpace(username)) { await Dialogs.Error(this, "Please enter a username."); return; }
            if (string.IsNullOrEmpty(key)) { await Dialogs.Error(this, "Please enter an encryption key."); return; }

            try
            {
                var chat = new ChatWindow(username, key, ChatWindow.ConnectionMode.Lan, server.Ip, server.Port);
                bool connected = await chat.ConnectAsync();
                if (connected)
                {
                    ChatOpened = true;
                    chat.Closed += (_, _) => _menu.Show();
                    chat.Show();
                    Close();
                }
                else
                {
                    await Dialogs.Error(this, "Failed to connect to the server.");
                }
            }
            catch (Exception ex)
            {
                await Dialogs.Error(this, $"Connection error: {ex.Message}");
            }
        }
    }
}
