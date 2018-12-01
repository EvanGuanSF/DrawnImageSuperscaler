using System;
using System.IO;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Configuration;
using Microsoft.WindowsAPICodePack.Taskbar;

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
        private struct ImageOperationInfo
        {
            public ImageResolutionClassification opType;

            public string ImagePath { get; }

            public ImageOperationInfo(string path, ImageResolutionClassification passedOpType)
            {
                ImagePath = path;
                this.opType = passedOpType;
            }
        }

        private static int numScaledImages = 0;
        private static int numProcessableImages = 0;

        /// <summary>
        /// This is the primary working function of the WaifuScaler class.
        /// It sets up, directs, and monitors the core workflow of the program.
        /// </summary>
        /// <param name="directory"></param>
        public static void UpYourWaifu(string directory)
        {
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
            string currentDirectory = Directory.GetCurrentDirectory();
            string sourceFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["SourceFolderName"]);
            string temporaryFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["TempFolderName"]);
            string destinationFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["DestinationFolderName"]);

            CancellationTokenSource pingerCancelToken = new CancellationTokenSource();

            // Make the temp folder and output folder if necessary.
            MakeDirectory(temporaryFolderPath);
            MakeDirectory(destinationFolderPath);

            // Get the images in the S&R folder and put their paths in a list
            // in natural order (Windows sort by name ascending).
            List<ImageOperationInfo> imageOpList = SortImageListByResolution(GetImages.GetAllImages(sourceFolderPath));
            int maxLength = 0;
            numProcessableImages = imageOpList.Count;

            // Get the length of the longest filename in the list.
            foreach (ImageOperationInfo image in imageOpList)
            {
                try
                {
                    int test = Path.GetFileName(image.ImagePath).Length;
                    if (test > maxLength)
                        maxLength = test;
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
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                userRequestCancelRemainingOperations = true;
            });

            // Waifu2x - Caffee conversion loop.
            TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Normal);
            Console.Title = GetScalerCompletionPercentage() + "% " +
                "(" + numScaledImages + "/" + numProcessableImages + ") Images Scaled";
            int dotIncrementer = 0;
            TaskbarManager.Instance.SetProgressValue(numScaledImages, numProcessableImages);
            foreach (ImageOperationInfo image in imageOpList)
            {
                if (userRequestCancelRemainingOperations)
                {
                    break;
                }

                Task<string> anJob = Task.Run(() => Waifu2xJobController(image));
                string currentImageName = Path.GetFileName(image.ImagePath);

                while (!anJob.IsCompleted)
                {
                    if (!userRequestCancelRemainingOperations)
                    {
                        Console.Write(("\rUpscaling" + new string('.', (dotIncrementer % 10) + 1) +
                            new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) + ": " +
                            GetScalerCompletionPercentage() + "% " +
                            "(" + numScaledImages + "/" + numProcessableImages + ") " +
                            "Now processing: " + currentImageName +
                            new string(' ', maxLength + 10) + new string('\b', maxLength + 11));
                        dotIncrementer++;
                    }
                    else
                    {
                        Console.Write(("\rFinishing current jobs" + new string('.', (dotIncrementer % 10) + 1) +
                            new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) + ": " +
                            GetScalerCompletionPercentage() + "% " +
                            "(" + numScaledImages + "/" + numProcessableImages + ") " +
                            "Now processing: " + currentImageName +
                            new string(' ', maxLength + 10) + new string('\b', maxLength + 11));
                        dotIncrementer++;
                    }

                    Thread.Sleep(100);
                }
                anJob.Wait();

                numScaledImages++;
                TaskbarManager.Instance.SetProgressValue(numScaledImages, numProcessableImages);
                try
                {
                    File.Delete(Path.Combine(temporaryFolderPath, Path.GetFileName(anJob.Result)));
                }
                catch (Exception e)
                {
                    ExceptionOutput.GetExceptionMessages(e);
                }
                // Now enqueue an optimization task.
                Pinger.EnqueueImageForOptimization(anJob.Result);

                Console.Title = GetScalerCompletionPercentage() + " % " +
                            "(" + numScaledImages + "/" + numProcessableImages + ") Images Scaled";
            }
            if (numScaledImages == numProcessableImages)
            {
                Console.Write("\rFiles converted".PadRight(46) + ": " +
                    GetScalerCompletionPercentage() + "% " +
                    "(" + numScaledImages + "/" + numProcessableImages + ") Completed Normally" +
                    new string(' ', maxLength + 40) +
                    new string('\b', maxLength + 41));
                Console.WriteLine();
            }
            else
            {
                Console.Write("\rFiles converted".PadRight(46) + ": " +
                    GetScalerCompletionPercentage() + "% " +
                    "(" + numScaledImages + "/" + numProcessableImages + ")" +
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
                if (Pinger.GetPingerImageQueueCount() == 0 && Pinger.GetRunningThreadCount() == 0)
                {
                    // Once that is done, send a cancel request to the task to end it. 
                    pingerCancelToken.Cancel();
                    break;
                }

                Console.Write(("\rOptimizer pass in progress" +
                    new string('.', (dotIncrementer % 10) + 1) +
                    new string(' ', 9 - (dotIncrementer % 10))).PadRight(46) +
                    ": " + GetOptimizationCompletionPercentage() + "% " +
                    "(" + GetOptmizedImagesCount() + "/" + numProcessableImages + ") Images Optimized");
                dotIncrementer++;
                Console.Title = GetOptimizationCompletionPercentage() + "% " +
                    "(" + GetOptmizedImagesCount() + "/" + numProcessableImages + ") Images Optimized";
                TaskbarManager.Instance.SetProgressValue(GetOptmizedImagesCount(), numProcessableImages);
                Thread.Sleep(100);
            }

            // If the user cancels, send a cancel token and wait for the remaining operations to finish.
            if (userRequestCancelRemainingOperations && (!optimizationBackgroundTask.IsCanceled || !optimizationBackgroundTask.IsCompleted))
            {
                dotIncrementer = 0;
                pingerCancelToken.Cancel();
                while (!optimizationBackgroundTask.IsCompleted && (Pinger.GetPingerImageQueueCount() > 0))
                {
                    Console.Write("\rFinishing active optimizations" + new string('.', (dotIncrementer % 10) + 1) +
                        new string(' ', 55) + new string('\b', 56));
                    dotIncrementer++;
                    Thread.Sleep(100);
                }
                TaskbarManager.Instance.SetProgressState(TaskbarProgressBarState.Error);
                Console.Write("\rOptimizer pass cancelled." + new string(' ', 55) + new string('\b', 56));
                Console.WriteLine();
                Console.Title = GetOptimizationCompletionPercentage() + "% " +
                    "(" + GetOptmizedImagesCount() + "/" + numProcessableImages + ") (User Cancelled)";
            }
            else
            {
                Console.Write("\rOptimizer pass completed".PadRight(46) + ": " +
                    GetOptimizationCompletionPercentage() + "% " +
                    "(" + (numProcessableImages - GetOptmizedImagesCount()) + "/" + numProcessableImages + ") Images Optimized" +
                    new string(' ', 55) + new string('\b', 56));
                Console.WriteLine();
                Console.Title = GetOptimizationCompletionPercentage() + "% " +
                    "(" + GetOptmizedImagesCount() + "/" + numProcessableImages + ") (Completed Normally)";
            }
            optimizationsCompleted = true;
            CleanupFolders();
        }

        /// <summary>
        /// Takes a list of ImageOperationInfo, checks images for resolution sizes, and sorts them accordingly.
        /// </summary>
        /// <param name="imagePaths"></param>
        /// <returns>A list of ImageOperationInfo with ImageResolutionClassification calculated per image</returns>
        private static List<ImageOperationInfo> SortImageListByResolution(List<string> imagePaths)
        {
            // Get the images in the S&R folder and put their paths in a list
            // in natural order (Windows sort by name ascending).
            List<ImageOperationInfo> imageOpList = new List<ImageOperationInfo>();

            foreach (string image in imagePaths)
            {
                try
                {
                    using (Stream stream = File.OpenRead(image))
                    {
                        using (Image sourceImage = Image.FromStream(stream, false, false))
                        {
                            // Set the operation type for the image to be processed based on the
                            // dimensions of the image.
                            int resolution = sourceImage.Width * sourceImage.Height;
                            if (resolution >= 100000000)
                            {
                                // At least 10000x10000
                                imageOpList.Add(new ImageOperationInfo(image, ImageResolutionClassification.VeryLarge));
                            }
                            else if (resolution >= 22500000)
                            {
                                // At least 5000x4500
                                imageOpList.Add(new ImageOperationInfo(image, ImageResolutionClassification.Large));
                            }
                            else if (resolution >= 786432)
                            {
                                // At least 1024x768
                                imageOpList.Add(new ImageOperationInfo(image, ImageResolutionClassification.Normal));
                            }
                            else if (resolution >= 172800)
                            {
                                // At least 480x360
                                imageOpList.Add(new ImageOperationInfo(image, ImageResolutionClassification.Small));
                            }
                            else
                            {
                                // Smaller than 480x360
                                imageOpList.Add(new ImageOperationInfo(image, ImageResolutionClassification.VerySmall));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    ExceptionOutput.GetExceptionMessages(e);
                }
            }

            return imageOpList;
        }

        /// <summary>
        /// Takes a single image and converts it according to hard-coded rules.
        /// </summary>
        /// <param name="image"></param>
        /// <returns>Path of the scaled image.</returns>
        private static async Task<string> Waifu2xJobController(ImageOperationInfo image)
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            string sourceFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["SourceFolderName"]);
            string temporaryFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["TempFolderName"]);
            string destinationFolderPath = Path.Combine(currentDirectory, ConfigurationManager.AppSettings["DestinationFolderName"]);
            string tempImagePath = null;
            string fileName = Path.GetFileName(image.ImagePath);

            // Sequential scale and quality operations depending on flag.
            switch (image.opType)
            {
                case ImageResolutionClassification.VeryLarge:
                    {
                        // VeryLarge case. Quality pass before doing a 2x scale with reduced batch size.
                        tempImagePath = DoUpTheWaifusDirect(image.ImagePath, Path.Combine(temporaryFolderPath, fileName), 1, 2, 256);
                        return DoUpTheWaifusDirect(tempImagePath, Path.Combine(destinationFolderPath, Path.GetFileName(tempImagePath)), 2, 2, 256);
                    }
                case ImageResolutionClassification.Large:
                    {
                        // Large case. Quality pass before doing a 2x scale.
                        tempImagePath = DoUpTheWaifusDirect(image.ImagePath, Path.Combine(temporaryFolderPath, fileName), 1, 4, 256);
                        return DoUpTheWaifusDirect(tempImagePath, Path.Combine(destinationFolderPath, Path.GetFileName(tempImagePath)), 2, 4, 256);
                    }
                case ImageResolutionClassification.Normal:
                    {
                        // Normal case. 2x scale before doing a quality pass.
                        tempImagePath = DoUpTheWaifusDirect(image.ImagePath, Path.Combine(temporaryFolderPath, fileName), 2, 4, 256);
                        return DoUpTheWaifusDirect(tempImagePath, Path.Combine(destinationFolderPath, Path.GetFileName(tempImagePath)), 1, 4, 256);
                    }
                case ImageResolutionClassification.Small:
                    {
                        // Small case. Quality pass before doing a 2x scale with modified .
                        tempImagePath = DoUpTheWaifusDirect(image.ImagePath, Path.Combine(temporaryFolderPath, fileName), 2, 4, 128);
                        return DoUpTheWaifusDirect(tempImagePath, Path.Combine(destinationFolderPath, Path.GetFileName(tempImagePath)), 1, 4, 128);
                    }
                case ImageResolutionClassification.VerySmall:
                    {
                        // Image is WAY too small and may use silly amounts of vram to process. Just return.
                        return fileName;
                    }
                default:
                    {
                        break;
                    }
            }
            return null;
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
        /// <returns>Path of the scaled output image.</returns>
        private static string DoUpTheWaifusDirect(string inputFile, string outputPath,
            int magnificationSize = 2, int batch = 6, int split = 128)
        {
            string modelDir = Path.Combine(ConfigurationManager.AppSettings["Waifu2xCaffeDirectory"],
                    ConfigurationManager.AppSettings["ModelDirectory"]);
            outputPath = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + ".png");

            string workingDirectory = Directory.GetCurrentDirectory();
            string waifuExec = Path.Combine(ConfigurationManager.AppSettings["Waifu2xCaffeDirectory"],
                ConfigurationManager.AppSettings["Waifu2xExecutableName"]);

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
                return null;
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
                        Console.WriteLine("\rERROR - Could not convert. Changing batch size from " + batch + " to " + (batch - 1) +
                            " for : " + Path.GetFileName(inputFile) +
                            new string(' ', inputFile.Length) +
                            new string('\b', inputFile.Length + 1));
                        batch--;
                    }
                    else if (split > 1)
                    {
                        // If we still can't convert, try lowering the split size.
                        Console.WriteLine("\rERROR - Could not convert. Changing split size from " + split + " to " + (split / 2) +
                            " for : " + Path.GetFileName(inputFile) +
                            new string(' ', inputFile.Length) +
                            new string('\b', inputFile.Length + 1));
                        split /= 2;
                    }
                    else
                    {
                        // If we hit 1/1 for both batch size and split size, then we can't scale the image.
                        Console.WriteLine("\rERROR - Could not convertZ".PadRight(46) + ": " + Path.GetFileName(inputFile));
                        Console.WriteLine("Last exit code".PadRight(46) + ": " + exitCode);
                        Console.ReadLine();
                        Environment.Exit(-1);
                    }
                }

            } while (exitCode < 0);

            Thread.Sleep(250);
            GC.Collect();

            return outputPath;
        }


        #region Helper Functions
        /// <summary>
        /// Returns percentage to one decimal place of total images scaled as a string.
        /// </summary>
        /// <returns>Percentage to one decimal place of total images scaled as a string.</returns>
        private static string GetScalerCompletionPercentage()
        {
            if (numProcessableImages == 0)
                return "0.0";
            return ((double)numScaledImages / numProcessableImages * 100).ToString("0.0"); ;
        }

        /// <summary>
        /// Returns the number
        /// </summary>
        /// <returns></returns>
        private static int GetOptmizedImagesCount()
        {
            return numProcessableImages - Pinger.GetPingerImageQueueCount() - Pinger.GetRunningThreadCount();
        }

        /// <summary>
        /// Returns percentage to one decimal place of total images optimized as a string.
        /// </summary>
        /// <returns>Percentage to one decimal place of total images optimized as a string.</returns>
        private static string GetOptimizationCompletionPercentage()
        {
            if (numProcessableImages == 0)
                return "0.0";
            return ((double)GetOptmizedImagesCount() / numProcessableImages * 100).ToString("0.0"); ;
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
            string tempImageFolder = Path.Combine(Directory.GetCurrentDirectory(), ConfigurationManager.AppSettings["TempFolderName"]);
            try
            {
                Directory.Delete(tempImageFolder, true);
            }
            catch (Exception e)
            {
                ExceptionOutput.GetExceptionMessages(e);
            }
        }

        #endregion
    }
}
