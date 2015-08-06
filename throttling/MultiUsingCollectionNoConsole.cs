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
    internal class MultiUsingCollectionNoConsole
    {
        private  Collection<long> allTicks = new Collection<long>();
        private readonly static TimeSpan Period = TimeSpan.FromSeconds(10);
        private readonly static int maxReqAllowedInPeriod = 1000;

        private readonly static long ticksInPeriod = Period.Ticks;


        private readonly static int totalRequestsToSend =  1000 * 1000;
        private readonly static int numOfThreads = 100; // think of it as # of concurrent Requests


        private CountdownEvent cde = new CountdownEvent(numOfThreads);
        private AutoResetEvent _lock = new AutoResetEvent(true);

        // this will slow the console.write down. 
        // but will have no effect on the actual values 

        private long totalProcessingTicks = 0;
        private int totalSucessfulReq = 0;
        private void runThread(object oThreadNum)
        {
            int threadNum = (int)oThreadNum;
            Stopwatch sp = new Stopwatch();
            var startingLine = threadNum * 6;

            var reqForThisThread = totalRequestsToSend / numOfThreads;


            for (var i = 0; i <= reqForThisThread; i++)
            {
                long TryAfterTicks = 0;

                sp.Start();
                    var bShouldDoIt = ShouldDoIt(ref TryAfterTicks);
                var ElabsedTicks = sp.ElapsedTicks;

                Interlocked.Add(ref totalProcessingTicks, ElabsedTicks);

                if (bShouldDoIt)
                    Interlocked.Increment(ref totalSucessfulReq);
                sp.Stop();
                sp.Reset();


                var time = string.Format("m/s:{0}/{1}", DateTime.Now.Minute, DateTime.Now.Second);

            if (bShouldDoIt)
                    Console.WriteLine(string.Format("Should do it: {0} - {1}", bShouldDoIt, time));
                else
                    Console.WriteLine(string.Format("Should do it: {0} After {1} ms - {2}", bShouldDoIt, TryAfterTicks / TimeSpan.TicksPerMillisecond, time));

                

                //random sleep, so we wouldn't get uniform values (in total ticks & counts). 
                Thread.Sleep(TimeSpan.FromMilliseconds((new Random()).Next(1, 5) * 10));
            }
            cde.Signal();
        }
        public void Run()
        {
            Thread[] threads = new Thread[numOfThreads];
            for (var i = 0; i < numOfThreads; i++)
            {
                threads[i] = new Thread(this.runThread);
                threads[i].Start(i);
            }
            cde.Wait();

            // although i am printing average, you should look for max. 
            // some requets wait too much 100ms (with 50 thread) + for lock release
            // if you are doing SLA (or similar commitment) you will be in trouble
            Console.WriteLine("Threads {0} - Averging Processing is @ {1} ms per request" ,threads.Length, totalProcessingTicks / TimeSpan.TicksPerMillisecond/ totalSucessfulReq);
        }
        public bool ShouldDoIt(ref long TryAfterTicks)
        {
            // can optimize further with multiple locks, for read and writes. 
            // but for the sake of simplicity we are keep it to one lock

            _lock.WaitOne();
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
            { 
                allTicks.Add(nowTicks);   
            }
            else
                TryAfterTicks = (allTicks[0] + ticksInPeriod) - nowTicks;

            _lock.Set();

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
