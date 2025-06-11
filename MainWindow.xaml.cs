using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Net.Http;
using System.Windows.Media;

namespace WpfChatApp
{
    public class ChatMessage
    {
        public string Content { get; set; } = string.Empty;
        public bool IsSentByMe { get; set; }
        public DateTime SentTime { get; set; }
    }

    public partial class MainWindow : Window
    {
        private TcpListener? _server;
        private TcpClient? _client;
        private StreamWriter? _writer;
        private StreamReader? _reader;

        public ObservableCollection<ChatMessage> Messages { get; } = new ObservableCollection<ChatMessage>();

        public MainWindow()
        {
            InitializeComponent();
            MessagesItemsControl.ItemsSource = Messages;
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int port = int.Parse(PortTextBox.Text);
                string localIp = "Could not fetch IP";
                try
                {

                    using (var httpClient = new HttpClient())
                    {
                        localIp = await httpClient.GetStringAsync("https://api.ipify.org");
                    }
                }
                catch
                {
                    localIp = "127.0.0.1 (Check Internet)";
                }

                _server = new TcpListener(IPAddress.Any, port);
                _server.Start();

                ConnectionPanel.Visibility = Visibility.Collapsed;
                WaitingInfoText.Text = $"Waiting for a friend to connect...\nYour IP: {localIp}\nPort: {port}";
                WaitingOverlay.Visibility = Visibility.Visible;

                await Task.Run(() => AcceptClient());
            }
            catch (Exception ex)
            {
                ConnectionPanel.Visibility = Visibility.Visible;
                WaitingOverlay.Visibility = Visibility.Collapsed;
                MessageBox.Show($"Could not start server. Please check your network.\nError: {ex.Message}");
            }
        }

        private async Task AcceptClient()
        {
            try
            {
                if (_server != null)
                {
                    _client = await _server.AcceptTcpClientAsync();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        WaitingOverlay.Visibility = Visibility.Collapsed;
                        AddMessage("Client connected. You can now chat.", false);
                    });

                    var stream = _client.GetStream();
                    _writer = new StreamWriter(stream) { AutoFlush = true };
                    _reader = new StreamReader(stream);

                    await ListenForMessages();
                }
            }
            catch (Exception ex)
            {
                AddMessage($"Error accepting client: {ex.Message}", false);
            }
        }

        private async void ConnectClientButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress ip = IPAddress.Parse(IpAddressTextBox.Text);
                int port = int.Parse(PortTextBox.Text);
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);

                var stream = _client.GetStream();
                _writer = new StreamWriter(stream) { AutoFlush = true };
                _reader = new StreamReader(stream);

                ConnectionPanel.Visibility = Visibility.Collapsed;
                AddMessage("Connected to server. You can now chat.", false);

                await ListenForMessages();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error connecting to server: {ex.Message}");
            }
        }

        private async Task ListenForMessages()
        {
            try
            {
                if (_reader != null && _client != null)
                {
                    while (_client.Connected)
                    {
                        string? message = await _reader.ReadLineAsync();
                        if (message != null)
                        {
                            AddMessage(message, false);
                        }
                    }
                }
            }
            catch (Exception)
            {
                AddMessage("Connection lost.", false);
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await SendMessage();
            }
        }

        private async Task SendMessage()
        {
            string message = MessageTextBox.Text;
            if (message != "Type a message..." && !string.IsNullOrWhiteSpace(message) && _writer != null)
            {
                await _writer.WriteLineAsync(message);
                AddMessage(message, true);
                MessageTextBox.Clear();
            }
        }

        private void AddMessage(string message, bool isSentByMe)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Messages.Add(new ChatMessage { Content = message, IsSentByMe = isSentByMe, SentTime = DateTime.Now });
                MessagesScrollViewer.ScrollToEnd();
                if (!isSentByMe)
                {
                    PlayNotificationSound();
                }
            });
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _client?.Close();
            _server?.Stop();
        }

        // PHẦN ĐÃ SỬA LỖI
        private void MessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (SendButton != null)
            {
                SendButton.IsEnabled = !string.IsNullOrWhiteSpace(MessageTextBox.Text) && MessageTextBox.Text != "Type a message...";
            }
        }

        private void PlayNotificationSound()
        {
            try
            {
                var player = new MediaPlayer();
                string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "D://WpfChatApp//WpfChatApp//221359__melliug__newmessage.mp3");
                player.Open(new Uri(soundPath));
                player.Volume = 1.0;
                player.Play();
            }
            catch
            {
                // Optionally handle errors
            }
        }
    }
}