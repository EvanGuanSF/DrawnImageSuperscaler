using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Collections.Concurrent;

namespace WaifuEmbiggeningAndBatchOptimizationOperations
{
    static class Pinger
    {
        private static BlockingCollection<string> concurrentImageCollection = new BlockingCollection<string>();
        private static int workingThreads = 0;

        /// <summary>
        /// This function runs asynchronously and checks the Pinger object's BlockingCollection
        /// for new work to do (image optimization), limiting the number of threads used to a set amount.
        /// </summary>
        /// <param name="imageList"></param>
        /// <returns>void</returns>
        public static void ConvertIndividualToPNGAsync(CancellationToken cancellationToken)
        {
            // This loop continually checks the BlockingCollection for new images to be processed.
            Parallel.ForEach(concurrentImageCollection.GetConsumingPartitioner(),
                new ParallelOptions { MaxDegreeOfParallelism = (int)(Environment.ProcessorCount * .75) },
                (curImage, loopState) =>
                {
                    // Optimize the image.
                    Interlocked.Increment(ref workingThreads);
                    OptimizeImage(curImage);
                    // Change the image name to mark it as finished.
                    MarkAsProcessed(curImage);
                    
                    Interlocked.Decrement(ref workingThreads);
                    curImage = null;

                    // If a cancel is requested, wait for remaining work to finish and then leave.
                    if (cancellationToken.IsCancellationRequested)
                    {
                        loopState.Break();
                    }

                    // Give the cpu and IO some breathing room.
                    Thread.Sleep(1000);
                    GC.Collect();
                    Thread.Sleep(1000);
                });
        }

        /// <summary>
        /// Image conversion worker thread. Converts image at given path
        /// to uncompressed png format.
        /// </summary>
        /// <param name="imagePath"></param>
        private static void OptimizeImage(object imagePath)
        {
            try
            {
                // Convert using imagemagick.
                using (MagickImage imageActual = new MagickImage(new FileInfo((string)imagePath)))
                {
                    // Check to see if the image is properly formatted for the optimizer.
                    // Convert if it isn't.
                    if (imageActual.Format != MagickFormat.Png)
                    {
                        // Remove the file if we are going to overwrite it anyways.
                        File.Delete(Path.GetFullPath((string)imagePath));
                        // Save picture as a bitmap with a png extension.
                        imageActual.Write((string)imagePath, MagickFormat.Png);
                    }
                    // Now compress.
                    ImageOptimizer optimizer = new ImageOptimizer();

                    optimizer.LosslessCompress((string)imagePath);
                }
                GC.Collect();
            }
            catch (Exception e)
            {
                ExceptionOutput.GetExceptionMessages(e);
            }
            Thread.Sleep(100);
        }

        /// <summary>
        /// Loop through the list and change ☆ to ★ in file name to indicate conversion finished.
        /// If a file with the new name already exists, delete it and then rename the old file.
        /// </summary>
        /// <param name="imageList"></param>
        private static void MarkAsProcessed(string image)
        {
            string newName = null;
            /* Alt:
                * ✨
                * ☆
                * ★
                * 
                */
            newName = image.Replace(char.Parse(ConfigurationManager.AppSettings["UnprocessedImageFlagChar"]),
                char.Parse(ConfigurationManager.AppSettings["ProcessedImageFlagChar"]));

            // Rename the file.
            // If a file with the new name already exists, delete then rename.
            try
            {
                if (File.Exists(newName))
                {
                    File.Delete(newName);
                    File.Move(image, newName);
                }
                else
                {
                    File.Move(image, newName);
                }
            }
            catch (Exception e)
            {
                ExceptionOutput.GetExceptionMessages(e);
            }
        }
        
        public static Partitioner<T> GetConsumingPartitioner<T>(this BlockingCollection<T> collection)
        {
            return new BlockingCollectionPartitioner<T>(collection);
        }

        // Helper funcitons for accessing the Pinger classes' image processing queue/collection.
        #region HelperFunctions

        /// <summary>
        /// Helper funciton for enqueueing an image path into Pinger's BlockingCollection for optimization.
        /// </summary>
        /// <param name="imagePath"></param>
        public static void EnqueueImageForOptimization(string imagePath)
        {
            concurrentImageCollection.Add(imagePath);
        }

        /// <summary>
        /// Helper funciton for returning the number of unprocessed images remaining in the BlockingCollection.
        /// </summary>
        /// <returns>Number of unprocessed images.</returns>
        public static int GetConcurrentImageQueueCount()
        {
            return concurrentImageCollection.Count;
        }

        /// <summary>
        /// 
        /// Helper funciton for getting the number of otpimization threads still active.
        /// </summary>
        /// <returns>Number of active otpimization threads.</returns>
        public static int GetRunningThreadCount()
        {
            return workingThreads;
        }

        #endregion HelperFunctions
    }
}
