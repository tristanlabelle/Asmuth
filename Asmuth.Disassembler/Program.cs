using Asmuth.X86;
using Asmuth.X86.Encoding;
using Asmuth.X86.Encoding.Nasm;
using Asmuth.X86.Encoding.Xed;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.Disassembler
{
	using static WinNT;

	class Program
	{
		static void Main(string[] args)
		{
			// StaticXedInstructionConverter test
			var xedDatabase = XedDatabase.LoadDirectory(args[0]);
			var xedInstructions = xedDatabase.EncodeDecodePatterns
				.OfType<XedInstructionTable>()
				.SelectMany(it => it.Instructions);
			Dictionary<string, XedInstruction> encodingsToInstructions = new Dictionary<string, XedInstruction>();
			foreach (var xedInstruction in xedInstructions)
			{
				if (Regex.IsMatch(xedInstruction.Class, @"^NOP\d$")) continue;
				foreach (var xedForm in xedInstruction.Forms)
				{
					var instructionDefinition = StaticXedInstructionConverter.GetInstructionDefinition(xedInstruction.Class, xedForm);

					Console.ForegroundColor = ConsoleColor.Yellow;
					Console.Write(instructionDefinition.Mnemonic);
					for (int i = 0; i < instructionDefinition.Operands.Count; ++i)
					{
						Console.Write(i == 0 ? " " : ", ");
						Console.Write(instructionDefinition.Operands[i]);
					}
					Console.ResetColor();

					Console.Write(": ");
					Console.WriteLine(instructionDefinition.Encoding);

					var opcodeEncodingStr = instructionDefinition.Encoding.ToString();
					if (encodingsToInstructions.TryGetValue(opcodeEncodingStr, out var existing))
					{
						Console.ForegroundColor = ConsoleColor.Red;
						Console.WriteLine($"Duplicate with {existing.Class}");
						Console.ResetColor();
					}
					else encodingsToInstructions.Add(opcodeEncodingStr, xedInstruction);
				}
			}

			// Engine test
			var xedEngine = new XedEngine(xedDatabase);
			xedEngine.TraceMessage += (i, m) => Console.WriteLine(new string(' ', i * 2) + m);

			var xedNopInstruction = ((XedInstructionTable)xedDatabase.EncodeDecodePatterns.Get("INSTRUCTIONS"))
				.Instructions.First(i => i.Class == "NOP");
			xedEngine.Encode(xedNopInstruction, formIndex: 0, CodeSegmentType.X64);

			// Load instruction definitions from NASM's insns.dat file
			var instructionTable = new OpcodeEncodingTable<InstructionDefinition>();

			var filePath = Path.GetFullPath(args[1]);
			using (var memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Open, null, 0, MemoryMappedFileAccess.Read))
			using (var fileViewAccessor = memoryMappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
			{
				// DOS header
				var dosHeader = fileViewAccessor.Read<IMAGE_DOS_HEADER>(0);
				if (dosHeader.e_magic != IMAGE_DOS_SIGNATURE) throw new InvalidDataException();

				// NT headers
				var ntHeaderPosition = (long)dosHeader.e_lfanew;
				if (fileViewAccessor.ReadUInt32(ntHeaderPosition) != IMAGE_NT_SIGNATURE)
					throw new InvalidDataException();
				var fileHeader = fileViewAccessor.Read<IMAGE_FILE_HEADER>(ntHeaderPosition + IMAGE_NT_HEADERS.FileHeaderOffset);
				if (fileHeader.Machine != IMAGE_FILE_MACHINE_I386 && fileHeader.Machine != IMAGE_FILE_MACHINE_AMD64)
					throw new InvalidDataException();

				bool is32Bit = fileHeader.Machine == IMAGE_FILE_MACHINE_I386;

				// NT optional header
				var optionalHeaderMagic = fileViewAccessor.ReadUInt16(
					ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset + IMAGE_OPTIONAL_HEADER.MagicOffset);
				
				IMAGE_OPTIONAL_HEADER64 optionalHeader;
				if (is32Bit)
				{
					if (optionalHeaderMagic != IMAGE_NT_OPTIONAL_HDR32_MAGIC
						|| fileHeader.SizeOfOptionalHeader != Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER32)))
						throw new InvalidDataException();
					optionalHeader = fileViewAccessor.Read<IMAGE_OPTIONAL_HEADER32>(
						ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset)
						.As64();
				}
				else
				{
					if (optionalHeaderMagic != IMAGE_NT_OPTIONAL_HDR64_MAGIC
						|| fileHeader.SizeOfOptionalHeader != Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER64)))
						throw new InvalidDataException();
					optionalHeader = fileViewAccessor.Read<IMAGE_OPTIONAL_HEADER64>(
						ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset);
				}

				Debug.Assert(optionalHeader.NumberOfRvaAndSizes == IMAGE_NUMBEROF_DIRECTORY_ENTRIES);

				// Sections
				var sectionHeaders = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
				var firstSectionHeaderPosition = ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset + fileHeader.SizeOfOptionalHeader;
				for (int i = 0; i < fileHeader.NumberOfSections; ++i)
				{
					var sectionHeaderPosition = firstSectionHeaderPosition + Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER)) * i;
					fileViewAccessor.Read(sectionHeaderPosition, out sectionHeaders[i]);
				}

				// Disassemble executable sections
				var instructionDecoder = new InstructionDecoder(
					is32Bit ? CodeSegmentType.IA32_Default32 : CodeSegmentType.X64,
					instructionTable);
				foreach (var sectionHeader in sectionHeaders)
				{
					if ((sectionHeader.Characteristics & IMAGE_SCN_CNT_CODE) == 0) continue;

					var sectionBaseAddress = optionalHeader.ImageBase + sectionHeader.VirtualAddress;
					if (sectionHeader.Name.Char0 != 0)
						Console.Write($" <{sectionHeader.Name}>");
					Console.WriteLine(":");

					long codeOffset = 0;
					while (true)
					{
						if (codeOffset == sectionHeader.SizeOfRawData) break;

						ulong codeAddress = sectionBaseAddress + (ulong)codeOffset;
						Console.Write("0x");
						Console.Write(is32Bit ? $"{codeAddress:X8}" : $"{codeAddress:X16}");
						Console.Write(":");

						// Read the instruction bytes
						while (true)
						{
							if (codeOffset >= sectionHeader.SizeOfRawData) throw new InvalidDataException();
							var @byte = fileViewAccessor.ReadByte(sectionHeader.PointerToRawData + codeOffset);
							codeOffset++;
							Console.Write(" {0:x2}", @byte);
							if (!instructionDecoder.Consume(@byte)) break;
						}

						var instruction = instructionDecoder.GetInstruction();
						var instructionDefinition = (InstructionDefinition)instructionDecoder.LookupTag;
						instructionDecoder.Reset();

						Console.Write(' ');
						while (Console.CursorLeft < 32)
							Console.Write(' ');

						Console.Write(instructionDefinition.Mnemonic);

						Console.Write(' ');
						while (Console.CursorLeft < 44)
							Console.Write(' ');

						instructionDefinition.FormatOperandList(Console.Out, in instruction, ip: codeAddress);

						Console.WriteLine();
					}
				}
			}
		}
	}

	internal static class UnmanagedMemoryAccessorExtensions
	{
		public static T Read<T>(this UnmanagedMemoryAccessor accessor, long position) where T : struct
		{
			accessor.Read(position, out T value);
			return value;
		}
	}
}
