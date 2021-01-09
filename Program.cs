using System;

namespace tcp_server {
    class Program {
        static int port = 2121;

        static void Main(string[] args) {
            Log($"Hello world! I'm waiting for TCP packets on port {port}");
        }

        static void Log(string msg) {
            Console.WriteLine($"{DateTime.Now.ToString("HH:mm")}\t{msg}");
        }
    }
}
