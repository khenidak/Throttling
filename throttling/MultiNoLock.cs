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
    internal class MultiNoLock
    {
        // these values are driven from config
        private readonly static TimeSpan Period           = TimeSpan.FromSeconds(5);
        private readonly static int maxReqAllowedInPeriod = 100;
        private readonly static long ticksInPeriod        = Period.Ticks;
        private readonly static int totalRequestsToSend   = 1000 * 1000;
        private readonly static int numOfThreads          = 10; // think of it as # of concurrent Requests



        // these values are per every throttling component
        private long[] allTicks = new long[maxReqAllowedInPeriod];
        private int head = 0; // head 
        private int tail = -1; // tail

        // wait for all threads
        private CountdownEvent cde = new CountdownEvent(numOfThreads);

        // this will slow the console.write down. 
        // but will have no effect on the actual values 
        // if you want you can remove the console.write and write to stdout or stderr
        // and work with the values 
        private AutoResetEvent _ConsoleLock = new AutoResetEvent(true);
        
        private long totalProcessingTicks = 0;
        private int  totalSucessfulReq    = 0;
        

        private void runThread(object oThreadNum)
        {
            // all the console.SetCursorPosition are meant for clear output. 

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
                    Console.WriteLine(string.Format("head/tail: {0}/{1}   ", head,tail));

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
                // max of 100 ms wait

                Thread.Sleep(TimeSpan.FromMilliseconds((new Random()).Next(1, 10) * 10));
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

        private void setTail(long nowTicks)
        {
            var newTTail = 0;
            // only first call
            if (-1 == tail)
            {
                Interlocked.Exchange(ref tail, newTTail);
                Interlocked.Exchange(ref allTicks[newTTail], nowTicks);
                return;
            }

            long ticksAfter = (nowTicks - ticksInPeriod); // we are not really intersted 
                                                          // in anything older than the period


            var newtail = tail;
            var currenttail = newtail;

            
            // no need to trim if current tail is within period
            if (allTicks[newtail] >= ticksAfter)
                return;// tail is good
           
            
            while (true)
            {
                if (newtail == maxReqAllowedInPeriod - 1)
                    newtail = 0;
                else
                    newtail++;

                // all entries are really old, and a previous thread didn't just set the value
                if (newtail == currenttail && allTicks[newtail] < ticksAfter)
                {
                    // reset place the tail just one place before the head.
                    newtail = (head == 0) ? maxReqAllowedInPeriod - 1 : head - 1;
                    Interlocked.Exchange(ref tail,  newtail);
                    Interlocked.Exchange(ref allTicks[newtail], nowTicks);
                    return;
                }

                // if there are two threads competing for tails
                // by definition one of them will be following the other in the loop
                // this below gurantees that one will set the value for the other 
            
                // is a good tail?
                if (allTicks[newtail] >= ticksAfter)
                {
                    Interlocked.Exchange(ref tail, newtail);
                    return;
                }
            }
        }

        private bool _ShouldDoIt(long nowTicks)
        {
            /*
            as a rule of thumb if your variables assignment is less than 64bit on a 64bit
            machine, then using the = operator should be fine for atomic operation

            hence long = long on a 64 machine is an atomic operation and does not require Interlocked.
            however if you are doing long to long assignment on a 32bit machine then this is 
            not an atomic assignment and will require interlocked. 

            the below uses interlocked everywhere to avoid confusion
            */
            setTail(nowTicks);

            var currentHead = 0;
            Interlocked.Exchange(ref currentHead, head);

            if (
            // meeting some where not at the begining of the track.
                ( (currentHead < maxReqAllowedInPeriod - 1)  && currentHead + 1  == tail )
                ||
            // meeting at the begining of the track
                (0 == tail && (currentHead == maxReqAllowedInPeriod - 1))
               )
                return false;
            // there is a slim possiblity that head has incremented
            // in this case the increment will be lost. it is very slim chance but possible 
            // in the majority of cases this should be acceptable

            if (currentHead < maxReqAllowedInPeriod - 1)
            {
                currentHead++;
                Interlocked.Exchange(ref head, currentHead);
            }
            else
            {
                currentHead = 0;
                Interlocked.Exchange(ref head, 0);
            }
            

            Interlocked.Exchange(ref allTicks[currentHead], nowTicks);

            return true;
        }
        public bool ShouldDoIt(ref long TryAfterTicks)
        {
            long nowTicks = DateTime.Now.Ticks;
            bool bOk = _ShouldDoIt(nowTicks);
            if (!bOk) // you might get skewed results for TryAfter, since tail might be adjusted
                TryAfterTicks = (allTicks[tail] + ticksInPeriod) - nowTicks;
            return bOk;
        }
    }
}
