using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tandem;
using Tandem.Managers;

namespace Example.MassLocking
{
    class Program
    {
        static async Task Main(string[] args) {
            // setup redis lock manager
            ConnectionMultiplexer connectionMultiplexer = await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            ILockManager manager = new RedisLockManager(connectionMultiplexer, "MyServer");

            // generate our locks
            List<string> locks = Enumerable.Repeat(0, 2048).Select(i => $"tandem://{Guid.NewGuid().ToString()}").ToList();
            List<ILockHandle> lockHandles = new List<ILockHandle>();

            // create stopwatch
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            // setup
            foreach(string l in locks) {
                lockHandles.Add(await manager.LockAsync(l, TimeSpan.FromSeconds(5)).ConfigureAwait(false));
            }

            // time
            stopwatch.Stop();
            Console.WriteLine($"Added 1024 locks in {stopwatch.Elapsed.TotalMilliseconds}ms");

            Random r = new Random();

            while(lockHandles.Count > 0) {
                // get a random lock from the list and remove it
                int index = r.Next(0, lockHandles.Count);
                ILockHandle l = lockHandles[index];
                lockHandles.RemoveAt(index);

                // release it
                l.Dispose();

                await Task.Delay(50);
            }
        }
    }
}
