using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using static HyperVWcfTransport.Win32.NativeMethods;

namespace HyperVWcfTransport.Common
{
    class HyperVNetChannelListener : ChannelListenerBase<IDuplexSessionChannel>
    {
        BufferManager bufferManager;
        MessageEncoderFactory encoderFactory;
        Socket listenSocket;
        public override Uri Uri { get; }

        public HyperVNetChannelListener(HyperVNetBindingElement bindingElement, BindingContext context)
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
            var localEndpoint = HyperVSocketEndPoint.Parse(this.Uri);
            this.listenSocket = new Socket(AF_HYPERV, SocketType.Stream, HV_PROTOCOL_RAW);
            this.listenSocket.Bind(localEndpoint);
            this.listenSocket.Listen(1);
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
            var dataSocket = listenSocket.Accept();
            return new ServerDuplexSessionChannel(this.encoderFactory, this.bufferManager, dataSocket, new EndpointAddress(Uri), this);
        }

        protected override IAsyncResult OnBeginAcceptChannel(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return Tap.Run(callback, state, async () =>
            {
                try
                {
                    var dataSocket = await listenSocket.AcceptAsync();
                    return (IDuplexSessionChannel)new ServerDuplexSessionChannel(this.encoderFactory, this.bufferManager, dataSocket, new EndpointAddress(this.Uri), this);
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

        class ServerDuplexSessionChannel : HyperVNetDuplexSessionChannel
        {
            public ServerDuplexSessionChannel(MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager,
                Socket socket, EndpointAddress localAddress, ChannelManagerBase channelManager)
                : base(messageEncoderFactory, bufferManager, AnonymousAddress, localAddress,
                AnonymousAddress.Uri, channelManager)
            {
                base.InitializeSocket(socket);
            }
        }
    }
}
