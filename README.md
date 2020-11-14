# Drawn Image Supersampler
Image superscaler and optimizer using Waifu2x - Caffe and Magick.NET respectively.

Uses the console executable from https://github.com/lltcggie/waifu2x-caffe to upscale images,
and then losslessly compresses the output images using Magick.NET.

## To Build:
 1. Acquire the latest Magick.NET-Q16-HDRI-AnyCPU package via NuGet package manager in Visual Studio or via https://www.nuget.org/packages/Magick.NET-Q16-HDRI-AnyCPU.
 2. Acquire the latest Microsoft-WindowsAPICodePack-Core and Microsoft-WindowsAPICodePack-Shell via NuGet package manager in Visual Studio or in the links on https://github.com/contre/Windows-API-Code-Pack-1.1.
 3. Be sure to set the target to x64 only. Magick.NET may not function correctly on 32 bit systems. Images processed can easily have upwards of 30 million pixels, so there is a need for longer address spaces.

## To Run:
__**Waifu2x - Caffe and its requirements must be acquired seperately**__

Setup Instructions:
 1. Download the latest release from https://github.com/lltcggie/waifu2x-caffe. Unzip it somewhere. I recommend putting it somewhere close the the root of a drive, since you will need to copy the folder path later.
 2. Download the latest cuDNN library from https://developer.nvidia.com/cudnn (requires a free account). Unzip it somewhere.
 3. Open the cuDNN folder and copy cudnn64_7.dll to the waifu2x-caffe folder that contains the waifu2x-caffe.exe.

Running Instructions:
 1. Copy the absolute path of the waifu2x-caffe folder to the program's config file.
 2. By default, the exe operates on the directory it is located in.
 This can be changed by setting an absolute path in the BaseFolderPath value,
 or by using a shortcut to the program that has an empty "Start In" field.
 3. The program seeks files in the config specified SourceFolderName subfolder,
 and only adds files with names that contain the config specified UnprocessedImageFlagString.
 4. Files are renamed and output to the working directory by default, or the config specified DestinationFolderName.

*Note*: The program uses Path.Combine to combine the working directory with the output directory. A feature of Path.Combine is that it ignores the first parameter (working directory's path by default) if an absolute path is given for the second parameter (App.config specified path). This means that if you want to specify an output directory outside of the working directory, you can do so by providing a full path in the relevant App.config field.

**Waifu2x - Caffe is optimized to work with more recent Nvidia GPUs, and this program is currently hardcoded to use cuDDN (though you can change this yourself), and therefore only those GPUs by default.
The values in the code are optimized to work with an Nvidia GTX 1070 with 8GB of vram and 32GB of system ram.
Your mileage may vary, and experimentation with batch and split sizes may be required. (Lower batch and split sizes use less vram.)**

## TO DO (in no particular order):
 - Add a service version to run in the background and automatically operate on a folder on a set timer and/or at a set time of day.
 - Figure out the best way to estimate and display reamaining time.
 - Develop a self-learning process to allow the program to train itself to find and store optimized batch and split values for a given system.
 - Make a GUI using WPF and MVVM design pattern.
 - Figure out how licenses work.
