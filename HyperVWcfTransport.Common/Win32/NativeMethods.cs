using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace HyperVWcfTransport.Win32
{
    internal static class NativeMethods
    {
        public const ProtocolType HV_PROTOCOL_RAW = (ProtocolType)1;
        public const AddressFamily AF_HYPERV = (AddressFamily)34;
        public const int HYPERV_SOCK_ADDR_SIZE = 36;
    }
}
