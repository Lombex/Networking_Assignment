using System;
using System.Data;
using System.Data.SqlTypes;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using LibData;

// ReceiveFrom();
class Program
{
    static void Main(string[] args)
    {
        ServerUDP.Start();
    }
}

public class Setting
{
    public int ServerPortNumber { get; set; }
    public string? ServerIPAddress { get; set; }
    public int ClientPortNumber { get; set; }
    public string? ClientIPAddress { get; set; }
}

class ServerUDP
{
    static string configFile = @"../Setting.json";
    static string configContent = File.ReadAllText(configFile);
    static Setting? setting = JsonSerializer.Deserialize<Setting>(configContent);  
    static string dnsRecordsFile = @"DNSrecords.json";
    private static int ackCount = 0;

    private static List<DNSRecord> LoadDnsRecords()
    {
        var json = File.ReadAllText(dnsRecordsFile);
        return JsonSerializer.Deserialize<List<DNSRecord>>(json) ?? new List<DNSRecord>();
    }

    public static void Start()
    {
        var serverEndPoint = new IPEndPoint(IPAddress.Parse(setting.ServerIPAddress), setting.ServerPortNumber);
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Bind(serverEndPoint);

        Console.WriteLine($"Server started on {serverEndPoint}. Waiting for clients...");
        var dnsRecords = LoadDnsRecords();

        while (true)
        {
            EndPoint clientEndPoint = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                byte[] buffer = new byte[1024];
                int received = socket.ReceiveFrom(buffer, ref clientEndPoint);
                var messageJson = Encoding.UTF8.GetString(buffer, 0, received);
                var message = JsonSerializer.Deserialize<Message>(messageJson);

                Console.WriteLine($"Received {message.MsgType} message with ID {message.MsgId} from {clientEndPoint}");

                switch (message.MsgType)
                {
                    case MessageType.Hello:
                        var welcome = new Message
                        {
                            MsgId = new Random().Next(1, 1000),
                            MsgType = MessageType.Welcome,
                            Content = "Welcome from server"
                        };
                        SendMessage(socket, welcome, clientEndPoint);
                        ackCount = 0;
                        break;

                    case MessageType.DNSLookup:
                        DNSRecord? lookupRecord;
                        try
                        {
                            lookupRecord = JsonSerializer.Deserialize<DNSRecord>(message.Content.ToString());
                        }
                        catch
                        {
                            var error = new Message
                            {
                                MsgId = message.MsgId,
                                MsgType = MessageType.Error,
                                Content = "Invalid DNSLookup format. Expected Type and Name."
                            };
                            SendMessage(socket, error, clientEndPoint);
                            break;
                        }

                        var record = dnsRecords.FirstOrDefault(r =>
                            r.Name.Equals(lookupRecord.Name, StringComparison.OrdinalIgnoreCase) &&
                            r.Type.Equals(lookupRecord.Type, StringComparison.OrdinalIgnoreCase));

                        if (record != null)
                        {
                            var reply = new Message
                            {
                                MsgId = message.MsgId,
                                MsgType = MessageType.DNSLookupReply,
                                Content = record
                            };
                            SendMessage(socket, reply, clientEndPoint);
                        }
                        else
                        {
                            var error = new Message
                            {
                                MsgId = message.MsgId,
                                MsgType = MessageType.Error,
                                Content = $"Domain {lookupRecord.Name} (Type: {lookupRecord.Type}) not found"
                            };
                            SendMessage(socket, error, clientEndPoint);
                        }
                        break;

                    case MessageType.Ack:
                        ackCount++;
                        Console.WriteLine($"Received ACK #{ackCount} for message ID {message.Content}");
                        
                        if (ackCount >= 4)
                        {
                            var endMessage = new Message
                            {
                                MsgId = new Random().Next(10000, 99999),
                                MsgType = MessageType.End,
                                Content = "End of communication"
                            };
                            SendMessage(socket, endMessage, clientEndPoint);
                            Console.WriteLine($"Completed communication with client {clientEndPoint}. Ready for next client.");
                            ackCount = 0;
                        }
                        break;
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Socket error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }
    }

    private static void SendMessage(Socket socket, Message message, EndPoint endPoint)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            socket.SendTo(buffer, endPoint);
            Console.WriteLine($"Sent {message.MsgType} message with ID {message.MsgId} to {endPoint}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message: {ex.Message}");
        }
    }
}