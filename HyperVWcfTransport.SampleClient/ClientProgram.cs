using HyperVWcfTransport;
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

            var client = new ServerClient(new EndpointAddress("hypervnb://e0e16197-dd56-4a10-9195-5ee7a155a838/C7240163-6E2B-4466-9E41-FF74E7F0DE47"));
            client.Open();
            var d = client.DoThing("bar");
            Console.WriteLine(d.Length);
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

        public byte[] DoThing(string foo) => Channel.DoThing(foo);
    }
}
