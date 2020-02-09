using System.ServiceModel.Channels;

namespace HyperVWcfTransport.Common
{
    public class CustomNetTcpBinding : Binding, IBindingRuntimePreferences
    {
        WseTcpTransportBindingElement transport;
        BinaryMessageEncodingBindingElement encoding;

        public CustomNetTcpBinding()
        {
            transport = new WseTcpTransportBindingElement();
            encoding = new BinaryMessageEncodingBindingElement();
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
