using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	partial class ProcessDebugger
	{
		public sealed class MemoryStream : Stream
		{
			private readonly ProcessDebugger debugger;
			
			internal MemoryStream(ProcessDebugger debugger)
			{
				if (debugger == null) throw new ArgumentNullException(nameof(debugger));
				this.debugger = debugger;
			}

			public override bool CanRead => true;
			public override bool CanSeek => true;
			public override bool CanWrite => true;
			public override long Length => long.MaxValue;
			public ForeignPtr Ptr { get; set; }

			public override long Position
			{
				get { return (long)Ptr.Address; }
				set { Ptr = new ForeignPtr((ulong)value); }
			}

			public override void Flush() { }

			public override int Read(byte[] buffer, int offset, int count)
			{
				var pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
				try
				{
					debugger.service.ReadMemory(Ptr, pin.AddrOfPinnedObject() + offset, count);
					Ptr += count;
				}
				finally
				{
					pin.Free();
				}
				return count;
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotImplementedException();
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException();
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new NotImplementedException();
			}
		}
	}
}
