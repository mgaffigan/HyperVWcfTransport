using System.ServiceModel.Channels;

namespace HyperVWcfTransport
{
    public class HyperVNetBinding : Binding, IBindingRuntimePreferences
    {
        HyperVNetBindingElement transport;
        BinaryMessageEncodingBindingElement encoding;

        public HyperVNetBinding()
            : this(256)
        {
        }

        public HyperVNetBinding(int mbMaxRead)
        {
            transport = new HyperVNetBindingElement();
            encoding = new BinaryMessageEncodingBindingElement();

            const int mb = 1024 /* kb */ * 1024;
            var limit = mbMaxRead * mb;
            transport.MaxBufferPoolSize = limit;
            transport.MaxReceivedMessageSize = limit;
            encoding.MaxWritePoolSize = limit;
            encoding.ReaderQuotas.MaxBytesPerRead = limit;
            encoding.ReaderQuotas.MaxStringContentLength = limit;
            encoding.ReaderQuotas.MaxArrayLength = limit;
        }

        bool IBindingRuntimePreferences.ReceiveSynchronously
        {
            get { return false; }
        }

        public override string Scheme { get { return transport.Scheme; } }

        public override BindingElementCollection CreateBindingElements()
        {
            var bindingElements = new BindingElementCollection();
            bindingElements.Add(encoding);
            bindingElements.Add(transport);
            return bindingElements.Clone();
        }
    }
}
