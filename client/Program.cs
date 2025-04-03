using System.Collections.Immutable;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using LibData;

// SendTo();
class Program
{
    static void Main(string[] args)
    {
        ClientUDP.Start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ClientUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);

    public static void Start()
    {
        if (setting == null || string.IsNullOrEmpty(setting.ClientIPAddress) || string.IsNullOrEmpty(setting.ServerIPAddress))
            throw new Exception("Invalid configuration settings");
        
        EndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var localEndPoint = new IPEndPoint(IPAddress.Parse(setting.ClientIPAddress), setting.ClientPortNumber);
        socket.Bind(localEndPoint);

        try
        {
            var hello = new Message
            {
                MsgId = 1,
                MsgType = MessageType.Hello,
                Content = "Hello from client"
            };
            SendMessage(socket, hello, serverEndPoint); // Send Hello message

            var welcome = ReceiveMessage(socket, ref serverEndPoint); // Receive Welcome message
            if (welcome.MsgType != MessageType.Welcome) throw new Exception("Protocol error: Expected Welcome message");

            // List of DNS lookups
            var lookups = new[]
            {
                new DNSRecord { Type = "A", Name = "www.outlook.com" }, // Valid A record
                new DNSRecord { Type = "A", Name = "mail.example.com" }, // Valid A record
                new DNSRecord { Type = "A", Name = "www.example.com" }, // Invalid A record
                new DNSRecord { Type = "A", Name = "unknown.domain" }, // Invalid A record
            };

            foreach (var lookup in lookups)
            {
                var dnsLookup = new Message
                {
                    MsgId = new Random().Next(100, 999),
                    MsgType = MessageType.DNSLookup,
                    Content = lookup
                };
                SendMessage(socket, dnsLookup, serverEndPoint);

                var response = ReceiveMessage(socket, ref serverEndPoint);

                if (response.MsgType == MessageType.DNSLookupReply)
                {
                    Console.WriteLine($"DNS Record found: {JsonSerializer.Serialize(response.Content)}");
                }
                else if (response.MsgType == MessageType.Error)
                {
                    Console.WriteLine($"Error: {response.Content}");
                }

                var ack = new Message
                {
                    MsgId = new Random().Next(1000, 9999),
                    MsgType = MessageType.Ack,
                    Content = response.MsgId.ToString()
                };
                SendMessage(socket, ack, serverEndPoint);
            }

            var end = ReceiveMessage(socket, ref serverEndPoint);
            if (end.MsgType == MessageType.End) Console.WriteLine("Server ended communication");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            socket.Close();
        }
    }

    private static void SendMessage(Socket socket, Message message, EndPoint endPoint)
    {
        var json = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        socket.SendTo(buffer, endPoint);
        Console.WriteLine($"Sent {message.MsgType} message with ID {message.MsgId}");
    }

    private static Message ReceiveMessage(Socket socket, ref EndPoint endPoint)
    {
        byte[] buffer = new byte[1024]; 
        int received = socket.ReceiveFrom(buffer, ref endPoint);
        var messageJson = Encoding.UTF8.GetString(buffer, 0, received);
        var message = JsonSerializer.Deserialize<Message>(messageJson);
        if (message == null) throw new Exception("Received null message");
        Console.WriteLine($"Received {message.MsgType} message with ID {message.MsgId}");
        return message;
    }
}