using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CrèmeDeLaCrème
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.SetWindowSize(150, 40);
            Console.SetWindowPosition(0, 0);

            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;

            DateTime startTime = DateTime.Now;

            Console.WriteLine("Crème De La Crème started at".PadRight(45) + ": " + DateTime.Now.ToString("hh:mm:ss tt"));
            Console.WriteLine();

            // Start a timer.
            var watch = Stopwatch.StartNew();
            // Do a sequence of high-quality upscales on the images using Waifu2x - Caffe.
            WaifuScaler.UpYourWaifu(Directory.GetCurrentDirectory());
            watch.Stop();
            DateTime endTime = DateTime.Now;
            
            Console.WriteLine();
            Console.WriteLine("Crème De La Crème finished at".PadRight(45) + ": " + DateTime.Now.ToString("hh:mm:ss tt"));
            Console.WriteLine("Image operations completed in".PadRight(45) + ": " + ReadableTime(TimeSpan.FromMilliseconds(watch.ElapsedMilliseconds)));
            Console.WriteLine("All done.");
            Console.ReadLine();
        }

        /// <summary>
        /// Converts number of bytes into human readable format.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns>string with suffix of highest order byte class</returns>
        private static string FormatBytes(ulong bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024)
            {
                dblSByte = bytes / 1024.0;
            }

            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        /// <summary>
        /// Converts timespan(milliseconds) into human readable format.
        /// </summary>
        /// <param name="ts"></param>
        /// <returns>string with format: hh:mm:ss:ms</returns>
        private static string ReadableTime(TimeSpan ts)
        {
            string readableTime = string.Format("{0:D2}h:{1:D2}m:{2:D2}s:{3:D3}ms",
                ts.Hours,
                ts.Minutes,
                ts.Seconds,
                ts.Milliseconds,
                CultureInfo.InvariantCulture);

            return readableTime;
        }
    }
}
