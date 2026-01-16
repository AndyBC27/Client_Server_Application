using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

//localhost - 127.0.0.1

namespace Server_Nowy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private CancellationTokenSource cts = new CancellationTokenSource();
        public MainWindow()
        {
            InitializeComponent();
        }

        public TcpClient client;
        public TcpListener server;
        public IPAddress adresIP = null;
        public BackgroundWorker bwConnection;
        public bool activeCall = false;

        private List<TcpClient> connectedClients = new List<TcpClient>();



        private async void bStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Recreates the CancellationTokenSource when restarting the server
                cts = new CancellationTokenSource();

                int port = System.Convert.ToInt16(nUDPort.Text);
                adresIP = IPAddress.Parse(tbHostAddress.Text);

                server = new TcpListener(adresIP, port);
                server.Start();

                bStart.IsEnabled = false;
                bStop.IsEnabled = true;

                while (!cts.Token.IsCancellationRequested)
                {
                    TcpClient newClient = await server.AcceptTcpClientAsync();
                    connectedClients.Add(newClient);
                    Task.Run(() => HandleClient(newClient, cts.Token));
                }
            }
            catch (Exception ex)
            {
                Server.Text += "\nError initializing the server!";
                bStart.IsEnabled = true;
                bStop.IsEnabled = false;
                MessageBox.Show(ex.ToString(), "Error"); //to pokazuje dokladny error
            }
        }

        private async Task HandleClient(TcpClient client, CancellationToken ct)
        {
            try
            {
                using (NetworkStream ns = client.GetStream())
                using (BinaryReader reader = new BinaryReader(ns, Encoding.UTF8, leaveOpen: true))
                {
                    while (!ct.IsCancellationRequested)
                    {
                        if (ns.DataAvailable)
                        {
                            string messageReceived = reader.ReadString();
                            Dispatcher.Invoke(() => Server.Text += "\nReceived: " + messageReceived); //tutaj wysylam reszcie
                            foreach (TcpClient otherClient in connectedClients)
                            {
                                // Skip if the client is the sender
                                if (client == otherClient) continue;

                                try
                                {
                                    if (otherClient.Connected)
                                    {
                                        // Use the otherClient's stream to send the message
                                        NetworkStream clientStream = otherClient.GetStream();
                                        using (BinaryWriter writer = new BinaryWriter(clientStream, Encoding.UTF8, leaveOpen: true))
                                        {
                                            writer.Write(messageReceived);
                                        }

                                    }
                                }
                                catch (Exception ex)
                                {
                                    Dispatcher.Invoke(() => Server.Text += "\nError sending to a client: " + ex.Message);
                                    // Optionally, remove the client from the list if it's no longer connected
                                }
                            }
                        }
                        else
                        {
                            await Task.Delay(100); // Wait a bit before trying to read again
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Network error: {ex.Message}");

                // Perform any necessary cleanup, such as removing the client from the list
                Dispatcher.Invoke(() =>
                {
                    connectedClients.Remove(client);
                    Server.Text += $"\nClient disconnected unexpectedly: {ex.Message}";
                });
            }
            catch (ObjectDisposedException)
            {
                // Stream or TcpClient is disposed
                Dispatcher.Invoke(() =>
                {
                    // Since the object is disposed, it's safe to assume the client is no longer connected
                    connectedClients.Remove(client);
                    Server.Text += "\nA connection has been closed.";
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error");
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    connectedClients.Remove(client);
                    Server.Text += $"\nClient disconnected";
                    client.Close();
                });
            }
        }

        private void bStop_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cts.Cancel(); // Signal all tasks to terminate

                foreach (TcpClient client in connectedClients)
                {
                    if (client.GetStream() != null)
                    {
                        client.GetStream().Close();
                    }
                    client.Close(); // Close each client connection
                }
                connectedClients.Clear(); // Clear the list of clients

                if (server != null)
                {
                    server.Stop(); // Stop the server
                }

                Server.Text = "Server has been stopped.";
                bStart.IsEnabled = true;
                bStop.IsEnabled = false;    
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error"); //tu pokazuje ten error moglbym wylaczyc jakbym chcial
            }
        }

        private void bSend_Click(object sender, RoutedEventArgs e)
        {
            string messageToSend = tbMessage.Text.Trim();

            if (string.IsNullOrEmpty(messageToSend))
            {
                MessageBox.Show("Please enter a message to send.");
                return;
            }

            Server.Text += "\nSending: From server: " + messageToSend;

            List<TcpClient> disconnectedClients = new List<TcpClient>();

            foreach (TcpClient connectedClient in connectedClients.ToList())
            {
                try
                {
                    if (connectedClient.Connected)
                    {
                        NetworkStream clientStream = connectedClient.GetStream();
                        using (BinaryWriter writer = new BinaryWriter(clientStream, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(messageToSend);
                            writer.Flush(); // Ensure the message is sent immediately
                        }
                    }
                    else
                    {
                        disconnectedClients.Add(connectedClient);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => Server.Text += "\nError sending to a client: " + ex.Message);
                    disconnectedClients.Add(connectedClient);
                }
            }

            foreach (TcpClient disconnectedClient in disconnectedClients)
            {
                Dispatcher.Invoke(() => Server.Text += $"\nClient disconnected: {disconnectedClient.Client.RemoteEndPoint}");
                connectedClients.Remove(disconnectedClient);
                disconnectedClient.Close();
            }

            tbMessage.Clear(); // Clear the message textbox after sending
        }
    }
}
