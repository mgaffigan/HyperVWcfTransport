using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace HyperVWcfTransport
{
    [ServiceContract]
    public interface IServer
    {
        [OperationContract]
        byte[] DoThing(string foo);
    }
}
