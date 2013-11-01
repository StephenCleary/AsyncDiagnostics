using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.QualityTools.Testing.Fakes.Shims;
using Nito.AsyncEx.AsyncDiagnostics;

[assembly: AsyncDiagnosticAspect(AttributePriority = 1)]

namespace ConsoleApplication1
{
    public static class Test
    {
        static Task cached;

        public static Task MainAsync()
        {
            return MainAsync(CancellationToken.None);
        }

        static async Task MainAsync(CancellationToken token)
        {
            cached = Level2Async();
            Task another = Level2Async();
            var third = Task.Run(() => { throw new InvalidOperationException("third"); });
            var task = Task.WhenAll(cached, another, third);
            try
            {
                await task;
            }
            catch (Exception)
            {
                throw task.Exception;
            }
        }

        static Task Level2Async()
        {
            return Level2Async(CancellationToken.None);
        }

        static async Task Level2Async(CancellationToken token)
        {
            await Task.Delay(1000);
            SynchronousMethodsToo();
        }

        static void SynchronousMethodsToo()
        {
            throw new Exception("test");
        }

        static int Property { get; set; }

        static event EventHandler Event;
    }

    class Program
    {
        static Program()
        {
        }

        public Program()
        {
        }

        private static void Main(string[] args)
        {
            try
            {
                using (AsyncDiagnosticStack.Enter("Hi"))
                {
                    Test.MainAsync().Wait();
                }
            }
            catch (AggregateException ex)
            {
                foreach (var exception in ((AggregateException)ex.InnerException).InnerExceptions)
                    Console.WriteLine(exception.ToAsyncDiagnosticString());
            }

            Console.ReadKey();
        }
    }
}
