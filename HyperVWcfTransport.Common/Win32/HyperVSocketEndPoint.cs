using static HyperVWcfTransport.Win32.NativeMethods;
using System;
using System.Net;
using System.Net.Sockets;

namespace HyperVWcfTransport.Common
{
    [Serializable]
    internal class HyperVSocketEndPoint : EndPoint
    {
        public HyperVSocketEndPoint(Guid vmid, Guid serviceid)
        {
            this.VmId = vmid;
            this.ServiceId = serviceid;
        }

        public HyperVSocketEndPoint(string host, string service)
            : this(Guid.Parse(host), Guid.Parse(service))
        {
        }

        public static HyperVSocketEndPoint Parse(Uri uri)
        {
            if (uri.Segments.Length != 2)
            {
                throw new FormatException("Unexpected number of path segments");
            }
            if (!Guid.TryParse(uri.Host, out var vmid))
            {
                throw new FormatException($"Invalid VMID format '{uri.Host}'");
            }
            if (!Guid.TryParse(uri.Segments[1], out var serviceid))
            {
                throw new FormatException($"Invalid service format '{uri.Segments[1]}'");
            }
            return new HyperVSocketEndPoint(vmid, serviceid);
        }

        public override AddressFamily AddressFamily => AF_HYPERV;

        public Guid VmId { get; set; }

        public Guid ServiceId { get; set; }

        public override EndPoint Create(SocketAddress sockAddr)
        {
            if (sockAddr == null ||
                sockAddr.Family != AF_HYPERV ||
                sockAddr.Size != 34)
            {
                return null;
            }

            var sockAddress = sockAddr.ToString();
            return new HyperVSocketEndPoint(
                vmid: new Guid(sockAddress.Substring(4, 16)),
                serviceid: new Guid(sockAddress.Substring(20, 16)));
        }

        public override bool Equals(object obj)
        {
            return obj is HyperVSocketEndPoint endpoint
                && this.VmId == endpoint.VmId && this.ServiceId == endpoint.ServiceId;
        }

        public override int GetHashCode() => ServiceId.GetHashCode() ^ VmId.GetHashCode();

        public override SocketAddress Serialize()
        {
            var sockAddress = new SocketAddress(AF_HYPERV, HYPERV_SOCK_ADDR_SIZE);
            void Copy(byte[] source, int offset)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    sockAddress[offset + i] = source[i];
                }
            }

            sockAddress[2] = (byte)0;
            Copy(VmId.ToByteArray(), 4);
            Copy(ServiceId.ToByteArray(), 20);

            return sockAddress;
        }

        public override string ToString() => $"{VmId}/{ServiceId}";
    }
}