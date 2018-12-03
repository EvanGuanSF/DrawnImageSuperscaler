# Waifu Enlarger And Batch Optimizer Operations (WeaBOO)
Image superscaler and optimizer using Waifu2x - Caffe and ImageMagick.net.

Uses the console executable from https://github.com/lltcggie/waifu2x-caffe to upscale images,
and then losslessly compresses the output image using ImageMagick.net.

__**Waifu2x - Caffee and its requirements must be acquired seperately**__

Setup Instructions:
 1. Download the latest release from https://github.com/lltcggie/waifu2x-caffe. Unzip it somewhere.
 2. Download the latest cuDNN library from https://developer.nvidia.com/cudnn (requires a free account). Unzip it somewhere.
 3. Open the cuDNN folder and copy cudnn64_7.dll to the waifu2x-caffe folder that contains the waifu2x-caffe.exe.

Running Instructions:
 1. Copy the absolute path of waifu2x-caffe folder to the program's config file.
 2. By default, the exe operates on the directory it is located in.
 This can be changed by setting an absolute path in the BaseFolderPath value,
 or by using a shortcut to the program that has an empty "Start In" field.
 3. The program seeks files in the config specified SourceFolderName subfolder,
 and only adds files with names that contain the config specified UnprocessedImageFlagString.
 4. Files are renamed and output to the working directory by default, or the config specified DestinationFolderName.
 

**Waifu2x - Caffee is optimized to work with more recent Nvidia GPUs, and this program is currently hardcoded to use cuDDN, and therefore only those GPUs.
The values in the code are optimized to work with a GTX 1070 with 8GB of vram and 32GB of system ram.
Your mileage may vary, and experimentation with batch and split sizes may be required. (Lower batch and split sizes use less vram.)**
