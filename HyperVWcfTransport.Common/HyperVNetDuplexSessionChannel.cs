using System;
using System.Net.Sockets;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace HyperVWcfTransport
{
    abstract class HyperVNetDuplexSessionChannel : ChannelBase, IDuplexSessionChannel
    {
        int maxBufferSize;
        BufferManager bufferManager;
        Socket socket;
        object readLock = new object();
        object writeLock = new object();

        public EndpointAddress RemoteAddress { get; }

        public Uri Via { get; }

        public static byte[] WseEndRecord = {
                    0x0A, 0x40, 0, 0, // version 0x01+ME, no type, no options
                    0, 0, 0, 0, 0, 0, 0, 0 }; // no lengths


        protected MessageEncoder MessageEncoder { get; }

        internal static readonly EndpointAddress AnonymousAddress =
            new EndpointAddress("http://schemas.xmlsoap.org/ws/2004/08/addressing/role/anonymous");

        protected HyperVNetDuplexSessionChannel(
            MessageEncoderFactory messageEncoderFactory, BufferManager bufferManager, int maxBufferSize,
            EndpointAddress remoteAddress, EndpointAddress localAddress, Uri via, ChannelManagerBase channelManager)
            : base(channelManager)
        {

            this.RemoteAddress = remoteAddress;
            this.LocalAddress = localAddress;
            this.Via = via;
            this.Session = new TcpDuplexSession(this);
            this.MessageEncoder = messageEncoderFactory.CreateSessionEncoder();
            this.bufferManager = bufferManager;
            this.maxBufferSize = maxBufferSize;
        }

        protected void InitializeSocket(Socket socket)
        {
            if (this.socket != null)
            {
                throw new InvalidOperationException("Socket is already set");
            }

            this.socket = socket;
        }

        protected static Exception ConvertSocketException(SocketException socketException, string operation)
        {
            if (socketException.ErrorCode == 10049 // WSAEADDRNOTAVAIL 
                || socketException.ErrorCode == 10061 // WSAECONNREFUSED 
                || socketException.ErrorCode == 10050 // WSAENETDOWN 
                || socketException.ErrorCode == 10051 // WSAENETUNREACH 
                || socketException.ErrorCode == 10064 // WSAEHOSTDOWN 
                || socketException.ErrorCode == 10065) // WSAEHOSTUNREACH
            {
                return new EndpointNotFoundException(string.Format(operation + " error: {0} ({1})", socketException.Message, socketException.ErrorCode), socketException);
            }
            if (socketException.ErrorCode == 10060) // WSAETIMEDOUT
            {
                return new TimeoutException(operation + " timed out.", socketException);
            }
            else
            {
                return new CommunicationException(string.Format(operation + " error: {0} ({1})", socketException.Message, socketException.ErrorCode), socketException);
            }
        }

        #region Send

        public void Send(Message message) => this.Send(message, DefaultSendTimeout);

        public void Send(Message message, TimeSpan timeout)
        {
            base.ThrowIfDisposedOrNotOpen();
            lock (writeLock)
            {
                try
                {
                    var encodedBytes = EncodeMessage(message);
                    WriteData(encodedBytes);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            }
        }

        public IAsyncResult BeginSend(Message message, AsyncCallback callback, object state)
            => BeginSend(message, DefaultSendTimeout, callback, state);

        public IAsyncResult BeginSend(Message message, TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();
            var encodedBytes = this.EncodeMessage(message);

            return Tap.Run(callback, state, async () =>
            {
                try
                {
                    await WriteDataAsync(encodedBytes);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            });
        }

        public void EndSend(IAsyncResult result) => Tap.Complete(result);

        void SocketSend(byte[] buffer) => SocketSend(new ArraySegment<byte>(buffer));

        void SocketSend(ArraySegment<byte> buffer)
        {
            try
            {
                socket.Send(buffer.Array, buffer.Offset, buffer.Count, SocketFlags.None);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Send");
            }
        }

        private async Task SocketSendAsync(ArraySegment<byte> buffer)
        {
            try
            {
                await socket.SendAsync(buffer, SocketFlags.None);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Send");
            }
        }

        #endregion

        #region Receive

        public Message Receive()
        {
            return this.Receive(DefaultReceiveTimeout);
        }

        public Message Receive(TimeSpan timeout)
        {
            base.ThrowIfDisposedOrNotOpen();
            lock (readLock)
            {
                try
                {
                    ArraySegment<byte> encodedBytes = ReadData();
                    return DecodeMessage(encodedBytes);
                }
                catch (SocketException socketException)
                {
                    throw ConvertSocketException(socketException, "Receive");
                }
            }
        }

        private async Task<Message> ReceiveAsync()
        {
            try
            {
                var data = await ReadDataAsync();
                return DecodeMessage(data);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Receive");
            }
        }

        public IAsyncResult BeginReceive(AsyncCallback callback, object state)
        {
            return BeginReceive(DefaultReceiveTimeout, callback, state);
        }

        public IAsyncResult BeginReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();
            return Tap.Run(callback, state, ReceiveAsync);
        }

        public Message EndReceive(IAsyncResult result)
            => Tap.Complete<Message>(result);

        public bool TryReceive(TimeSpan timeout, out Message message)
        {
            try
            {
                message = Receive(timeout);
                return true;
            }
            catch (TimeoutException)
            {
                message = null;
                return false;
            }
        }

        public IAsyncResult BeginTryReceive(TimeSpan timeout, AsyncCallback callback, object state)
        {
            base.ThrowIfDisposedOrNotOpen();

            return Tap.Run(callback, state, () => ReceiveAsync());
        }

        public bool EndTryReceive(IAsyncResult result, out Message message)
        {
            try
            {
                message = Tap.Complete<Message>(result);
                return true;
            }
            catch (TimeoutException)
            {
                message = null;
                return false;
            }
        }

        #endregion

        #region SocketReceive

        int SocketReceive(byte[] buffer, int offset, int size)
        {
            try
            {
                return socket.Receive(buffer, offset, size, SocketFlags.None);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Receive");
            }
        }

        private async Task<int> SocketReceiveAsync(byte[] buffer, int offset, int size)
        {
            try
            {
                return await socket.ReceiveAsync(new ArraySegment<byte>(buffer, offset, size), SocketFlags.None);
            }
            catch (SocketException socketException)
            {
                throw ConvertSocketException(socketException, "Receive");
            }
        }

        #endregion

        #region SocketReceiveBytes

        byte[] SocketReceiveBytes(int size, bool throwOnEmpty = true)
        {
            int bytesReadTotal = 0;
            int bytesRead = 0;
            byte[] data = bufferManager.TakeBuffer(size);

            while (bytesReadTotal < size)
            {
                bytesRead = SocketReceive(data, bytesReadTotal, size - bytesReadTotal);
                bytesReadTotal += bytesRead;
                if (bytesRead == 0)
                {
                    if (bytesReadTotal == 0 && !throwOnEmpty)
                    {
                        bufferManager.ReturnBuffer(data);
                        return null;
                    }
                    else
                    {
                        throw new CommunicationException("Premature EOF reached");
                    }
                }
            }

            return data;
        }

        private async Task<byte[]> SocketReceiveBytesAsync(int size, bool throwOnEmpty = true)
        {
            int bytesReadTotal = 0;
            byte[] data = bufferManager.TakeBuffer(size);

            while (bytesReadTotal < size)
            {
                var bytesRead = await SocketReceiveAsync(data, bytesReadTotal, size - bytesReadTotal);
                bytesReadTotal += bytesRead;
                if (bytesRead == 0)
                {
                    if (bytesReadTotal == 0 && !throwOnEmpty)
                    {
                        bufferManager.ReturnBuffer(data);
                        return null;
                    }
                    else
                    {
                        throw new CommunicationException("Premature EOF reached");
                    }
                }
            }

            return data;
        }

        #endregion

        // Address the Message and serialize it into a byte array.
        ArraySegment<byte> EncodeMessage(Message message)
        {
            try
            {
                this.RemoteAddress.ApplyTo(message);
                return MessageEncoder.WriteMessage(message, maxBufferSize, bufferManager);
            }
            finally
            {
                // we've consumed the message by serializing it, so clean up
                message.Close();
            }
        }

        Message DecodeMessage(ArraySegment<byte> data)
        {
            if (data.Array == null)
                return null;
            else
                return MessageEncoder.ReadMessage(data, bufferManager);
        }

        void PrepareDummyRead(byte[] preambleBytes, out int idLength, out int typeLength)
        {
            // drain the ID + TYPE
            idLength = (preambleBytes[4] << 8) + preambleBytes[5];
            typeLength = (preambleBytes[6] << 8) + preambleBytes[7];

            // need to also drain padding
            if ((idLength % 4) > 0)
            {
                idLength += (4 - (idLength % 4));
            }

            if ((typeLength % 4) > 0)
            {
                typeLength += (4 - (typeLength % 4));
            }
        }

        int PrepareDataRead(byte[] preambleBytes, out int bytesToRead)
        {
            // now read the data itself
            int dataLength = (preambleBytes[8] << 24)
                + (preambleBytes[9] << 16)
                + (preambleBytes[10] << 8)
                + preambleBytes[11];

            // total to read should include padding
            bytesToRead = dataLength;
            if ((dataLength % 4) > 0)
            {
                bytesToRead += (4 - (dataLength % 4));
            }
            return dataLength;
        }

        ArraySegment<byte> ReadData()
        {
            // 4 bytes for WSE preamble and 8 bytes for lengths

            byte[] preambleBytes = SocketReceiveBytes(12, false);
            if (preambleBytes == null)
            {
                return new ArraySegment<byte>();
            }

            int idLength, typeLength;

            PrepareDummyRead(preambleBytes, out idLength, out typeLength);

            byte[] dummy = SocketReceiveBytes(idLength + typeLength);

            this.bufferManager.ReturnBuffer(dummy);

            int bytesToRead;
            int dataLength = PrepareDataRead(preambleBytes, out bytesToRead);

            byte[] data = SocketReceiveBytes(bytesToRead);

            if ((preambleBytes[0] & 0x02) == 0)
            {
                byte[] endRecord = SocketReceiveBytes(WseEndRecord.Length);
                for (int i = 0; i < WseEndRecord.Length; i++)
                {
                    if (endRecord[i] != WseEndRecord[i])
                    {
                        throw new CommunicationException("Invalid second framing record");
                    }
                }
                this.bufferManager.ReturnBuffer(endRecord);
            }
            this.bufferManager.ReturnBuffer(preambleBytes);

            return new ArraySegment<byte>(data, 0, dataLength);
        }

        async Task<ArraySegment<byte>> ReadDataAsync()
        {
            // 4 bytes for WSE preamble and 8 bytes for lengths

            byte[] preambleBytes = await SocketReceiveBytesAsync(12, false);
            if (preambleBytes == null)
            {
                return new ArraySegment<byte>();
            }

            int idLength, typeLength;

            PrepareDummyRead(preambleBytes, out idLength, out typeLength);

            byte[] dummy = await SocketReceiveBytesAsync(idLength + typeLength);

            this.bufferManager.ReturnBuffer(dummy);

            int bytesToRead;
            int dataLength = PrepareDataRead(preambleBytes, out bytesToRead);

            byte[] data = await SocketReceiveBytesAsync(bytesToRead);

            if ((preambleBytes[0] & 0x02) == 0)
            {
                byte[] endRecord = await SocketReceiveBytesAsync(WseEndRecord.Length);
                for (int i = 0; i < WseEndRecord.Length; i++)
                {
                    if (endRecord[i] != WseEndRecord[i])
                    {
                        throw new CommunicationException("Invalid second framing record");
                    }
                }
                this.bufferManager.ReturnBuffer(endRecord);
            }
            this.bufferManager.ReturnBuffer(preambleBytes);

            return new ArraySegment<byte>(data, 0, dataLength);
        }

        private async Task WriteDataAsync(ArraySegment<byte> data)
        {
            var buffer = GetPreDataBuffer(data);
            try
            {
                await SocketSendAsync(buffer);
                await SocketSendAsync(data);

                if ((data.Count % 4) > 0) // need to pad data to multiple of 4 bytes as well
                {
                    byte[] padBytes = new byte[4 - (data.Count % 4)];
                    await SocketSendAsync(new ArraySegment<byte>(padBytes));
                }
            }
            finally
            {
                bufferManager.ReturnBuffer(buffer.Array);
            }
        }

        void WriteData(ArraySegment<byte> data)
        {
            var buffer = GetPreDataBuffer(data);
            try
            {
                SocketSend(buffer);
                SocketSend(data);

                if ((data.Count % 4) > 0) // need to pad data to multiple of 4 bytes as well
                {
                    byte[] padBytes = new byte[4 - (data.Count % 4)];
                    SocketSend(padBytes);
                }
            }
            finally
            {
                bufferManager.ReturnBuffer(buffer.Array);
            }
        }

        ArraySegment<byte> GetPreDataBuffer(ArraySegment<byte> data)
        {
            byte[] ID = { 0x00, 0x00, 0x00, 0x00 };

            // WSE 3.0 uses the SOAP namespace
            byte[] WsePreamble = {
                0x0E, // version 0x01+MB+ME
                0x20, 0, 0 }; // TYPE_T=URI, no options

            byte[] TYPE;

            if (MessageEncoder.MessageVersion.Envelope == EnvelopeVersion.Soap11)
            {
                TYPE = Encoding.UTF8.GetBytes("http://schemas.xmlsoap.org/soap/envelope/");
            }
            else
            {
                TYPE = Encoding.UTF8.GetBytes("http://www.w3.org/2003/05/soap-envelope");
            }

            // then get the length fields(8 bytes)
            byte[] lengthBytes = new byte[] {
                (byte)((ID.Length & 0x0000FF00) >> 8),
                (byte)(ID.Length & 0x000000FF),
                (byte)((TYPE.Length & 0x0000FF00) >> 8),
                (byte)(TYPE.Length & 0x000000FF),
                (byte)((data.Count & 0xFF000000) >> 24),
                (byte)((data.Count & 0x00FF0000) >> 16),
                (byte)((data.Count & 0x0000FF00) >> 8),
                (byte)(data.Count & 0x000000FF)
                };

            // need to pad to multiple of 4 bytes
            int padLength = 4 - (TYPE.Length % 4);

            int sendLength = TYPE.Length
                + WsePreamble.Length
                + lengthBytes.Length
                + ID.Length
                + padLength;

            byte[] buffer = bufferManager.TakeBuffer(sendLength);
            bool success = false;
            try
            {
                int offset = 0;
                Buffer.BlockCopy(WsePreamble, 0, buffer, offset, WsePreamble.Length);
                offset += WsePreamble.Length;

                Buffer.BlockCopy(lengthBytes, 0, buffer, offset, lengthBytes.Length);
                offset += lengthBytes.Length;

                Buffer.BlockCopy(ID, 0, buffer, offset, ID.Length);
                offset += ID.Length;

                Buffer.BlockCopy(TYPE, 0, buffer, offset, TYPE.Length);

                success = true;
            }
            finally
            {
                if (!success)
                {
                    bufferManager.ReturnBuffer(buffer);
                }
            }

            return new ArraySegment<byte>(buffer, 0, sendLength);
        }

        protected override IAsyncResult OnBeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
        {
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndOpen(IAsyncResult result)
        {
            CompletedAsyncResult.End(result);
        }

        protected override void OnOpen(TimeSpan timeout)
        {
        }

        protected override void OnAbort()
        {
            if (this.socket != null)
            {
                socket.Close(0);
            }
        }

        protected override void OnClose(TimeSpan timeout)
        {
            socket.Close((int)timeout.TotalMilliseconds);
        }

        protected override IAsyncResult OnBeginClose(TimeSpan timeout, AsyncCallback callback, object state)
        {
            socket.Close((int)timeout.TotalMilliseconds);
            return new CompletedAsyncResult(callback, state);
        }

        protected override void OnEndClose(IAsyncResult result)
        {
            CompletedAsyncResult.End(result);
        }

        public EndpointAddress LocalAddress { get; }

        public IDuplexSession Session { get; }

        class TcpDuplexSession : IDuplexSession
        {
            HyperVNetDuplexSessionChannel channel;
            string id;

            public TcpDuplexSession(HyperVNetDuplexSessionChannel channel)
            {
                this.channel = channel;
                this.id = Guid.NewGuid().ToString();
            }

            public void CloseOutputSession(TimeSpan timeout)
            {
                if (channel.State != CommunicationState.Closing)
                {
                    channel.ThrowIfDisposedOrNotOpen();
                }
                channel.socket.Shutdown(SocketShutdown.Send);
            }

            public IAsyncResult BeginCloseOutputSession(TimeSpan timeout, AsyncCallback callback, object state)
            {
                CloseOutputSession(timeout);
                return new CompletedAsyncResult(callback, state);
            }

            public IAsyncResult BeginCloseOutputSession(AsyncCallback callback, object state)
            {
                return BeginCloseOutputSession(channel.DefaultCloseTimeout, callback, state);
            }

            public void EndCloseOutputSession(IAsyncResult result)
            {
                CompletedAsyncResult.End(result);
            }

            public void CloseOutputSession()
            {
                CloseOutputSession(channel.DefaultCloseTimeout);
            }


            public string Id
            {
                get { return this.id; }
            }

        }

        public IAsyncResult BeginWaitForMessage(TimeSpan timeout, AsyncCallback callback, object state)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public bool EndWaitForMessage(IAsyncResult result)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public bool WaitForMessage(TimeSpan timeout)
        {
            throw new Exception("The method or operation is not implemented.");
        }
    }
}
