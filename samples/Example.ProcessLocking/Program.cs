using System;
using System.Threading;
using System.Threading.Tasks;
using Tandem;
using Tandem.Managers;

namespace Example.ProcessLocking
{
    class Program
    {
        static SemaphoreSlim ProceedSemaphore = new SemaphoreSlim(0, 1);

        static void Main(string[] args) => MainAsync(args).Wait();

        static async Task MainAsync(string[] args) {
            ILockManager manager = new SlimLockManager();

            A1(manager);
            A2(manager);

            await ProceedSemaphore.WaitAsync();
            Console.WriteLine("Done!");
        }

        static async void A1(ILockManager manager) {
            using (var h = await manager.LockAsync("tandem://devices/wow")) {
                await Task.Delay(2000);
            }
        }

        static async void A2(ILockManager manager) {
            // wait one second
            await Task.Delay(1000);

            // check if it's locked
            if (await manager.IsLockedAsync("tandem://devices/Wow"))
                Console.WriteLine("It's locked!");

            // now try and obtain a lock
            await Task.Delay(2000);

            using (var h = await manager.LockAsync("tandem://devices/wow")) {
                if (h == null)
                    Console.WriteLine("Couldn't get a lock!");
                else {
                    Console.WriteLine("Got a lock!");
                    ProceedSemaphore.Release();
                }
            }
        }
    }
}
