using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace HyperVWcfTransport.Common
{
    class WseClientTcpDuplexSessionChannel : WseTcpDuplexSessionChannel
    {
        public WseClientTcpDuplexSessionChannel(
            MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager,
            EndpointAddress remoteAddress, Uri via, ChannelManagerBase channelManager)
            : base(messageEncoderFactory, bufferManager, remoteAddress, AnonymousAddress, via, channelManager)
        {
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            var servers = Hostname.ParseAsync(Via.Authority, 8081).Result;
            for (int i = 0; i < servers.Length; i++)
            {
                try
                {
                    var address = servers[i];
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(address);

                    base.InitializeSocket(socket);

                    break;
                }
                catch (SocketException socketException) when (i < servers.Length - 1)
                {
                    throw ConvertSocketException(socketException, "Connect");
                }
            }

            base.OnOpen(timeout);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, async() =>
            {
                var servers = await Hostname.ParseAsync(Via.Authority, 8081);
                for (int i = 0; i < servers.Length; i++)
                {
                    try
                    {
                        var address = servers[i];
                        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                        await socket.ConnectAsync(address);

                        base.InitializeSocket(socket);

                        break;
                    }
                    catch (SocketException socketException) when (i < servers.Length - 1)
                    {
                        throw ConvertSocketException(socketException, "Connect");
                    }
                }
            });
        }

        protected override void OnEndOpen(IAsyncResult result) => Tap.Complete(result);
    }
}
