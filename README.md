# HyperV WCF Transport

Provides a binding and transport to allow HyperV communication across VM boundaries without network connections using [Hyper-V Sockets](https://docs.microsoft.com/en-us/virtualization/hyper-v-on-windows/user-guide/make-integration-service#register-a-new-application).

Example Server running on a guest VM:

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
            var binding = new HyperVNetBinding();
            sh.AddServiceEndpoint(typeof(IServer), binding, "hypervnb://00000000-0000-0000-0000-000000000000/C7240163-6E2B-4466-9E41-FF74E7F0DE47");
            sh.Open();
            Console.ReadLine();
            sh.Close();
        }
    }
	
Example Client running on the Hypervisor:

    class Program
    {
        static void Main(string[] args)
        {
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
	
Notes:

 - Replace `C7240163-6E2B-4466-9E41-FF74E7F0DE47` with the service ID you register in the registry (see Example Hypervisor Service Registration.reg).  When developing a new app, the service ID should be unique and generated specifically for that application.
   - Register your service ID in the hypervisor registry
   - Enable "Guest Services" in the VM settings under "Integration Services"
 - Replace `642d4719-f5d7-477d-9ca3-2c46c280052d` with the ID of the VM to which you want to connect.  The VM ID can be found with Powershell using `Get-VM`.