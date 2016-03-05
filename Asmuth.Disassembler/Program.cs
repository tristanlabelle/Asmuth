using Asmuth.X86;
using Asmuth.X86.Nasm;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.Disassembler
{
	using static WinNT;

	class Program
	{
		static void Main(string[] args)
		{
			var nasmInsnsEntries = new List<NasmInsnsEntry>();
			foreach (var line in File.ReadAllLines("insns.dat", Encoding.ASCII))
			{
				if (NasmInsns.IsIgnoredLine(line)) continue;
				nasmInsnsEntries.Add(NasmInsns.ParseLine(line));
			}

			var filePath = Path.GetFullPath(args[0]);
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
				if (fileHeader.Machine != IMAGE_FILE_MACHINE_I386)
					throw new InvalidDataException();

				// NT optional header
				var optionalHeaderMagic = fileViewAccessor.ReadUInt16(
					ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset + IMAGE_OPTIONAL_HEADER.MagicOffset);
				if (optionalHeaderMagic != IMAGE_NT_OPTIONAL_HDR32_MAGIC
					|| fileHeader.SizeOfOptionalHeader != Marshal.SizeOf(typeof(IMAGE_OPTIONAL_HEADER32)))
					throw new InvalidDataException();
				var optionalHeader = fileViewAccessor.Read<IMAGE_OPTIONAL_HEADER32>(
					ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset);
				
				Contract.Assert(optionalHeader.NumberOfRvaAndSizes == IMAGE_NUMBEROF_DIRECTORY_ENTRIES);

				// Sections
				var sectionHeaders = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
				var firstSectionHeaderPosition = ntHeaderPosition + IMAGE_NT_HEADERS.OptionalHeaderOffset + fileHeader.SizeOfOptionalHeader;
				for (int i = 0; i < fileHeader.NumberOfSections; ++i)
				{
					var sectionHeaderPosition = firstSectionHeaderPosition + Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER)) * i;
					fileViewAccessor.Read(sectionHeaderPosition, out sectionHeaders[i]);
				}

				// Disassemble executable sections
				var instructionDecoderLookup = new NasmInstructionDecoderLookup(nasmInsnsEntries);
				var instructionDecoder = new InstructionDecoder(instructionDecoderLookup, InstructionDecodingMode.IA32_Default32);
				foreach (var sectionHeader in sectionHeaders)
				{
					if ((sectionHeader.Characteristics & IMAGE_SCN_CNT_CODE) == 0) continue;

					var sectionBaseAddress = optionalHeader.ImageBase + sectionHeader.VirtualAddress;
					Console.Write($"{sectionBaseAddress:X8}");
					if (sectionHeader.Name.Char0 != 0)
						Console.Write($" <{sectionHeader.Name}>");
					Console.WriteLine(":");

					long codeOffset = 0;
					while (true)
					{
						Console.Write($"{sectionBaseAddress + codeOffset:X}".PadLeft(8));
						Console.Write(":       ");
						while (true)
						{
							var @byte = fileViewAccessor.ReadByte(sectionHeader.PointerToRawData + codeOffset);
							if (instructionDecoder.State != InstructionDecodingState.Initial)
								Console.Write(' ');
							Console.Write($"{@byte:X2}");
							codeOffset++;
							if (!instructionDecoder.Feed(@byte)) break;
						}

						while (Console.CursorLeft < 40) Console.Write(' ');

						var instruction = instructionDecoder.GetInstruction();
						instructionDecoder.Reset();

						var nasmInsnsEntry = nasmInsnsEntries.Where(e => e.IsMatch(instruction)).First();
						Console.Write('\t');
						Console.Write(nasmInsnsEntry.Mnemonic.ToLowerInvariant());

						bool firstOperand = true;
						foreach (var operandDefinition in nasmInsnsEntry.Operands)
						{
							Console.Write(firstOperand ? " " : ", ");
							if (operandDefinition.Field == OperandField.BaseReg)
							{
								Console.Write(instruction.GetRMEffectiveAddress()
									.ToString(OperandSize.Dword, instruction.Xex.Type == XexType.RexAndEscapes)
									.ToLowerInvariant());
							}
							else if (operandDefinition.Field == OperandField.ModReg)
							{
								var reg = instruction.ModRM?.GetReg() ?? (instruction.MainByte & 0x7);
								if (instruction.Xex.ModRegExtension) reg |= 0x8;
								Console.Write(new Gpr(reg, GprPart.Dword).Name.ToLowerInvariant());
							}
							else if (operandDefinition.Field == OperandField.Immediate)
							{
								if (instruction.Immediate <= (byte)sbyte.MaxValue)
									Console.Write(instruction.Immediate);
								else
									Console.Write("0x{0:X}", instruction.Immediate);
							}
							else
							{
								Console.Write('?');
							}

							firstOperand = false;
						}

						Console.WriteLine();

						if (instruction.MainByte == KnownOpcodes.RetNear || instruction.MainByte == KnownOpcodes.RetNearAndPop)
							break;
					}
				}
			}
		}
	}

	internal static class UnmanagedMemoryAccessorExtensions
	{
		public static T Read<T>(this UnmanagedMemoryAccessor accessor, long position) where T : struct
		{
			T value;
			accessor.Read(position, out value);
			return value;
		}
	}
}
