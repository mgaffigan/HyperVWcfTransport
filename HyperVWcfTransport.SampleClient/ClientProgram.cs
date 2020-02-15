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

            var client = new ServerClient(new EndpointAddress("hypervnb://642d4719-f5d7-477d-9ca3-2c46c280052d/C7240163-6E2B-4466-9E41-FF74E7F0DE47"));
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
            : base(new HyperVNetBinding(), addy)
        {
        }

        public string DoThing(string foo) => Channel.DoThing(foo);
    }
}
