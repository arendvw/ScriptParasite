using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudioAvw.Gh.Parasites
{
    public static class DebounceHelper
    {
        public static Action Debounce<T>(Func<T> func, int milliseconds)
        {
            CancellationTokenSource cancelTokenSource = null;

            return () =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted)
                        {
                            func();
                        }
                    }, TaskScheduler.Default);
            };
        }

        public static Action Debounce(this Action func, int milliseconds)
        {
            CancellationTokenSource cancelTokenSource = null;

            return () =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted && !t.IsCanceled)
                        {
                            func();
                        }
                    }, TaskScheduler.Default);
            };
        }

        public static Action<T> Debounce<T>(this Action<T> func, int milliseconds)
        {
            CancellationTokenSource cancelTokenSource = null;

            return arg =>
            {
                cancelTokenSource?.Cancel();
                cancelTokenSource = new CancellationTokenSource();

                Task.Delay(milliseconds, cancelTokenSource.Token)
                    .ContinueWith(t =>
                    {
                        if (t.IsCompleted)
                        {
                            func(arg);
                        }
                    }, TaskScheduler.Default);
            };
        }
    }
}
