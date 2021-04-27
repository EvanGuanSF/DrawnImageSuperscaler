using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Configuration;
using System;

namespace DrawnImageSuperscaler
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

            IEnumerable<string> listOfStuff;

            bool recurseFolders = bool.TryParse(ConfigurationManager.AppSettings["RecurseFolders"] ?? "false", out recurseFolders);

            // These are the current file types supported natively by Waifu2x-Caffe
            if(recurseFolders)
            {
                listOfStuff = Directory.EnumerateFileSystemEntries(directory, "*.*", SearchOption.AllDirectories)
                .Where(s => s.EndsWith(".bmp") || s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".tif") || s.EndsWith(".tiff"));
            }
            else
            {
                listOfStuff = Directory.EnumerateFileSystemEntries(directory, "*.*", SearchOption.TopDirectoryOnly)
                .Where(s => s.EndsWith(".bmp") || s.EndsWith(".png") || s.EndsWith(".jpg") || s.EndsWith(".jpeg") || s.EndsWith(".tif") || s.EndsWith(".tiff"));
            }

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
