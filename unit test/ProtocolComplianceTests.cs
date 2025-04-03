using System.Net;
using System.Text.Json;
using LibData;
using Xunit;

namespace DnsClientServerTests
{
    public class ProtocolComplianceTests : DnsTestBase
    {
        [Fact]
        public async Task Server_ReturnsDnsRecord_ForValidDnsLookup()
        {
            // Arrange
            var lookupRecord = new DNSRecord { Type = "A", Name = "www.test.com" };
            var lookup = new Message
            {
                MsgId = 3,
                MsgType = MessageType.DNSLookup,
                Content = lookupRecord
            };
            Log($"Starting DNS lookup test for {lookupRecord.Name}");

            // Mock server
            var serverTask = Task.Run(() => {
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                var received = ReceiveMessage(ServerSocket, ref clientEp);

                if (received?.MsgType == MessageType.DNSLookup)
                {
                    DNSRecord record;
                    if (received.Content is JsonElement jsonElement)
                    {
                        record = JsonSerializer.Deserialize<DNSRecord>(jsonElement.GetRawText());
                        Log($"Deserialized DNSRecord from JSON: {record.Name}");
                    }
                    else
                    {
                        record = (DNSRecord)received.Content;
                    }

                    record.Type ??= string.Empty;
                    record.Name ??= string.Empty;

                    var match = TestDnsRecords.FirstOrDefault(r =>
                        r.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) &&
                        r.Type.Equals(record.Type, StringComparison.OrdinalIgnoreCase));

                    var reply = new Message
                    {
                        MsgId = received.MsgId,
                        MsgType = match != null ? MessageType.DNSLookupReply : MessageType.Error,
                        Content = match ?? (object)$"Record not found: {record.Name}"
                    };

                    SendMessage(ServerSocket, reply, clientEp);
                }
            });

            // Act
            SendMessage(ClientSocket, lookup, ServerEndPoint);
            await serverTask;
            var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            var response = ReceiveMessage(ClientSocket, ref clientEp);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(MessageType.DNSLookupReply, response.MsgType);

            DNSRecord responseRecord;
            if (response.Content is JsonElement responseJson)
            {
                responseRecord = JsonSerializer.Deserialize<DNSRecord>(responseJson.GetRawText());
            }
            else
            {
                responseRecord = (DNSRecord)response.Content;
            }

            Assert.NotNull(responseRecord);
            Assert.Equal("www.test.com", responseRecord.Name);
            Assert.Equal("192.168.1.20", responseRecord.Value);
        }
    }
}