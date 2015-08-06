using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Throttling
{
    internal class SingleUsingCollection
    {
        private  Collection<long> allTicks = new Collection<long>();
        private readonly static TimeSpan Period = TimeSpan.FromSeconds(10);
        private readonly static int maxReqAllowedInPeriod = 100;

        private readonly static Stopwatch sp = new Stopwatch();
        private readonly static long ticksInPeriod = Period.Ticks;


        private readonly static int totalRequestsToSend = 1000 * 1000 * 1000;

        public void Run()
        {
            for (var i = 0; i <= totalRequestsToSend; i++)
            {
                Console.SetCursorPosition(1, 1);
                Console.WriteLine(string.Format("Total Ticks: {0}   ", allTicks.Count));

                Console.SetCursorPosition(1, 2);
                Console.WriteLine(string.Format("Total Calls: {0}   ", i));



                long TryAfterTicks = 0;

                sp.Start();
                    var bShouldDoIt = ShouldDoIt(ref TryAfterTicks);
                sp.Stop();
                Console.SetCursorPosition(1, 5);
                // Process ticks is propotional with # of requests allowed
                // requests allowed is the maximum size of the collection
                // the bigger the array the higher processing time. 
                // try modifying the maximum allowed requests to see the impact
                Console.WriteLine(string.Format("Process Ticks: {0}  ", sp.ElapsedTicks));
                sp.Reset();



                Console.SetCursorPosition(1, 3);
                if (bShouldDoIt)
                    Console.WriteLine(string.Format("Should do it: {0} \t\t\t\t\t\t ", bShouldDoIt));
                else
                    Console.WriteLine(string.Format("Should do it: {0}  Try After {1} Milliseconds", bShouldDoIt, TryAfterTicks / TimeSpan.TicksPerMillisecond));

                Console.SetCursorPosition(1, 4);
                Console.WriteLine(string.Format("current minute/secound {0}/{1}", DateTime.Now.Minute, DateTime.Now.Second));


                Thread.Sleep(TimeSpan.FromMilliseconds((new Random()).Next(1, 5) * 10));
            }

        }
        public bool ShouldDoIt(ref long TryAfterTicks)
        { 
            
            long nowTicks = DateTime.Now.Ticks;

            // optimize: 
            // trim only when the first request is 
            // older than the period.
            // The below also means a client which sends small # of requests
            // will get slightly longer requst time, since more often than not
            // a trim will occure. 

            if (allTicks.Count > 0 && allTicks[0] < (nowTicks - ticksInPeriod))
                Trim(nowTicks - ticksInPeriod);



            var bOk = (allTicks.Count + 1 <= maxReqAllowedInPeriod);
            if (bOk)
                allTicks.Add(nowTicks);
            else
                TryAfterTicks = (allTicks[0] + ticksInPeriod) - nowTicks;

            return bOk;
        }

        public void Trim(long TicksBefore)
        {
            allTicks = new Collection<long>(
                    allTicks.Where(tick =>
                    {
                        return (tick >= TicksBefore);
                    }).ToList()
                    );
        }
    }
}
