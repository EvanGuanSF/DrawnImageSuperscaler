using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Configuration;

namespace WaifuEmbiggeningAndBatchOptimizationOperations
{
    public static class WaifuScaler
    {
        // Helper enumerator for the ImageOperationType class.
        private enum ImageResolutionClassification : int { VerySmall, Small, Normal, Large, VeryLarge };

        /// <summary>
        /// This class hold the name of the image and whether or not it
        /// requires the special processing order.
        /// </summary>
        private class ImageOperationType
        {
            public ImageResolutionClassification opType;

            public string ImagePath { get; }

            private ImageOperationType()
            {
                ImagePath = null;
                opType = ImageResolutionClassification.Normal;
            }

            public ImageOperationType(string path, ImageResolutionClassification passedOpType)
            {
                ImagePath = path;
                this.opType = passedOpType;
            }
        }

        public static void UpYourWaifu(string directory)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string stageOneNormalInputPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["SourceFolderName"]);
            string stageOneOutputPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["TempFolderName"].ToString());
            string processedImagePath = null;

            int totalImages = 0;

            CancellationTokenSource pingerCancelToken = new CancellationTokenSource();

            // Make the temp folder.
            MakeDirectory(stageOneOutputPath);

            // Get the images in the S&R folder and put their paths in a list
            // in natural order (Windows sort by name ascending).
            List<string> imagePaths = GetImages.GetAllImages(stageOneNormalInputPath);
            List<ImageOperationType> imageOpList = new List<ImageOperationType>();
            int maxLength = 0;

            foreach (string image in imagePaths)
            {
                totalImages++;
                try
                {
                    int test = Path.GetFileName(image).Length;
                    if (test > maxLength)
                        maxLength = test;

                    using (Stream stream = File.OpenRead(image))
                    {
                        using (Image sourceImage = Image.FromStream(stream, false, false))
                        {
                            // Set the operation type for the image to be processed based on the
                            // dimensions of the image.
                            if ((sourceImage.Width * sourceImage.Height) >= 100000000)
                            {
                                // At least 10000x10000
                                imageOpList.Add(new ImageOperationType(image, ImageResolutionClassification.VeryLarge));
                            }
                            else if ((sourceImage.Width * sourceImage.Height) >= 22500000)
                            {
                                // At least 5000x4500
                                imageOpList.Add(new ImageOperationType(image, ImageResolutionClassification.Large));
                            }
                            else if ((sourceImage.Width * sourceImage.Height) >= 786432)
                            {
                                // At least 1024x768
                                imageOpList.Add(new ImageOperationType(image, ImageResolutionClassification.Normal));
                            }
                            else if ((sourceImage.Width * sourceImage.Height) >= 172800)
                            {
                                // At least 480x360
                                imageOpList.Add(new ImageOperationType(image, ImageResolutionClassification.Small));
                            }
                            else
                            {
                                // Smaller than 480x360
                                imageOpList.Add(new ImageOperationType(image, ImageResolutionClassification.VerySmall));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ExceptionOutput.GetExceptionMessages(e);
                }
            }

            // Start a background process for doing work.
            Task optimizationBackgroundTask = Task.Run(() => Pinger.ConvertIndividualToPNGAsync(pingerCancelToken.Token));

            // Allow the user to request the program to stop further processing.
            bool userRequestCancelRemainingOperations = false;
            bool optimizationsCompleted = false;
            Task getCancelInput = Task.Factory.StartNew(() =>
            {
                while (Console.ReadKey(true).Key != ConsoleKey.C && !optimizationsCompleted)
                {
                    Thread.Sleep(250);
                }
                userRequestCancelRemainingOperations = true;
            });

            // Tell the user that the scaling operations are in progress.
            int imagesScaled = 0;

            // Waifu2x - Caffee conversion loop.
            int dotIncrementer = 0;
            foreach (ImageOperationType image in imageOpList)
            {
                if (userRequestCancelRemainingOperations)
                {
                    break;
                }

                Task anJob = Task.Run(() => Waifu2xJobController(image));
                string currentImageName = Path.GetFileName(image.ImagePath);

                while (!anJob.IsCompleted)
                {
                    if (!userRequestCancelRemainingOperations)
                    {
                        Console.Write(("\rUpscaling" + new string('.', (dotIncrementer % 10) + 1) +
                            new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) +
                            ": (" + imagesScaled + "/" + totalImages + ") " +
                            "Now processing: " + currentImageName +
                            new string(' ', maxLength + 10) + new string('\b', maxLength + 11));
                        dotIncrementer++;
                    }
                    else
                    {
                        Console.Write(("\rFinishing current jobs" + new string('.', (dotIncrementer % 10) + 1) +
                            new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) +
                            ": (" + imagesScaled + "/" + totalImages + ") " +
                            "Now processing: " + currentImageName +
                            new string(' ', maxLength + 10) + new string('\b', maxLength + 11));
                        dotIncrementer++;
                    }

                    Thread.Sleep(100);
                }
                anJob.Wait();

                imagesScaled++;
                try
                {
                    File.Delete(Path.Combine(stageOneOutputPath, Path.GetFileName(image.ImagePath)));
                }
                catch (Exception e)
                {
                    ExceptionOutput.GetExceptionMessages(e);
                }

                processedImagePath = Path.Combine(currentDirectory, Path.GetFileName(image.ImagePath));
                // Now enqueue an optimization task.
                Pinger.EnqueueImageForOptimization(processedImagePath);
            }
            if (imagesScaled == totalImages)
            {
                Console.Write("\rFiles converted".PadRight(46) + ": (" + imagesScaled + "/" + totalImages + ") (done)" +
                        new string(' ', maxLength + 40) +
                        new string('\b', maxLength + 41));
                Console.WriteLine();
            }
            else
            {
                Console.Write("\rFiles converted".PadRight(46) + ": (" + imagesScaled + "/" + totalImages + ")" +
                        new string(' ', maxLength + 40) +
                        new string('\b', maxLength + 41));
                Console.WriteLine();
            }

            // *********
            // Initiate, display status of, and check for cancellation of image optimization.
            dotIncrementer = 0;

            while (!userRequestCancelRemainingOperations && !optimizationBackgroundTask.IsCompleted)
            {
                // Scaling is done at this point, so we are only waiting
                // for the ConcurrentImageQueueCount to reach zero and
                // the remaining thread count to reach zero.
                if (Pinger.GetConcurrentImageQueueCount() == 0 && Pinger.GetRunningThreadCount() == 0)
                {
                    // Once that is done, send a cancel request to the task to end it. 
                    pingerCancelToken.Cancel();
                    break;
                }

                Console.Write(("\rOptimizer pass in progress" +
                    new string('.', (dotIncrementer % 10) + 1) +
                    new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) +
                    ": (" + (totalImages - Pinger.GetConcurrentImageQueueCount() - Pinger.GetRunningThreadCount()) +
                    "/" + totalImages + ") images optimized");
                dotIncrementer++;
                Thread.Sleep(100);
            }
            if (userRequestCancelRemainingOperations && (!optimizationBackgroundTask.IsCanceled || !optimizationBackgroundTask.IsCompleted))
            {
                dotIncrementer = 0;
                pingerCancelToken.Cancel();
                while (!optimizationBackgroundTask.IsCompleted && (Pinger.GetConcurrentImageQueueCount() > 0))
                {
                    Console.Write("\rFinishing active optimizations" + new string('.', (dotIncrementer % 10) + 1) +
                        new string(' ', 55) + new string('\b', 56));
                    dotIncrementer++;
                    Thread.Sleep(100);
                }
            }
            if (userRequestCancelRemainingOperations)
            {
                Console.Write("\rOptimizer pass cancelled." + new string(' ', 55) + new string('\b', 56));
                Console.WriteLine();
                return;
            }
            else
            {
                Console.Write("\rOptimizer pass completed".PadRight(46) +
                    ": (" + (totalImages - Pinger.GetConcurrentImageQueueCount()) + "/" + totalImages + ") images optimized" +
                    new string(' ', 55) + new string('\b', 56));
                Console.WriteLine();
            }
            optimizationsCompleted = true;
            CleanupFolders();
        }

        private static void Waifu2xJobController(ImageOperationType image)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string stageOneNormalInputPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["SourceFolderName"]);
            string stageOneOutputPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["TempFolderName"]);
            string latterStageImage = null;
            string fileName = Path.GetFileName(image.ImagePath);

            // Sequential scale and quality operations depending on flag.
            switch (image.opType)
            {
                case ImageResolutionClassification.VeryLarge:
                    {
                        // VeryLarge case. Quality pass before doing a 2x scale with reduced batch size.
                        DoUpTheWaifusDirect(image.ImagePath, Path.Combine(stageOneOutputPath, fileName), 1, 2, 256);
                        latterStageImage = Path.Combine(stageOneOutputPath, fileName);
                        DoUpTheWaifusDirect(latterStageImage, Path.Combine(currentDirectory, fileName), 2, 2, 256);
                        break;
                    }
                case ImageResolutionClassification.Large:
                    {
                        // Large case. Quality pass before doing a 2x scale.
                        DoUpTheWaifusDirect(image.ImagePath, Path.Combine(stageOneOutputPath, fileName), 1, 4, 256);
                        latterStageImage = Path.Combine(stageOneOutputPath, fileName);
                        DoUpTheWaifusDirect(latterStageImage, Path.Combine(currentDirectory, fileName), 2, 4, 256);
                        break;
                    }
                case ImageResolutionClassification.Normal:
                    {
                        // Normal case. 2x scale before doing a quality pass.
                        DoUpTheWaifusDirect(image.ImagePath, Path.Combine(stageOneOutputPath, fileName), 2, 4, 256);
                        latterStageImage = Path.Combine(stageOneOutputPath, fileName);
                        DoUpTheWaifusDirect(latterStageImage, Path.Combine(currentDirectory, fileName), 1, 4, 256);
                        break;
                    }
                case ImageResolutionClassification.Small:
                    {
                        // Small case. Quality pass before doing a 2x scale with modified .
                        DoUpTheWaifusDirect(image.ImagePath, Path.Combine(stageOneOutputPath, fileName), 1, 6, 256);
                        latterStageImage = Path.Combine(stageOneOutputPath, fileName);
                        DoUpTheWaifusDirect(latterStageImage, Path.Combine(currentDirectory, fileName), 2, 6, 256);
                        break;
                    }
                case ImageResolutionClassification.VerySmall:
                    {
                        // Image is WAY too small and may use silly amounts of vram to process. Just return.
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }

        /// <summary>
        /// Helper function to safely create a directory.
        /// </summary>
        /// <param name="directory"></param>
        private static void MakeDirectory(string directory)
        {
            // Check for and make proper directories.
            if (!Directory.Exists(directory))
            {
                // No input directory. Make it for future use, then return.
                try
                {
                    Directory.CreateDirectory(directory);
                }
                catch (Exception e)
                {
                    ExceptionOutput.GetExceptionMessages(e);
                }
            }
        }

        /// <summary>
        /// This funciton cleans up processing folders.
        /// </summary>
        private static void CleanupFolders()
        {
            // Cleanup folders.
            string stageOneOutputPath = Path.Combine(Directory.GetCurrentDirectory(), ConfigurationManager.AppSettings["TempFolderName"]);
            try
            {
                Directory.Delete(stageOneOutputPath, true);
            }
            catch (Exception e)
            {
                ExceptionOutput.GetExceptionMessages(e);
            }
        }

        /// <summary>
        /// This function organizes parameters for and then launches a process
        /// of Waifu2x - Caffe on the given folder.
        /// </summary>
        /// <param name="inputFile"></param>
        /// <param name="outputPath"></param>
        /// <param name="magnificationSize"></param>
        /// <param name="batch"></param>
        /// <param name="split"></param>
        /// <returns>Exit code of Waifu2x - Caffe</returns>
        private static int DoUpTheWaifusDirect(string inputFile, string outputPath,
            int magnificationSize = 2, int batch = 6, int split = 128)
        {
            string modelDir = Path.Combine(ConfigurationManager.AppSettings["Waifu2xCaffeDirectory"],
                    ConfigurationManager.AppSettings["ModelDirectory"]);
            //Console.WriteLine(modelDir);

            string workingDirectory = Directory.GetCurrentDirectory();
            string waifuExec = Path.Combine(ConfigurationManager.AppSettings["Waifu2xCaffeDirectory"],
                Path.Combine(ConfigurationManager.AppSettings["Waifu2xExecutableName"]));

            if (!File.Exists(waifuExec))
            {
                Console.WriteLine("No Waifu2x - Caffe executable found! Enter a key to exit.");
                Console.ReadKey();
                Environment.Exit(-1);
            }

            if (!File.Exists(inputFile))
            {
                // Empty directory. Just return.
                Console.WriteLine("\nERROR - No such file".PadRight(46) + ": " + inputFile);
                return -1;
            }

            // Adaptive scaling. If an attemp fails, try lowering the batch; if that still doesn't work, divide the split size by 2.
            int exitCode = -1;
            do
            {
                // Initialize Waifu2x-Caffe information for 2x scale denoise+magnify 3.
                ProcessStartInfo magDenoiseInfo = new ProcessStartInfo
                {
                    FileName = waifuExec,
                    WindowStyle = ProcessWindowStyle.Hidden,

                    // Setup arguments.
                    Arguments = "--gpu 0" +
                        " -b " + batch +
                        " -c " + split +
                        " -d 8" +
                        " -p cudnn" +
                        " --model_dir \"" + modelDir + "\"" +
                        " -s " + magnificationSize +
                        " -n " + ConfigurationManager.AppSettings["DenoiseLevel"] +
                        " -m " + ConfigurationManager.AppSettings["ConversionMode"] +
                        " -e .png" +
                        " -l png" +
                        " -o \"" + outputPath + "\"" +
                        " -i \"" + inputFile + "\""
                };
                // Start Waifu2x-Caffe and wait for it to exit.
                Process magDenoise = Process.Start(magDenoiseInfo);
                magDenoise.WaitForExit();

                exitCode = magDenoise.ExitCode;

                // Check the exit code.
                if (exitCode < 0)
                {
                    if (batch > 1)
                    {
                        // Try reducing the batch size first.
                        Console.WriteLine("\rERROR - Could not convert. Changing batch size from " + batch + " to " + (batch - 1));
                        batch--;
                    }
                    else if (split > 1)
                    {
                        // If we still can't convert, try lowering the split size.
                        Console.WriteLine("\rERROR - Could not convert. Changing split size from " + split + " to " + (split / 2));
                        split /= 2;
                    }
                    else
                    {
                        Console.WriteLine("\rERROR - Could not convert".PadRight(46) + ": " + Path.GetFileName(inputFile));
                        Console.WriteLine("Last exit code".PadRight(46) + ": " + exitCode);
                        Console.ReadLine();
                        Environment.Exit(-1);
                    }
                }

            } while (exitCode < 0);

            Thread.Sleep(250);
            GC.Collect();

            return exitCode;
        }
    }
}
