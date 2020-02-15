using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Xml;

namespace HyperVWcfTransport.Common
{
    class HyperVNetBindingElement
        : TransportBindingElement // to signal that we're a transport
        , IPolicyExportExtension // for policy export
    {
        public HyperVNetBindingElement()
            : base()
        {
        }

        protected HyperVNetBindingElement(HyperVNetBindingElement other)
            : base(other)
        {
        }

        public override string Scheme
        {
            get { return "hypervnb"; }
        }

        public override IChannelFactory<TChannel> BuildChannelFactory<TChannel>(BindingContext context)
        {
            return (IChannelFactory<TChannel>)(object)new HyperVNetChannelFactory(this, context);
        }

        public override IChannelListener<TChannel> BuildChannelListener<TChannel>(BindingContext context)
        {
            return (IChannelListener<TChannel>)(object)new HyperVNetChannelListener(this, context);
        }

        // We only support IDuplexSession for our client ChannelFactories
        public override bool CanBuildChannelFactory<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return true;
            }

            return false;
        }

        // We only support IDuplexSession for our Listeners
        public override bool CanBuildChannelListener<TChannel>(BindingContext context)
        {
            if (typeof(TChannel) == typeof(IDuplexSessionChannel))
            {
                return true;
            }

            return false;
        }

        public override BindingElement Clone()
        {
            return new HyperVNetBindingElement(this);
        }

        public override T GetProperty<T>(BindingContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            // default to MTOM if no encoding is specified
            if (context.BindingParameters.Find<MessageEncodingBindingElement>() == null)
            {
                context.BindingParameters.Add(new MtomMessageEncodingBindingElement());
            }

            return base.GetProperty<T>(context);
        }

        // We expose in policy The fact that we're TCP.
        // Import is done through TcpBindingElementImporter.
        void IPolicyExportExtension.ExportPolicy(MetadataExporter exporter, PolicyConversionContext context)
        {
            if (exporter == null)
            {
                throw new ArgumentNullException("exporter");
            }

            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            ICollection<XmlElement> bindingAssertions = context.GetBindingAssertions();
            XmlDocument xmlDocument = new XmlDocument();
            const string prefix = "hv";
            const string transportAssertion = "hypervnb";
            const string tcpPolicyNamespace = "urn:rmg:hyperv:nb";
            bindingAssertions.Add(xmlDocument.CreateElement(prefix, transportAssertion, tcpPolicyNamespace));
        }
    }
}
