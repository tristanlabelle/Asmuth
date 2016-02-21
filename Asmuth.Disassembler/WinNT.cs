using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.Disassembler
{
	using BYTE = System.Byte;
	using DWORD = System.UInt32;
	using ULONGLONG = System.UInt64;
	using WORD = System.UInt16;

	internal static class WinNT
	{
		public const DWORD IMAGE_SIZEOF_SHORT_NAME = 8;

		public const DWORD IMAGE_DIRECTORY_ENTRY_EXPORT = 0;
		public const DWORD IMAGE_DIRECTORY_ENTRY_IMPORT = 1;
		public const DWORD IMAGE_DIRECTORY_ENTRY_RESOURCE = 2;
		public const DWORD IMAGE_DIRECTORY_ENTRY_EXCEPTION = 3;
		public const DWORD IMAGE_DIRECTORY_ENTRY_SECURITY = 4;
		public const DWORD IMAGE_DIRECTORY_ENTRY_BASERELOC = 5;
		public const DWORD IMAGE_DIRECTORY_ENTRY_DEBUG = 6;
		public const DWORD IMAGE_DIRECTORY_ENTRY_COPYRIGHT = 7;
		public const DWORD IMAGE_DIRECTORY_ENTRY_GLOBALPTR = 8;
		public const DWORD IMAGE_DIRECTORY_ENTRY_TLS = 9;
		public const DWORD IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG = 10;
		public const DWORD IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT = 11;
		public const DWORD IMAGE_DIRECTORY_ENTRY_IAT = 12;
		public const DWORD IMAGE_DIRECTORY_ENTRY_DELAY_IMPORT = 13;
		public const DWORD IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

		public const WORD IMAGE_DOS_SIGNATURE = 0x5A4D;
		public const DWORD IMAGE_NT_SIGNATURE = 0x00004550;
		public const WORD IMAGE_NT_OPTIONAL_HDR32_MAGIC = 0x10B;
		public const WORD IMAGE_NT_OPTIONAL_HDR64_MAGIC = 0x20B;
		public const WORD IMAGE_ROM_OPTIONAL_HDR_MAGIC = 0x107;

		public const WORD IMAGE_FILE_RELOCS_STRIPPED = 0x0001; // Relocation info stripped from file.
		public const WORD IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002; // File is executable  (i.e. no unresolved externel references).
		public const WORD IMAGE_FILE_LINE_NUMS_STRIPPED = 0x0004; // Line nunbers stripped from file.
		public const WORD IMAGE_FILE_LOCAL_SYMS_STRIPPED = 0x0008; // Local symbols stripped from file.
		public const WORD IMAGE_FILE_AGGRESIVE_WS_TRIM = 0x0010; // Agressively trim working set
		public const WORD IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020; // App can handle >2gb addresses
		public const WORD IMAGE_FILE_BYTES_REVERSED_LO = 0x0080; // Bytes of machine word are reversed.
		public const WORD IMAGE_FILE_32BIT_MACHINE = 0x0100; // 32 bit word machine.
		public const WORD IMAGE_FILE_DEBUG_STRIPPED = 0x0200; // Debugging info stripped from file in .DBG file
		public const WORD IMAGE_FILE_REMOVABLE_RUN_FROM_SWAP = 0x0400; // If Image is on removable media, copy and run from the swap file.
		public const WORD IMAGE_FILE_NET_RUN_FROM_SWAP = 0x0800; // If Image is on Net, copy and run from the swap file.
		public const WORD IMAGE_FILE_SYSTEM = 0x1000; // System File.
		public const WORD IMAGE_FILE_DLL = 0x2000; // File is a DLL.
		public const WORD IMAGE_FILE_UP_SYSTEM_ONLY = 0x4000; // File should only be run on a UP machine
		public const WORD IMAGE_FILE_BYTES_REVERSED_HI = 0x8000; // Bytes of machine word are reversed.

		public const WORD IMAGE_FILE_MACHINE_UNKNOWN = 0x0000;
		public const WORD IMAGE_FILE_MACHINE_ALPHA = 0x0184;
		public const WORD IMAGE_FILE_MACHINE_ALPHA64 = 0x0284;
		public const WORD IMAGE_FILE_MACHINE_AM33 = 0x01d3;
		public const WORD IMAGE_FILE_MACHINE_AMD64 = 0x8664;
		public const WORD IMAGE_FILE_MACHINE_ARM = 0x01c0;
		public const WORD IMAGE_FILE_MACHINE_AXP64 = IMAGE_FILE_MACHINE_ALPHA64;
		public const WORD IMAGE_FILE_MACHINE_CEE = 0xc0ee;
		public const WORD IMAGE_FILE_MACHINE_CEF = 0x0cef;
		public const WORD IMAGE_FILE_MACHINE_EBC = 0x0ebc;
		public const WORD IMAGE_FILE_MACHINE_I386 = 0x014c;
		public const WORD IMAGE_FILE_MACHINE_IA64 = 0x0200;
		public const WORD IMAGE_FILE_MACHINE_M32R = 0x9041;
		public const WORD IMAGE_FILE_MACHINE_M68K = 0x0268;
		public const WORD IMAGE_FILE_MACHINE_MIPS16 = 0x0266;
		public const WORD IMAGE_FILE_MACHINE_MIPSFPU = 0x0366;
		public const WORD IMAGE_FILE_MACHINE_MIPSFPU16 = 0x0466;
		public const WORD IMAGE_FILE_MACHINE_POWERPC = 0x01f0;
		public const WORD IMAGE_FILE_MACHINE_POWERPCFP = 0x01f1;
		public const WORD IMAGE_FILE_MACHINE_R10000 = 0x0168;
		public const WORD IMAGE_FILE_MACHINE_R3000 = 0x0162;
		public const WORD IMAGE_FILE_MACHINE_R4000 = 0x0166;
		public const WORD IMAGE_FILE_MACHINE_SH3 = 0x01a2;
		public const WORD IMAGE_FILE_MACHINE_SH3DSP = 0x01a3;
		public const WORD IMAGE_FILE_MACHINE_SH3E = 0x01a4;
		public const WORD IMAGE_FILE_MACHINE_SH4 = 0x01a6;
		public const WORD IMAGE_FILE_MACHINE_SH5 = 0x01a8;
		public const WORD IMAGE_FILE_MACHINE_THUMB = 0x01c2;
		public const WORD IMAGE_FILE_MACHINE_TRICORE = 0x0520;
		public const WORD IMAGE_FILE_MACHINE_WCEMIPSV2 = 0x0169;

		public const DWORD IMAGE_SCN_TYPE_NO_PAD = 0x00000008; // Reserved.
		public const DWORD IMAGE_SCN_CNT_CODE = 0x00000020; // Section contains code.
		public const DWORD IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040; // Section contains initialized data.
		public const DWORD IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080; // Section contains uninitialized data.
		public const DWORD IMAGE_SCN_LNK_OTHER = 0x00000100; // Reserved.
		public const DWORD IMAGE_SCN_LNK_INFO = 0x00000200; // Section contains comments or some other type of information.
		public const DWORD IMAGE_SCN_LNK_REMOVE = 0x00000800; // Section contents will not become part of image.
		public const DWORD IMAGE_SCN_LNK_COMDAT = 0x00001000; // Section contents comdat.
		public const DWORD IMAGE_SCN_MEM_FARDATA = 0x00008000;
		public const DWORD IMAGE_SCN_MEM_PURGEABLE = 0x00020000;
		public const DWORD IMAGE_SCN_MEM_16BIT = 0x00020000;
		public const DWORD IMAGE_SCN_MEM_LOCKED = 0x00040000;
		public const DWORD IMAGE_SCN_MEM_PRELOAD = 0x00080000;
		public const DWORD IMAGE_SCN_ALIGN_1BYTES = 0x00100000;
		public const DWORD IMAGE_SCN_ALIGN_2BYTES = 0x00200000;
		public const DWORD IMAGE_SCN_ALIGN_4BYTES = 0x00300000;
		public const DWORD IMAGE_SCN_ALIGN_8BYTES = 0x00400000;
		public const DWORD IMAGE_SCN_ALIGN_16BYTES = 0x00500000; // Default alignment if no others are specified.
		public const DWORD IMAGE_SCN_ALIGN_32BYTES = 0x00600000;
		public const DWORD IMAGE_SCN_ALIGN_64BYTES = 0x00700000;
		public const DWORD IMAGE_SCN_LNK_NRELOC_OVFL = 0x01000000; // Section contains extended relocations.
		public const DWORD IMAGE_SCN_MEM_NOT_CACHED = 0x04000000; // Section is not cachable.
		public const DWORD IMAGE_SCN_MEM_NOT_PAGED = 0x08000000; // Section is not pageable.
		public const DWORD IMAGE_SCN_MEM_SHARED = 0x10000000; // Section is shareable. 

		public const DWORD IMAGE_SUBSYSTEM_UNKNOWN = 0; // Unknown subsystem.
		public const DWORD IMAGE_SUBSYSTEM_NATIVE = 1; // Image doesn't require a subsystem.
		public const DWORD IMAGE_SUBSYSTEM_WINDOWS_GUI = 2; // Image runs in the Windows GUI subsystem.
		public const DWORD IMAGE_SUBSYSTEM_WINDOWS_CUI = 3; // Image runs in the Windows character subsystem.
		public const DWORD IMAGE_SUBSYSTEM_OS2_CUI = 5; // image runs in the OS/2 character subsystem.
		public const DWORD IMAGE_SUBSYSTEM_POSIX_CUI = 7; // image run  in the Posix character subsystem.

		public const DWORD IMAGE_NUMBEROF_DIRECTORY_ENTRIES = 16;

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_DATA_DIRECTORY
		{
			public DWORD VirtualAddress;
			public DWORD Size;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_DOS_HEADER
		{
			public WORD e_magic; // Magic number
			public WORD e_cblp; // Bytes on last page of file
			public WORD e_cp; // Pages in file
			public WORD e_crlc; // Relocations
			public WORD e_cparhdr; // Size of header in paragraphs
			public WORD e_minalloc; // Minimum extra paragraphs needed
			public WORD e_maxalloc; // Maximum extra paragraphs needed
			public WORD e_ss; // Initial (relative) SS value
			public WORD e_sp; // Initial SP value
			public WORD e_csum; // Checksum
			public WORD e_ip; // Initial IP value
			public WORD e_cs; // Initial (relative) CS value
			public WORD e_lfarlc; // File address of relocation table
			public WORD e_ovno; // Overlay number
			public ulong e_res; // Reserved words
			public WORD e_oemid; // OEM identifier (for e_oeminfo)
			public WORD e_oeminfo; // OEM information; e_oemid specific
			public ulong e_res2_0; // Reserved words
			public ulong e_res2_1; // Reserved words
			public DWORD e_res2_2; // Reserved words
			public DWORD e_lfanew; // File address of new exe header
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_FILE_HEADER
		{
			public WORD Machine;
			public WORD NumberOfSections;
			public DWORD TimeDateStamp;
			public DWORD PointerToSymbolTable;
			public DWORD NumberOfSymbols;
			public WORD SizeOfOptionalHeader;
			public WORD Characteristics;
		}

		public static class IMAGE_NT_HEADERS
		{
			public static readonly int FileHeaderOffset = sizeof(DWORD);
			public static readonly int OptionalHeaderOffset = FileHeaderOffset + Marshal.SizeOf(typeof(IMAGE_FILE_HEADER));
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_NT_HEADERS32
		{
			public DWORD Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER32 OptionalHeader;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_NT_HEADERS64
		{
			public DWORD Signature;
			public IMAGE_FILE_HEADER FileHeader;
			public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
		}

		[StructLayout(LayoutKind.Sequential, Size = 8 * (int)IMAGE_NUMBEROF_DIRECTORY_ENTRIES)]
		public struct IMAGE_OPTIONAL_HEADER_DATA_DIRECTORY
		{
			IMAGE_DATA_DIRECTORY Export, Import, Resource, Exception,
				Security, BaseRelocation, Debug, Copyright,
				GlobalPointer, ThreadLocalStorage, LoadConfig, BoundImport,
				IAT, DelayImport, ComDescriptor, Reserved;
		}

		public static class IMAGE_OPTIONAL_HEADER
		{
			public static readonly int MagicOffset = 0;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_OPTIONAL_HEADER32
		{
			public WORD Magic;
			public BYTE MajorLinkerVersion;
			public BYTE MinorLinkerVersion;
			public DWORD SizeOfCode;
			public DWORD SizeOfInitializedData;
			public DWORD SizeOfUninitializedData;
			public DWORD AddressOfEntryPoint;
			public DWORD BaseOfCode;
			public DWORD BaseOfData;
			public DWORD ImageBase;
			public DWORD SectionAlignment;
			public DWORD FileAlignment;
			public WORD MajorOperatingSystemVersion;
			public WORD MinorOperatingSystemVersion;
			public WORD MajorImageVersion;
			public WORD MinorImageVersion;
			public WORD MajorSubsystemVersion;
			public WORD MinorSubsystemVersion;
			public DWORD Win32VersionValue;
			public DWORD SizeOfImage;
			public DWORD SizeOfHeaders;
			public DWORD CheckSum;
			public WORD Subsystem;
			public WORD DllCharacteristics;
			public DWORD SizeOfStackReserve;
			public DWORD SizeOfStackCommit;
			public DWORD SizeOfHeapReserve;
			public DWORD SizeOfHeapCommit;
			public DWORD LoaderFlags;
			public DWORD NumberOfRvaAndSizes;
			public IMAGE_OPTIONAL_HEADER_DATA_DIRECTORY DataDirectory;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		public struct IMAGE_OPTIONAL_HEADER64
		{
			public WORD Magic;
			public BYTE MajorLinkerVersion;
			public BYTE MinorLinkerVersion;
			public DWORD SizeOfCode;
			public DWORD SizeOfInitializedData;
			public DWORD SizeOfUninitializedData;
			public DWORD AddressOfEntryPoint;
			public DWORD BaseOfCode;
			public DWORD BaseOfData;
			public ULONGLONG ImageBase;
			public DWORD SectionAlignment;
			public DWORD FileAlignment;
			public WORD MajorOperatingSystemVersion;
			public WORD MinorOperatingSystemVersion;
			public WORD MajorImageVersion;
			public WORD MinorImageVersion;
			public WORD MajorSubsystemVersion;
			public WORD MinorSubsystemVersion;
			public DWORD Win32VersionValue;
			public DWORD SizeOfImage;
			public DWORD SizeOfHeaders;
			public DWORD CheckSum;
			public WORD Subsystem;
			public WORD DllCharacteristics;
			public ULONGLONG SizeOfStackReserve;
			public ULONGLONG SizeOfStackCommit;
			public ULONGLONG SizeOfHeapReserve;
			public ULONGLONG SizeOfHeapCommit;
			public DWORD LoaderFlags;
			public DWORD NumberOfRvaAndSizes;
			public IMAGE_OPTIONAL_HEADER_DATA_DIRECTORY DataDirectory;
		}

		[StructLayout(LayoutKind.Explicit)]
		public struct IMAGE_SECTION_HEADER
		{
			[FieldOffset(0)]
			public ulong Name;
			[FieldOffset(8)]
			public DWORD PhysicalAddress;
			[FieldOffset(8)]
			public DWORD VirtualSize;
			[FieldOffset(12)]
			public DWORD VirtualAddress;
			[FieldOffset(16)]
			public DWORD SizeOfRawData;
			[FieldOffset(20)]
			public DWORD PointerToRawData;
			[FieldOffset(24)]
			public DWORD PointerToRelocations;
			[FieldOffset(28)]
			public DWORD PointerToLinenumbers;
			[FieldOffset(32)]
			public WORD NumberOfRelocations;
			[FieldOffset(34)]
			public WORD NumberOfLinenumbers;
			[FieldOffset(36)]
			public DWORD Characteristics;

			public string NameString
			{
				get
				{
					char[] chars = new char[8];
					int length = 0;
					while (length < 8)
					{
						chars[length] = unchecked((char)(byte)(Name >> (length * 8)));
						if (chars[length] == 0) break;
						++length;
					}
					return new string(chars, 0, length);
				}
			}
		}
	}
}
