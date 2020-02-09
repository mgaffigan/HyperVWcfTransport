using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;

namespace HyperVWcfTransport.Common
{
    class WseTcpChannelListener : ChannelListenerBase<IDuplexSessionChannel>
    {
        BufferManager bufferManager;
        MessageEncoderFactory encoderFactory;
        Socket listenSocket;
        public override Uri Uri { get; }

        public WseTcpChannelListener(WseTcpTransportBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            // populate members from binding element
            int maxBufferSize = (int)bindingElement.MaxReceivedMessageSize;
            this.bufferManager = BufferManager.CreateBufferManager(bindingElement.MaxBufferPoolSize, maxBufferSize);

            var messageEncoderBindingElement = context.BindingParameters.OfType<MessageEncodingBindingElement>().SingleOrDefault();
            if (messageEncoderBindingElement != null)
            {
                this.encoderFactory = messageEncoderBindingElement.CreateMessageEncoderFactory();
            }
            else
            {
                this.encoderFactory = new MtomMessageEncodingBindingElement().CreateMessageEncoderFactory();
            }

            this.Uri = new Uri(context.ListenUriBaseAddress, context.ListenUriRelativeAddress);
        }

        #region Open / Close

        void OpenListenSocket()
        {
            var localEndpoint = Hostname.ParseAsync(Uri.Authority, 8081).Result.Single();
            this.listenSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            this.listenSocket.Bind(localEndpoint);
            this.listenSocket.Listen(10);
        }

        private void CloseListenSocket(TimeSpan timeout) => this.listenSocket.Close((int)timeout.TotalMilliseconds);

        protected override void OnOpen(TimeSpan timeout) => OpenListenSocket();

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            OpenListenSocket();
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result) => CompletedAsyncResult.End(result);

        protected override void OnAbort() => CloseListenSocket(TimeSpan.Zero);

        protected override void OnClose(TimeSpan timeout) => CloseListenSocket(timeout);

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            CloseListenSocket(timeout);
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result) => CompletedAsyncResult.End(result);

        #endregion

        #region Accept

        protected override IDuplexSessionChannel OnAcceptChannel(TimeSpan timeout)
        {
            Socket dataSocket = listenSocket.Accept();
            return new ServerTcpDuplexSessionChannel(this.encoderFactory, this.bufferManager, dataSocket, new EndpointAddress(Uri), this);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, async () =>
            {
                try
                {
                    var dataSocket = await listenSocket.AcceptAsync();
                    return (IDuplexSessionChannel)new ServerTcpDuplexSessionChannel(this.encoderFactory, this.bufferManager, dataSocket, new EndpointAddress(this.Uri), this);
                }
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted)
                {
                    return null;
                }
            });
        }

        protected override IDuplexSessionChannel OnEndAcceptChannel(IAsyncResult result) => Tap.Complete<IDuplexSessionChannel>(result);

        #endregion

        #region WaitForChannel

        protected override bool OnWaitForChannel(TimeSpan timeout)
            => throw new NotSupportedException();

        protected override IAsyncResult OnBeginWaitForChannel(TimeSpan timeout, AsyncCallback callback, object state)
            => throw new NotSupportedException();

        protected override bool OnEndWaitForChannel(IAsyncResult result)
            => throw new NotSupportedException();

        #endregion

        class ServerTcpDuplexSessionChannel : WseTcpDuplexSessionChannel
        {
            public ServerTcpDuplexSessionChannel(MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager,
                Socket socket, EndpointAddress localAddress, ChannelManagerBase channelManager)
                : base(messageEncoderFactory, bufferManager, WseTcpDuplexSessionChannel.AnonymousAddress, localAddress,
                WseTcpDuplexSessionChannel.AnonymousAddress.Uri, channelManager)
            {
                base.InitializeSocket(socket);
            }
        }
    }
}
