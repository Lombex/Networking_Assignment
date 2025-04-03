using System.Net;
using System.Text.Json;
using LibData;
using Xunit;

namespace DnsClientServerTests
{
    public class DnsRecordHandlingTests : DnsTestBase
    {
        [Fact]
        public async Task Server_HandlesDifferentRecordTypes_Correctly()
        {
            // Arrange
            var lookupRecord = new DNSRecord { Type = "A", Name = "www.test.com" };
            var lookup = new Message
            {
                MsgId = 1,
                MsgType = MessageType.DNSLookup,
                Content = lookupRecord
            };

            // Mock server
            var serverTask = Task.Run(() => {
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                var received = ReceiveMessage(ServerSocket, ref clientEp);

                if (received?.MsgType == MessageType.DNSLookup)
                {
                    DNSRecord record;
                    if (received.Content is DNSRecord directRecord)
                    {
                        record = directRecord;
                    }
                    else if (received.Content is JsonElement jsonElement)
                    {
                        record = JsonSerializer.Deserialize<DNSRecord>(jsonElement.GetRawText());
                    }
                    else
                    {
                        record = JsonSerializer.Deserialize<DNSRecord>(received.Content.ToString());
                    }

                    record.Type ??= string.Empty;
                    record.Name ??= string.Empty;

                    var match = TestDnsRecords.FirstOrDefault(r =>
                        r.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) &&
                        r.Type.Equals(record.Type, StringComparison.OrdinalIgnoreCase));

                    var reply = new Message
                    {
                        MsgId = received.MsgId,
                        MsgType = MessageType.DNSLookupReply,
                        Content = match
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

            var responseRecord = response.Content as DNSRecord ??
                               JsonSerializer.Deserialize<DNSRecord>(((JsonElement)response.Content).GetRawText());
            Assert.Equal("A", responseRecord.Type);
            Assert.Equal("www.test.com", responseRecord.Name);
        }

        [Fact]
        public async Task Server_HandlesCaseInsensitiveLookups()
        {
            // Arrange
            var lookupRecord = new DNSRecord { Type = "a", Name = "WWW.TEST.COM" };
            var lookup = new Message
            {
                MsgId = 1,
                MsgType = MessageType.DNSLookup,
                Content = lookupRecord
            };

            // Mock server
            var serverTask = Task.Run(() => {
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                var received = ReceiveMessage(ServerSocket, ref clientEp);

                if (received?.MsgType == MessageType.DNSLookup)
                {
                    DNSRecord record;
                    if (received.Content is DNSRecord directRecord)
                    {
                        record = directRecord;
                    }
                    else if (received.Content is JsonElement jsonElement)
                    {
                        record = JsonSerializer.Deserialize<DNSRecord>(jsonElement.GetRawText());
                    }
                    else
                    {
                        record = JsonSerializer.Deserialize<DNSRecord>(received.Content.ToString());
                    }

                    record.Type ??= string.Empty;
                    record.Name ??= string.Empty;

                    var match = TestDnsRecords.FirstOrDefault(r =>
                        r.Name.Equals(record.Name, StringComparison.OrdinalIgnoreCase) &&
                        r.Type.Equals(record.Type, StringComparison.OrdinalIgnoreCase));

                    var reply = new Message
                    {
                        MsgId = received.MsgId,
                        MsgType = MessageType.DNSLookupReply,
                        Content = match
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

            var responseRecord = response.Content as DNSRecord ??
                               JsonSerializer.Deserialize<DNSRecord>(((JsonElement)response.Content).GetRawText());
            Assert.Equal("www.test.com", responseRecord.Name, ignoreCase: true);
        }
    }
}