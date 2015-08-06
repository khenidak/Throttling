using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Throttling
{
    class Program
    {

        

        static void Main(string[] args)
        {
          


            /*
            ** single thread using a collection.


            var singleUsingCollection = new SingleUsingCollection();
            singleUsingCollection.Run();
            */


            /*
            ** multi thread using a collection.
            ** if you used more than 50 thread you will get console.write exceptions

            var  multiUsingCollection = new MultiUsingCollection();
            multiUsingCollection.Run();
            */

            /*
                multi threaded not using console, to load large # of threads.
            
                var multiNoConsole = new MultiUsingCollectionNoConsole();
                multiNoConsole.Run();
            */

            /*
                Multi No Lock
            */
            var multiNoLock = new MultiNoLock();
                multiNoLock.Run();

            Console.Read();

        }


    }
}
