using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace tcp_server {
    class Program {
        static short port = 2121;
        static byte bufferSize = 8; //not lower than 5! first 5 bytes shows whole message size
        static StringBuilder messageBuilder = new StringBuilder();

        static void Main(string[] args) {
            Print($"Hello world!");
            TcpListener server = null;
            try {
                //IPAddress local = IPAddress.Loopback;
                server = new TcpListener(IPAddress.Loopback, port);
                server.Start();

                while (true) {
                    Print($"Waiting for a connection on port {port}...");
                    TcpClient client = server.AcceptTcpClient();

                    Print($"Connected!");
                    NetworkStream stream = client.GetStream();
                    byte[] receivedBuffor = new byte[bufferSize];
                    short sizeFromHeader = 0;
                    int alreadyReadBytes = 0;
                    short alreadyReadParts = 0;
                    int currentPartSize = 0;
                    while ((currentPartSize = stream.Read(receivedBuffor, 0, receivedBuffor.Length)) != 0) {
                        string decoded = Encoding.ASCII.GetString(receivedBuffor, 0, currentPartSize);
                        messageBuilder.Append(decoded);

                        if (sizeFromHeader == 0) { //first part
                            sizeFromHeader = byte.Parse(decoded.Substring(0, 5));
                        }

                        alreadyReadBytes += currentPartSize;
                        alreadyReadParts ++;
                        if (alreadyReadBytes == sizeFromHeader) { //last part
                            Print($"<<< Received: \"{messageBuilder}\".");
                            string response = $"Read {alreadyReadBytes} bytes long message in {alreadyReadParts} parts.";
                            byte[] encoded = Encoding.ASCII.GetBytes(response);
                            stream.Write(encoded, 0, encoded.Length);
                            Print($">>> Sent: \"{response}\".");

                            messageBuilder.Clear();
                            sizeFromHeader = 0;
                            alreadyReadBytes = 0;
                            alreadyReadParts = 0;
                        }
                    }

                    client.Close();
                }
            } finally {
                server?.Stop();
            }
        }

        static void Print(string msg) {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm fff")}\t{msg}");
        }
    }
}
