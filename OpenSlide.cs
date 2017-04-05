using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using static System.Double;

namespace OpenSlideCs
{
	public unsafe class Openslide : IDisposable
	{
		/// <summary>
		/// microns per pixel
		/// </summary>
		private const string OpenslidePropertyNameMppX = "openslide.mpp-x";

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int* openslide_open([MarshalAs(UnmanagedType.LPStr)] string filename);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr openslide_detect_vendor([MarshalAs(UnmanagedType.LPStr)] string filename);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int openslide_get_level_count(int* osr);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void openslide_get_level_dimensions(int* osr, int level, out long w, out long h);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern double openslide_get_level_downsample(int* osr, int level);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern int openslide_get_best_level_for_downsample(int* osr, double downsample);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void openslide_read_region(int* osr, void* dest, long x, long y, int level, long w, long h);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void openslide_close(int* osr);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr openslide_get_error(int* osr);

		[DllImport("libopenslide-0.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern IntPtr openslide_get_property_value(int* osr, [MarshalAs(UnmanagedType.LPStr)] string name);

		public static Action<string> OnTrace = Console.WriteLine;

		private readonly int* _handle;

		/// <summary>
		/// dimensions (Width and Height) per os_level (deep zoom level)
		/// </summary>
		public List<Size> Dimensions = new List<Size>();

		public string GetLastError()
		{
			var error = CheckForLastError();
			if (error != null)
				throw new ArgumentException("openslide error: " + error);
			throw new ArgumentException("openslide error, but error is empty?");
		}

		public string CheckForLastError()
		{
			var lastError = openslide_get_error(_handle);
			if (lastError.ToInt32() == 0)
			{
				return null;
			}
			return Marshal.PtrToStringAnsi(lastError);
		}

		public Openslide(string filename)
		{
			if (!File.Exists(filename))
				throw new ArgumentException($"File '{filename}' can't be opened");
			_handle = openslide_open(filename);
			if (_handle == null || _handle[0] == 0)
			{
				var vendor = openslide_detect_vendor(filename);
				if (vendor.ToInt32() != 0)
					throw new ArgumentException("Vendor " + Marshal.PtrToStringAnsi(vendor) + " unsupported?");
				throw new ArgumentException("File unrecognized");
			}
			var maxOsLevel = openslide_get_level_count(_handle);
			if (maxOsLevel == -1)
			{
				GetLastError();
			}

			for (int level = 0; level < maxOsLevel; level++)
			{
				long w, h;
				openslide_get_level_dimensions(_handle, level, out w, out h);
				if (w == -1 || h == -1)
				{
					GetLastError();
				}
				Dimensions.Add(new Size(w, h));
				var downsample = openslide_get_level_downsample(_handle, level);

				if (downsample == -1.0)
				{
					GetLastError();
				}
			}
		}

		public long Height => Dimensions[0].Height;

		public long Width => Dimensions[0].Width;

		/// <summary>
		/// Returns the Bitmap that shows Exceprt with the defined parameters. Whereas Location defines the x and y coordinates of the 
		/// top left point of the excerpt and size defines the width and height of the btimap.
		/// </summary>
		/// <param name="location"> Location on level 0 (not scaled!) </param>
		/// <param name="level">The deepzoom Level where 0 is the biggest (detailed), max_os_level the tiniest </param>
		/// <param name="size"> The Size of the region (scaled according to the level) </param>
		/// <returns> A Bitmap showing the described Region of the SVS file </returns>
		public Bitmap ReadRegion(Size location, int level, Size size)
		{
			Bitmap bmp = new Bitmap((int) size.Width, (int) size.Height);

			bmp.SetPixel(0, 0, Color.AliceBlue);

			BitmapData bmpdata = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite,
				PixelFormat.Format32bppArgb);

			void* p = bmpdata.Scan0.ToPointer();
			openslide_read_region(_handle, p, location.Width, location.Height, level, (int) size.Width, (int) size.Height);

			bmp.UnlockBits(bmpdata);


			if (bmp.GetPixel(0, 0) == Color.Black)
			{
				var error = CheckForLastError();
				if (error != null)
					throw new ArgumentException($"error reading region loc:{location}, level:{level}, size:{size}" + error);
			}
			return bmp;
		}

		/// <summary>
		/// Calculates the Microns (Mikrometer) per Pixel 
		/// </summary>
		/// <returns> The calculated Microns (Mikrometer) per Pixel </returns>
		public double GetMpp()
		{
			IntPtr prop = openslide_get_property_value(_handle, OpenslidePropertyNameMppX);

			if (prop == IntPtr.Zero)
				GetLastError();

			var propstring = Marshal.PtrToStringAnsi(prop);
			double ret;
			TryParse(propstring?.Replace(",", "."), out ret);

			if (ret < 1e-10 || ret > 1000)
			{
				TryParse(propstring?.Replace(".", ","), out ret);
			}

			return ret;
		}

		#region IDisposable Support

		private bool _disposedValue; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposedValue)
			{
				if (disposing)
				{
				}
				if (_handle != null && _handle[0] != 0)
				{
					openslide_close(_handle);
				}

				_disposedValue = true;
			}
		}

		~Openslide()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion
	}

	/// <summary>
	/// Data Wrapper Class for a value pair of Width / Height (x/y) 
	/// </summary>
	public struct Size
	{
		public long Width;
		public long Height;

		public Size(long w, long h)
		{
			Width = w;
			Height = h;
		}

		public override string ToString()
		{
			return "w:" + Width + " h:" + Height;
		}
	}
}