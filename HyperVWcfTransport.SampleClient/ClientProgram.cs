using HyperVWcfTransport.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HyperVWcfTransport.SampleClient
{
    class Program
    {
        static void Main(string[] args)
        {
            Thread.Sleep(250);

            var client = new ServerClient(new EndpointAddress("wse.tcp://192.168.190.81:4056/"));
            client.Open();
            Console.WriteLine(client.DoThing("bar"));
            Console.WriteLine(client.DoThing(Console.ReadLine()));
            client.Close();
            Console.ReadLine();
        }
    }

    class ServerClient : ClientBase<IServer>, IServer
    {
        public ServerClient(EndpointAddress addy)
            : base(new CustomNetTcpBinding(), addy)
        {
        }

        public string DoThing(string foo) => Channel.DoThing(foo);
    }
}
