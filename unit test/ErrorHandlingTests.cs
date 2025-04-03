using System.Net;
using System.Text.Json;
using LibData;
using Xunit;

namespace DnsClientServerTests
{
    public class ErrorHandlingTests : DnsTestBase
    {
        [Fact]
        public async Task Server_ReturnsError_ForInvalidDnsLookup()
        {
            // Arrange
            var lookupRecord = new DNSRecord { Type = "A", Name = "invalid.domain" };
            lookupRecord.Type ??= string.Empty;
            lookupRecord.Name ??= string.Empty;

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
                    var error = new Message
                    {
                        MsgId = received.MsgId,
                        MsgType = MessageType.Error,
                        Content = $"Domain not found: {((JsonElement)received.Content).GetProperty("Name").GetString()}"
                    };
                    SendMessage(ServerSocket, error, clientEp);
                }
            });

            // Act
            SendMessage(ClientSocket, lookup, ServerEndPoint);
            await serverTask;
            var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            var response = ReceiveMessage(ClientSocket, ref clientEp);

            // Assert
            Assert.NotNull(response);
            Assert.Equal(MessageType.Error, response.MsgType);
            Assert.Contains("not found", response.Content?.ToString() ?? "");
        }
    }
}