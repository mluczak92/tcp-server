using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace tcp_server {
    class Program {
        static readonly short port = 2121;
        static readonly byte bufferSize = 64; //not lower than 5! first 5 bytes shows whole message size
        static readonly Dictionary<Guid, dynamic> clients = new Dictionary<Guid, dynamic>();
        static TcpListener server;

        static void Main(string[] args) {
            Print($"Hello world! Waiting for new clients on TCP port {port}...");
            server = new TcpListener(IPAddress.Loopback, port);
            server.Start();

            while (true) {
                WaitForNextClient(out Guid id, out TcpClient client, out NetworkStream stream);
                ReadMessage(id, client, stream);
            }
        }

        static void WaitForNextClient(out Guid id, out TcpClient client, out NetworkStream stream) {
            client = server.AcceptTcpClient();
            stream = client.GetStream();
            id = Guid.NewGuid();
            clients.Add(id, new { client, stream });
            Print($"New client!\t{id}\tTotal: {clients.Count}");
        }

        async static void ReadMessage(Guid id, TcpClient client, NetworkStream stream) {
            StringBuilder messageBuilder = new StringBuilder();
            byte[] receivedBuffor = new byte[bufferSize];
            short sizeFromMessageHeader = 0;
            int alreadyReadBytes = 0;
            try {
                int currentPartSize;
                while ((currentPartSize = await stream.ReadAsync(receivedBuffor, 0, receivedBuffor.Length)) != 0) {
                    string decoded = Encoding.ASCII.GetString(receivedBuffor, 0, currentPartSize);
                    messageBuilder.Append(decoded);

                    if (sizeFromMessageHeader == 0) { //first part
                        sizeFromMessageHeader = short.Parse(decoded.Substring(0, 5));
                    }

                    alreadyReadBytes += currentPartSize;
                    if (alreadyReadBytes == sizeFromMessageHeader) { //last part
                        Print($"<<< Received\t{ShortId(id)}:\t\"{messageBuilder}\".");
                        string response = $"Read {alreadyReadBytes} bytes long message.";
                        byte[] encoded = Encoding.ASCII.GetBytes(response);
                        await stream.WriteAsync(encoded, 0, encoded.Length);
                        Print($">>> Sent\t{ShortId(id)}:\t\"{response}\".");

                        messageBuilder.Clear();
                        sizeFromMessageHeader = 0;
                        alreadyReadBytes = 0;
                        Print("Idle...");
                    }
                }
            } catch (IOException) { //client force closed while connection was active
                stream.Close();
                client.Close();
                clients.Remove(id);
                Print($"Disconnected.\t{id}\tTotal: {clients.Count}");
            }
        }

        static string ShortId(Guid id) {
            return $"{id.ToString().Substring(0, 8)}...";
        }

        static void Print(string msg) {
            Console.WriteLine($"{DateTime.Now:HH:mm fff}\t{msg}");
        }
    }
}
