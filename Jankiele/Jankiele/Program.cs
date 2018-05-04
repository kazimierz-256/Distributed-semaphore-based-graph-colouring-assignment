using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jankiele
{
    class Program
    {
        static void Main(string[] args)
        {
            var jankielThreads =
                JankielSetup.PrepareJankiels(
                    JankielLoader.CreateJankiels(
                        JankielLoader.ParseJankielFile(
                            JankielLoader.LoadText(
                                @"..\..\Jankiele.txt"))).ToArray()).ToArray();

            foreach (var jankielThread in jankielThreads)
                jankielThread.Start();

            foreach (var jankielThread in jankielThreads)
                jankielThread.Join();

            Console.WriteLine("Concert completed successfully");
        }
    }
}
