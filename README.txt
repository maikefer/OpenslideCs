##Openslide Cs##
Implementation of Openslide API (with Deepzoom) in C# via Interop. Based on https://github.com/openslide/openslide-python/blob/master/openslide/deepzoom.py 

You need to have Openslide's dlls (with correct architecture - x64 or x86) in your PATH or where the DLL loader can find them.
Download the latest dlls at: http://openslide.org/docs/windows/

I adjusted the Code in a way that you can only read an OpenSlide (.svs) file and parse it into an OpenSlide object.
