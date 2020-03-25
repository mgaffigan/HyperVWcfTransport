using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using static HyperVWcfTransport.Win32.NativeMethods;

namespace HyperVWcfTransport
{
    class HyperVNetClientDuplexSessionChannel : HyperVNetDuplexSessionChannel
    {
        public HyperVNetClientDuplexSessionChannel(
            MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager, int maxBufferSize,
            EndpointAddress remoteAddress, Uri via, ChannelManagerBase channelManager)
            : base(messageEncoderFactory, bufferManager, maxBufferSize, remoteAddress, AnonymousAddress, via, channelManager)
        {
        }

        protected override void OnOpen(TimeSpan timeout)
        {
            var targetAddress = HyperVSocketEndPoint.Parse(Via);
            try
            {
                var socket = new Socket(AF_HYPERV, SocketType.Stream, HV_PROTOCOL_RAW);
                socket.Connect(targetAddress);

                base.InitializeSocket(socket);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Connect");
            }

            base.OnOpen(timeout);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, async () =>
            {
                var targetAddress = HyperVSocketEndPoint.Parse(Via);
                try
                {
                    var socket = new Socket(AF_HYPERV, SocketType.Stream, HV_PROTOCOL_RAW);
                    await socket.ConnectAsync(targetAddress);

                    base.InitializeSocket(socket);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Connect");
                }
            });
        }

        protected override void OnEndOpen(IAsyncResult result) => Tap.Complete(result);
    }
}
