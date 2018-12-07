using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Configuration;
using System;

namespace WaifuEnlargerAndBatchOptimizerOperations
{
    static class ImageAcquirer
    {
        /// <summary>
        /// Returns a list of images with valid extensions in a directory.
        /// </summary>
        /// <param name="directory"></param>
        /// <returns>List of paths</returns>
        public static List<string> GetImagesFromDirectory(string directory)
        {
            List<string> newList = new List<string>(); ;
            if (!Directory.Exists(directory))
            {
                return newList;
            }

            var listOfStuff = Directory.EnumerateFiles(directory, "*.*", SearchOption.TopDirectoryOnly)
            .Where(s => s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".bmp"));

            int count = listOfStuff.Count();
            string[] arrayOfStuff;

            if (count > 0)
            {
                arrayOfStuff = new string[count - 1];
                arrayOfStuff = listOfStuff.ToArray();
            }
            else
            {
                arrayOfStuff = new string[0];
            }

            newList = arrayOfStuff.OrderBy(x => x, new CustomComparer<string>(NaturalComparer.CompareNatural)).ToList();

            // Filter out optimized or unready files.
            newList.RemoveAll(s => !s.Contains(ConfigurationManager.AppSettings["UnprocessedImageFlagString"]));
            
            return newList;
        }
    }
}
