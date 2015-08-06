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
    internal class MultiUsingCollection
    {
        private  Collection<long> allTicks = new Collection<long>();
        private readonly static TimeSpan Period = TimeSpan.FromSeconds(10);
        private readonly static int maxReqAllowedInPeriod = 1000;

        private readonly static long ticksInPeriod = Period.Ticks;


        private readonly static int totalRequestsToSend =  1000 * 1000;
        private readonly static int numOfThreads = 50; // think of it as # of concurrent Requests


        private CountdownEvent cde = new CountdownEvent(numOfThreads);
        private AutoResetEvent _lock = new AutoResetEvent(true);

        // this will slow the console.write down. 
        // but will have no effect on the actual values 

        private AutoResetEvent _ConsoleLock = new AutoResetEvent(true);
        private long totalProcessingTicks = 0;
        private int totalSucessfulReq = 0;

        private void runThread(object oThreadNum)
        {
            int threadNum = (int)oThreadNum;
            Stopwatch sp = new Stopwatch();
            var startingLine = threadNum * 6;

            var reqForThisThread = totalRequestsToSend / numOfThreads;

            _ConsoleLock.WaitOne();
            Console.SetCursorPosition(1, 0 + startingLine); // aka first
            Console.WriteLine(string.Format("Thread # {0}:", threadNum));
            _ConsoleLock.Set();

            for (var i = 0; i <= reqForThisThread; i++)
            {
                _ConsoleLock.WaitOne();
                    Console.SetCursorPosition(1, 1 + startingLine);
                    Console.WriteLine(string.Format("Total Ticks: {0}   ", allTicks.Count));

                    Console.SetCursorPosition(1, 2 + startingLine);
                    Console.WriteLine(string.Format("Total Calls: {0}   ", i));
                _ConsoleLock.Set();

                long TryAfterTicks = 0;

                sp.Start();
                    var bShouldDoIt = ShouldDoIt(ref TryAfterTicks);
                var ElabsedTicks = sp.ElapsedTicks;

                Interlocked.Add(ref totalProcessingTicks, ElabsedTicks);

                if (bShouldDoIt)
                    Interlocked.Increment(ref totalSucessfulReq);

                sp.Stop();
                sp.Reset();

                _ConsoleLock.WaitOne();
                Console.SetCursorPosition(1, 5 + startingLine);
                Console.WriteLine(string.Format("Process Ticks: {0}  ", ElabsedTicks));
                Debug.WriteLine(string.Format("Process Ticks: {0}  ", ElabsedTicks));
                _ConsoleLock.Set();


                _ConsoleLock.WaitOne();
                Console.SetCursorPosition(1, 3 + startingLine);
                if (bShouldDoIt)
                    Console.WriteLine(string.Format("Should do it: {0} \t\t\t\t\t\t ", bShouldDoIt));
                else
                    Console.WriteLine(string.Format("Should do it: {0}  Try After {1} Milliseconds", bShouldDoIt, TryAfterTicks / TimeSpan.TicksPerMillisecond));

                Console.SetCursorPosition(1, 4 + startingLine);
                Console.WriteLine(string.Format("current minute/secound {0}/{1}", DateTime.Now.Minute, DateTime.Now.Second));
                _ConsoleLock.Set();


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
            // some requets too much to identify go/no go decision (i.e 10ms) (with 50 thread) (time waiting for lock release)
            // if you are doing SLA (or similar commitment) you will be in trouble
            Debug.WriteLine("Threads {0} - Averging Processing is @ {1} ms per request" ,threads.Length, totalProcessingTicks / TimeSpan.TicksPerMillisecond/  totalSucessfulReq  );
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
