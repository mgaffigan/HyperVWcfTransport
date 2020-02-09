using System;

namespace HyperVWcfTransport.Common
{
    //An AsyncResult that completes as soon as it is instantiated.
    class CompletedAsyncResult : AsyncResult
    {
        public CompletedAsyncResult(AsyncCallback callback, object state)
            : base(callback, state)
        {
            Complete(true);
        }

        public static void End(IAsyncResult result)
        {
            AsyncResult.End<CompletedAsyncResult>(result);
        }
    }
}
