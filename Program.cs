using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace tcp_server {
    class ClientProxy {
        readonly EventHandler<string> printHandler;
        readonly EventHandler<ClientProxy> dcHandler;

        readonly byte[] receivedBuffor = new byte[64]; //not lower than 5! first 5 bytes shows whole message size
        readonly StringBuilder messageBuilder = new StringBuilder();
        int currentPartSize;
        short sizeFromMessageHeader;
        int alreadyReadBytes;

        public ClientProxy(TcpClient client, EventHandler<string> printHandler,
            EventHandler<ClientProxy> dcHandler) {
            Client = client;
            this.printHandler = printHandler;
            this.dcHandler = dcHandler;
            Stream = Client.GetStream();
        }

        public TcpClient Client { get; }
        public Guid Id { get; } = Guid.NewGuid();
        public string ShortId => Id.ToString().Substring(0, 8);
        public NetworkStream Stream { get; }

        public void KeepListening() {
            try {
                while ((currentPartSize = Stream.Read(receivedBuffor, 0, receivedBuffor.Length)) != 0) {
                    string decoded = Encoding.ASCII.GetString(receivedBuffor, 0, currentPartSize);
                    messageBuilder.Append(decoded);

                    if (sizeFromMessageHeader == 0) { //first part
                        sizeFromMessageHeader = short.Parse(decoded.Substring(0, 5));
                    }

                    alreadyReadBytes += currentPartSize;
                    if (alreadyReadBytes == sizeFromMessageHeader) { //last part
                        printHandler.Invoke(this, $"{messageBuilder.ToString()[6..]}");
                        messageBuilder.Clear();
                        sizeFromMessageHeader = 0;
                        alreadyReadBytes = 0;
                    }
                }
            } catch (IOException) { //client force closed while connection was active
                Stream.Close();
                Client.Close();
                dcHandler.Invoke(this, null);
            }
        }

        public async Task SendAsync(string msg) {
            try {
                byte[] encoded = Encoding.ASCII.GetBytes(msg);
                Stream.WriteAsync(encoded, 0, encoded.Length);
            } catch (IOException) { //client force closed while connection was active
                Stream.Close();
                Client.Close();
                dcHandler.Invoke(this, null);
            }
        }
    }

    class Program {
        //static readonly IPAddress host = IPAddress.Parse("127.0.0.1");
        static readonly short port = 2121;
        static readonly TcpListener server = new TcpListener(port);
        static readonly HashSet<ClientProxy> clients = new HashSet<ClientProxy>();

        static void Main(string[] args) {
            server.Start();
            Print($"Waiting for new clients on TCP port {port}...");

            //reading messages from each client
            Task.Run(() => {
                while (true) {
                    ClientProxy client = WaitForNextClient();
                    Task.Run(() => {
                        client.KeepListening();
                    });
                }
            });

            Console.ReadKey();
        }

        static ClientProxy WaitForNextClient() {
            TcpClient client = server.AcceptTcpClient();
            ClientProxy proxy = new ClientProxy(client, ReceiveHandler, ClientDcHandler);
            clients.Add(proxy);
            string printed = Print($"{proxy.ShortId}\tNew client!\tTotal: {clients.Count}");
            PoorMulticast(printed);
            return proxy;
        }

        static string Print(string msg) {
            msg = $"{DateTime.Now:HH:mm fff}\t{msg}";
            Console.WriteLine(msg);
            return msg;
        }

        static readonly EventHandler<string> ReceiveHandler = (sender, msg) => {
            string printed = Print($"{((ClientProxy)sender).ShortId}:\t{msg}");
            PoorMulticast(printed);
        };

        static void PoorMulticast(string msg) {
            foreach (ClientProxy proxy in clients) {
                proxy.SendAsync(AddHeader(msg));
            }
        }

        static string AddHeader(string input) {
            return $"{(input.Length + 6).ToString().PadLeft(5, '0')}.{input}";
        }

        static readonly EventHandler<ClientProxy> ClientDcHandler = (sender, msg) => {
            clients.Remove((ClientProxy)sender);
            string printed = Print($"{((ClientProxy)sender).ShortId}\tDisconnected.\tTotal: {clients.Count}");
            PoorMulticast(printed);
        };
    }
}
