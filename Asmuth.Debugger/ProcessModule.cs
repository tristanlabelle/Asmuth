using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	public sealed class ProcessModule
	{
		private readonly ulong baseAddress;
		private readonly SafeFileHandle handle;
		private readonly string imagePath;

		internal ProcessModule(ulong baseAddress, SafeFileHandle handle)
		{
			Contract.Assert(baseAddress > 0);
			this.baseAddress = baseAddress;
			this.handle = handle;

			if (!handle.IsInvalid)
				this.imagePath = Kernel32.GetFinalPathNameByHandle(handle.DangerousGetHandle(), 0);
		}

		public ulong BaseAddress => baseAddress;
		public SafeFileHandle Handle => handle;
		public string ImagePath => imagePath;

		public override string ToString() => Path.GetFileNameWithoutExtension(ImagePath);
	}
}
