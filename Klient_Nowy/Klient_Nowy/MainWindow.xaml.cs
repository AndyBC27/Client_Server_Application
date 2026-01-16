using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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

namespace Klient_Nowy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public BackgroundWorker bwReceiver;
        public MainWindow()
        {
            InitializeComponent();
            bwReceiver = new BackgroundWorker
            {
                WorkerSupportsCancellation = true
            };
            bwReceiver.DoWork += BwReceiver_DoWork;
        }

        private void BwReceiver_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!bwReceiver.CancellationPending)
            {
                try
                {
                    string messageReceived = reading.ReadString();
                    Dispatcher.Invoke(() => Client.Text += "\nReceived: " + messageReceived);
                }
                catch (IOException ioEx)
                {
                    // This will catch any IO exceptions, including if the stream is closed unexpectedly
                    Dispatcher.Invoke(() => Client.Text += "\nDisconnected from server: " + ioEx.Message);
                    break; // Exit the loop as the connection is closed
                }
                catch (ObjectDisposedException)
                {
                    // This occurs if the NetworkStream is closed
                    Dispatcher.Invoke(() => Client.Text += "\nDisconnected: Stream has been closed.");
                    break; // Exit the loop as the connection is closed
                }
                catch (Exception ex)
                {
                    // Handle any other exceptions that might occur
                    Dispatcher.Invoke(() => Client.Text += "\nAn error occurred: " + ex.Message);
                    break; // Exit the loop as we encountered an unexpected exception
                }
                finally
                {
                    // Clean up resources and update the state as necessary
                    activeCall = false;
                }
            }

            // If we get here, the background worker should be cancelled as we've disconnected
            if (bwReceiver.WorkerSupportsCancellation)
            {
                bwReceiver.CancelAsync();
            }
        }

        private TcpClient client = null;
        private BinaryReader reading = null;
        private BinaryWriter writing = null;
        private bool activeCall = false;
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            string host = tbHostAddress.Text;
            int port = System.Convert.ToInt16(nUDPort.Text);
            try
            {
                client = new TcpClient();
                if (client.Connected)
                {
                    Client.Text += "\nAlready connected.";
                    return;
                }

                await client.ConnectAsync(host, port); // Connect asynchronously
                Client.Text += "\nConnected to " + host + " on port: " + port;

                NetworkStream ns = client.GetStream();
                reading = new BinaryReader(ns);
                writing = new BinaryWriter(ns);

                activeCall = true;
                if (!bwReceiver.IsBusy)
                {
                    Dispatcher.Invoke(() => bwReceiver.RunWorkerAsync());
                }
            }
            catch (IOException ioEx)
            {
                Dispatcher.Invoke(() => Client.Text += "\nDisconnected while reading password: " + ioEx.Message);
            }
            catch (Exception ex)
            {
                Client.Text = Client.Text + "\nBlad: Nie udalo sie nawizac polaczenia!";
                MessageBox.Show(ex.ToString(), "Error"); //To robi ten taki duzy blad ale to nie problem
            }
        }

        private void bSend_Click(object sender, RoutedEventArgs e)
        {
            if (client != null && client.Connected)
            {
                try
                {
                    string messageToSend = "\nFrom: " + tbNick.Text + ": " + tbMessage.Text;
                    NetworkStream stream = client.GetStream();
                    if (stream.CanWrite)
                    {
                        Client.Text += messageToSend;
                        using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(messageToSend);
                            writer.Flush(); // Ensure the message is sent immediately
                        }
                    }
                }
                catch (IOException ioEx)
                {
                    // Handle the exception, update the UI accordingly
                    Client.Text += "\nFailed to send message: " + ioEx.Message;
                }
            }
            else
            {
                Client.Text = Client.Text + "\nBlad: Nie ma aktywnego polaczenia!";
            }
        }
    }
}
