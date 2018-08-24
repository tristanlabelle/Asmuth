using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using Asmuth.X86;
using Asmuth.X86.Asm.Nasm;

namespace Asmuth.Debugger
{
	using static Kernel32;

	class Program
	{
		static void Main(string[] args)
		{
			Run().Wait();
		}

		private static async Task Run()
		{
			var nasmInsnsEntries = new List<NasmInsnsEntry>();
			foreach (var line in File.ReadAllLines("insns.dat", Encoding.ASCII))
			{
				if (NasmInsns.IsIgnoredLine(line)) continue;
				nasmInsnsEntries.Add(NasmInsns.ParseLine(line));
			}
			var instructionDecoder = new InstructionDecoder(
				CodeSegmentType.IA32_Default32, new NasmInstructionDecoderLookup(nasmInsnsEntries));

			var notepadProcess = Process.Start(@"C:\Windows\SysWow64\notepad.exe");
			var notepadDebugger = await ProcessDebugger.AttachAsync(notepadProcess.Id, initialBreak: false);

			await Task.Delay(TimeSpan.FromSeconds(2));
			var brokenThread = await notepadDebugger.BreakAsync();
			var context = brokenThread.GetContext(X86.CONTEXT_ALL);

			var ip = new ForeignPtr(context.Eip);
			var instruction = Decode(instructionDecoder, notepadDebugger, ip);
		}

		private static Instruction Decode(InstructionDecoder decoder, ProcessDebugger debugger, ForeignPtr ptr)
		{
			var reader = new BinaryReader(debugger.OpenMemory(ptr));
			while (decoder.Consume(reader.ReadByte())) { }
			return decoder.GetInstruction();
		}
	}
}
