using System;

namespace HyperVWcfTransport.Common
{
    //A strongly typed AsyncResult
    abstract class TypedAsyncResult<T> : AsyncResult
    {
        T data;

        protected TypedAsyncResult(AsyncCallback callback, object state)
            : base(callback, state)
        {
        }

        public T Data
        {
            get { return data; }
        }

        protected void Complete(T data, bool completedSynchronously)
        {
            this.data = data;
            Complete(completedSynchronously);
        }

        public static T End(IAsyncResult result)
        {
            TypedAsyncResult<T> typedResult = AsyncResult.End<TypedAsyncResult<T>>(result);
            return typedResult.Data;
        }
    }
}
