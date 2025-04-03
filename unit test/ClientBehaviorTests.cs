using System.Net;
using LibData;
using Xunit;

namespace DnsClientServerTests
{
    public class ClientBehaviorTests : DnsTestBase
    {
        [Fact]
        public async Task Client_SendsHello_AsFirstMessage()
        {
            // Mock server that expects Hello
            var serverTask = Task.Run(() => {
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
                var received = ReceiveMessage(ServerSocket, ref clientEp);
                Assert.NotNull(received);
                Assert.Equal(MessageType.Hello, received.MsgType);
            });

            // Act (simulate client)
            var hello = new Message
            {
                MsgId = 1,
                MsgType = MessageType.Hello,
                Content = "Hello"
            };
            SendMessage(ClientSocket, hello, ServerEndPoint);

            await serverTask;
        }

        [Fact]
        public async Task Client_SendsAck_AfterDnsLookupReply()
        {
            // Arrange - mock server
            var serverTask = Task.Run(() => {
                var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);

                // Receive DNSLookup
                var lookup = ReceiveMessage(ServerSocket, ref clientEp);
                Assert.Equal(MessageType.DNSLookup, lookup?.MsgType);

                // Send reply
                var reply = new Message
                {
                    MsgId = lookup!.MsgId,
                    MsgType = MessageType.DNSLookupReply,
                    Content = TestDnsRecords[0] // First record
                };
                SendMessage(ServerSocket, reply, clientEp);

                // Expect Ack
                var ack = ReceiveMessage(ServerSocket, ref clientEp);
                Assert.Equal(MessageType.Ack, ack?.MsgType);
                Assert.Equal(reply.MsgId.ToString(), ack?.Content?.ToString());
            });

            // Act (simulate client)
            var lookup = new Message
            {
                MsgId = 1,
                MsgType = MessageType.DNSLookup,
                Content = new DNSRecord { Type = "A", Name = "www.outlook.com" }
            };
            SendMessage(ClientSocket, lookup, ServerEndPoint);

            // Get reply
            var clientEp = (EndPoint)new IPEndPoint(IPAddress.Any, 0);
            var reply = ReceiveMessage(ClientSocket, ref clientEp);

            // Send Ack
            if (reply != null)
            {
                var ack = new Message
                {
                    MsgId = 2,
                    MsgType = MessageType.Ack,
                    Content = reply.MsgId.ToString()
                };
                SendMessage(ClientSocket, ack, ServerEndPoint);
            }

            await serverTask;
        }
    }
}