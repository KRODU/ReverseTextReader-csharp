using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReverseTextReader
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            int cnt = 1;
            using (var reader = new ReverseTextReader(@"D:\TEST.txt", 4096, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReverseRead("\r\n")) != null)
                {
                    Console.WriteLine($"{cnt++} str: {line}");
                    Thread.Sleep(1000);
                    reader.FileTruncateReadStr();
                }
            }
            Console.WriteLine($"초: {stopWatch.Elapsed.TotalSeconds}");

            Console.ReadKey();
        }
    }
}
