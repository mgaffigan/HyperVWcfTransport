using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;

namespace HyperVWcfTransport
{
    class HyperVNetChannelFactory : ChannelFactoryBase<IDuplexSessionChannel>
    {
        private readonly int maxBufferSize;
        BufferManager bufferManager;
        MessageEncoderFactory encoderFactory;

        public HyperVNetChannelFactory(HyperVNetBindingElement bindingElement, BindingContext context)
            : base(context.Binding)
        {
            // populate members from binding element
            this.maxBufferSize = (int)bindingElement.MaxReceivedMessageSize;
            this.bufferManager = BufferManager.CreateBufferManager(bindingElement.MaxBufferPoolSize, maxBufferSize);

            var messageEncoderElement = context.BindingParameters
                .OfType<MessageEncodingBindingElement>()
                .SingleOrDefault()
                ?? new MtomMessageEncodingBindingElement();
            this.encoderFactory = messageEncoderElement.CreateMessageEncoderFactory();
        }

        #region Open

        protected override void OnOpen(TimeSpan timeout)
        {
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            CompletedAsyncResult.End(result);
        }

        #endregion

        protected override IDuplexSessionChannel OnCreateChannel(EndpointAddress remoteAddress, Uri via)
        {
            return new HyperVNetClientDuplexSessionChannel(encoderFactory, bufferManager, maxBufferSize, remoteAddress, via, this);
        }
    }
}
