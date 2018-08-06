using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	partial class ProcessDebugger
	{
		public sealed class Module
		{
			private readonly ForeignPtr @base;
			private readonly SafeFileHandle handle;
			private readonly string imagePath;

			internal Module(ForeignPtr @base, SafeFileHandle handle)
			{
				Debug.Assert(@base.Address> 0);
				this.@base = @base;
				this.handle = handle;

				if (!handle.IsInvalid)
					this.imagePath = Kernel32.GetFinalPathNameByHandle(handle.DangerousGetHandle(), 0);
			}

			public ForeignPtr Base => @base;
			public SafeFileHandle ImageHandle => handle;
			public string ImagePath => imagePath;

			public override string ToString() => Path.GetFileNameWithoutExtension(ImagePath);
		}
	}
}
