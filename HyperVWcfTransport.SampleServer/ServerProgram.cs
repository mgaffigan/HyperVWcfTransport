using HyperVWcfTransport.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace HyperVWcfTransport.SampleServer
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single, ConcurrencyMode = ConcurrencyMode.Multiple, UseSynchronizationContext = false)]
    class SampleServer : IServer
    {
        public string DoThing(string foo)
        {
            Console.WriteLine($"Received {foo}");
            return $"Hello {foo}";
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var sh = new ServiceHost(new SampleServer());
            var binding = new CustomNetTcpBinding();
            sh.AddServiceEndpoint(typeof(IServer), binding, "wse.tcp://192.168.190.81:4056/");
            sh.Open();
            Console.ReadLine();
            sh.Close();
        }
    }
}
