using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperVWcfTransport
{
    internal static class TapInterop
    {
        // Copied from https://msdn.microsoft.com/en-us/library/hh873178.aspx
        internal static IAsyncResult AsApm<T>(this Task<T> task,
                                    AsyncCallback callback,
                                    object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<T>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(t.Result);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }

        internal static IAsyncResult AsApm(this Task task,
                            AsyncCallback callback,
                            object state)
        {
            if (task == null)
                throw new ArgumentNullException("task");

            var tcs = new TaskCompletionSource<object>(state);
            task.ContinueWith(t =>
            {
                if (t.IsFaulted)
                    tcs.TrySetException(t.Exception.InnerExceptions);
                else if (t.IsCanceled)
                    tcs.TrySetCanceled();
                else
                    tcs.TrySetResult(null);

                if (callback != null)
                    callback(tcs.Task);
            }, TaskScheduler.Default);
            return tcs.Task;
        }
    }

    internal static class Tap
    {
        public static IAsyncResult Run(AsyncCallback callback, object state, Func<Task> actor) => actor().AsApm(callback, state);
        public static IAsyncResult Run<TResult>(AsyncCallback callback, object state, Func<Task<TResult>> actor) => actor().AsApm(callback, state);

        public static void Complete(IAsyncResult iar) => ((Task)iar).Wait();

        public static TResult Complete<TResult>(IAsyncResult iar) => ((Task<TResult>)iar).Result;
    }
}
