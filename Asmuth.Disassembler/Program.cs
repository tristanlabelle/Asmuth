﻿using Asmuth.X86;
using Asmuth.X86.Nasm;
using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;

namespace Asmuth.Disassembler
{
	using static WinNT;

	class Program
	{
		static void Main(string[] args)
		{
			var instructionDictionary = new InstructionDictionary();
			foreach (var line in File.ReadAllLines("insns.dat", Encoding.ASCII))
			{
				if (NasmInsns.IsIgnoredLine(line)) continue;
				var entry = NasmInsns.ParseLine(line);
				if (entry.IsPseudo) continue;
				instructionDictionary.Add(entry.ToInstructionDefinition());
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
				var sections = new IMAGE_SECTION_HEADER[fileHeader.NumberOfSections];
				for (int i = 0; i < fileHeader.NumberOfSections; ++i)
				{
					var sectionPosition = ntHeaderPosition + fileHeader.SizeOfOptionalHeader + Marshal.SizeOf(typeof(IMAGE_SECTION_HEADER)) * i;
					fileViewAccessor.Read(sectionPosition, out sections[i]);
				}

				// Entry point
				long codePosition = optionalHeader.AddressOfEntryPoint;
				var instructionDecoder = new InstructionDecoder(instructionDictionary, InstructionDecodingMode.IA32_Default32);
				while (true)
				{
					Console.Write($"{codePosition:X8}\t");
					while (true)
					{
						var @byte = fileViewAccessor.ReadByte(codePosition);
						if (instructionDecoder.State != InstructionDecodingState.Initial)
							Console.Write(' ');
						Console.Write($"{@byte:X2}");
						codePosition++;
						if (!instructionDecoder.Feed(@byte)) break;
					}

					var instruction = instructionDecoder.GetInstruction();
					instructionDecoder.Reset();

					var instructionDefinition = instructionDictionary.Find(instruction.OpcodeLookupKey);
					Console.Write('\t');
					Console.WriteLine(instructionDefinition.Mnemonic);

					if (instruction.MainByte == KnownOpcodes.RetNear || instruction.MainByte == KnownOpcodes.RetNearAndPop)
						break;
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