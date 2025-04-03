using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LibData;

namespace DnsClientServerTests
{
    public abstract class DnsTestBase : IDisposable
    {
        protected readonly Socket ServerSocket;
        protected readonly Socket ClientSocket;
        protected readonly IPEndPoint ServerEndPoint;
        protected readonly IPEndPoint ClientEndPoint;
        protected readonly List<DNSRecord> TestDnsRecords;
        private static readonly object _socketLock = new();
        private static int _basePort = 10530;

        protected DnsTestBase()
        {
            lock (_socketLock)
            {
                int port = Interlocked.Add(ref _basePort, 2);
                ServerEndPoint = new IPEndPoint(IPAddress.Loopback, port);
                ClientEndPoint = new IPEndPoint(IPAddress.Loopback, port + 1);

                ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                ServerSocket.Bind(ServerEndPoint);
                ClientSocket.Bind(ClientEndPoint);
            }

            TestDnsRecords = new List<DNSRecord>
            {
                new DNSRecord { Type = "A", Name = "www.outlook.com", Value = "192.168.1.10", TTL = 3600 },
                new DNSRecord { Type = "A", Name = "www.test.com", Value = "192.168.1.20", TTL = 3600 },
                new DNSRecord { Type = "MX", Name = "example.com", Value = "mail.example.com", Priority = 10, TTL = 3600 }
            };
        }

        public void Dispose()
        {
            ServerSocket?.Close();
            ClientSocket?.Close();
            GC.SuppressFinalize(this);
        }

        protected void Log(string message, ConsoleColor color = ConsoleColor.White)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.ForegroundColor = color;
            Console.WriteLine($"[{timestamp}] {GetType().Name}: {message}");
            Console.ResetColor();
        }

        protected Message? ReceiveMessage(Socket socket, ref EndPoint endPoint)
        {
            byte[] buffer = new byte[1024];
            try
            {
                int received = socket.ReceiveFrom(buffer, ref endPoint);
                var message = JsonSerializer.Deserialize<Message>(Encoding.UTF8.GetString(buffer, 0, received));
                Log($"Received {message?.MsgType} (ID:{message?.MsgId})", ConsoleColor.Cyan);
                return message;
            }
            catch (SocketException ex)
            {
                Log($"Receive error: {ex.Message}", ConsoleColor.Red);
                return null;
            }
        }

        protected void SendMessage(Socket socket, Message message, EndPoint endPoint)
        {
            try
            {
                var json = JsonSerializer.Serialize(message);
                byte[] buffer = Encoding.UTF8.GetBytes(json);
                socket.SendTo(buffer, endPoint);
                Log($"Sent {message.MsgType} (ID:{message.MsgId})", ConsoleColor.Green);
            }
            catch (SocketException ex)
            {
                Log($"Send error: {ex.Message}", ConsoleColor.Red);
            }
        }
    }
}