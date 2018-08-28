using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public enum NonLegacyPrefixesForm : byte
	{
		Escapes,
		RexAndEscapes,
		Vex2,
		Vex3,
		Xop,
		EVex,
	}

	public static class NonLegacyPrefixesFormEnum
	{
		public static VexType GetVexType(this NonLegacyPrefixesForm form)
		{
			switch (form)
			{
				case NonLegacyPrefixesForm.Vex2: case NonLegacyPrefixesForm.Vex3: return VexType.Vex;
				case NonLegacyPrefixesForm.Xop: return VexType.Xop;
				case NonLegacyPrefixesForm.EVex: return VexType.EVex;
				default: return VexType.None;
			}
		}

		public static bool AllowsEscapes(this NonLegacyPrefixesForm form)
			=> form <= NonLegacyPrefixesForm.RexAndEscapes;

		public static bool IsVex(this NonLegacyPrefixesForm form)
			=> form == NonLegacyPrefixesForm.Vex2 || form == NonLegacyPrefixesForm.Vex3;

		public static bool IsVex3Xop(this NonLegacyPrefixesForm form)
			=> form == NonLegacyPrefixesForm.Vex3 || form == NonLegacyPrefixesForm.Xop;

		public static NonLegacyPrefixesForm SniffByte(CodeSegmentType codeSegmentType, byte @byte)
			=> SniffByte(codeSegmentType, @byte, second: null);

		public static NonLegacyPrefixesForm FromBytes(CodeSegmentType codeSegmentType, byte first, byte second)
			=> SniffByte(codeSegmentType, first, second);

		private static NonLegacyPrefixesForm SniffByte(CodeSegmentType codeSegmentType, byte first, byte? second)
		{
			ModRM secondRM = (ModRM)second.GetValueOrDefault();
			switch (first)
			{
				case Vex2.FirstByte: 
					if (second.HasValue && !Vex2.Test(codeSegmentType, first, second.Value))
						return NonLegacyPrefixesForm.Escapes;
					return NonLegacyPrefixesForm.Vex2;

				case Vex3Xop.FirstByte_Vex3:
				case Vex3Xop.FirstByte_Xop:
					if (second.HasValue && !Vex3Xop.Test(codeSegmentType, first, second.Value))
						return NonLegacyPrefixesForm.Escapes;
					return first == Vex3Xop.FirstByte_Vex3 ? NonLegacyPrefixesForm.Vex3 : NonLegacyPrefixesForm.Xop;

				case (byte)EVex.FirstByte:
					if (codeSegmentType.IsIA32() && second.HasValue && secondRM.IsMemoryRM)
						return NonLegacyPrefixesForm.Escapes; // This is no EVEX, it's a BOUND (62 /r)
					return NonLegacyPrefixesForm.EVex;

				default:
					return codeSegmentType.IsLongMode() && Rex.Test(first)
						? NonLegacyPrefixesForm.RexAndEscapes : NonLegacyPrefixesForm.Escapes;
			}
		}

		public static int GetMinSizeInBytes(this NonLegacyPrefixesForm form)
		{
			switch (form)
			{
				case NonLegacyPrefixesForm.Escapes: return 0;
				case NonLegacyPrefixesForm.RexAndEscapes: return 1;
				case NonLegacyPrefixesForm.Vex2: return 2;
				case NonLegacyPrefixesForm.Vex3: return 3;
				case NonLegacyPrefixesForm.Xop: return 3;
				case NonLegacyPrefixesForm.EVex: return 4;
				default: throw new ArgumentOutOfRangeException(nameof(form));
			}
		}

		public static bool CanEncodeOpcodeMap(this NonLegacyPrefixesForm form, OpcodeMap map)
		{
			switch (form)
			{
				case NonLegacyPrefixesForm.Escapes:
				case NonLegacyPrefixesForm.RexAndEscapes:
					return map <= OpcodeMap.Escape0F3A;

				case NonLegacyPrefixesForm.Vex2:
				case NonLegacyPrefixesForm.Vex3:
				case NonLegacyPrefixesForm.EVex:
					return map >= OpcodeMap.Escape0F && map <= OpcodeMap.Escape0F3A;

				case NonLegacyPrefixesForm.Xop:
					return map >= OpcodeMap.Xop8 && map <= OpcodeMap.Xop10;

				default:
					throw new ArgumentException(nameof(map));
			}
		}
	}

	[StructLayout(LayoutKind.Sequential, Size = 4)]
	public struct NonLegacyPrefixes : IEquatable<NonLegacyPrefixes>
	{
		[Flags]
		private enum Flags : uint
		{
			Form_Shift = 0,
			Form_Mask = 7 << (int)Form_Shift,

			SimdPrefix_Shift = Form_Shift + 3,
			SimdPrefix_Mask = 3U << (int)SimdPrefix_Shift,

			OpcodeMap_Shift = SimdPrefix_Shift + 2,
			OpcodeMap_Mask = 0x1F << (int)OpcodeMap_Shift,

			NonDestructiveReg_Shift = OpcodeMap_Shift + 5,
			NonDestructiveReg_Mask = 0x1F << (int)NonDestructiveReg_Shift,

			VectorSize_Shift = NonDestructiveReg_Shift + 5,
			VectorSize_128 = 0 << (int)VectorSize_Shift,
			VectorSize_256 = 1 << (int)VectorSize_Shift,
			VectorSize_512 = 2 << (int)VectorSize_Shift,
			VectorSize_Mask = 3 << (int)VectorSize_Shift,

			OperandSizePromotion = 1 << (int)(VectorSize_Shift + 2),
			ModRegExtension = OperandSizePromotion << 1,
			BaseRegExtension = ModRegExtension << 1,
			IndexRegExtension = BaseRegExtension << 1,
		}

		#region Fields
		public static readonly NonLegacyPrefixes None = new NonLegacyPrefixes();

		private readonly Flags flags;
		#endregion

		#region Constructors
		public NonLegacyPrefixes(OpcodeMap escapes)
		{
			if (!escapes.IsEncodableAsEscapeBytes()) throw new ArgumentException();
			flags = BaseFlags(NonLegacyPrefixesForm.Escapes, X86.SimdPrefix.None, escapes);
		}

		public NonLegacyPrefixes(Rex rex, OpcodeMap escapes = OpcodeMap.Default)
		{
			if (!escapes.IsEncodableAsEscapeBytes()) throw new ArgumentException();
			flags = BaseFlags(NonLegacyPrefixesForm.Escapes, X86.SimdPrefix.None, escapes);
			if (rex.OperandSizePromotion) flags |= Flags.OperandSizePromotion;
			if (rex.ModRegExtension) flags |= Flags.ModRegExtension;
			if (rex.BaseRegExtension) flags |= Flags.BaseRegExtension;
			if (rex.IndexRegExtension) flags |= Flags.IndexRegExtension;
		}

		public NonLegacyPrefixes(Vex2 vex2)
		{
			flags = BaseFlags(NonLegacyPrefixesForm.Vex2, vex2.SimdPrefix, OpcodeMap.Default);
			flags |= (Flags)((uint)vex2.NonDestructiveReg << (int)Flags.NonDestructiveReg_Shift);
			if (vex2.ModRegExtension) flags |= Flags.ModRegExtension;
			if (vex2.VectorSize256) flags |= Flags.VectorSize_256;
		}

		public NonLegacyPrefixes(Vex3Xop vex3)
		{
			flags = BaseFlags(vex3.IsXop? NonLegacyPrefixesForm.Xop : NonLegacyPrefixesForm.Vex3, vex3.SimdPrefix, vex3.OpcodeMap);
			flags |= (Flags)((uint)vex3.NonDestructiveReg << (int)Flags.NonDestructiveReg_Shift);
			if (vex3.ModRegExtension) flags |= Flags.ModRegExtension;
			if (vex3.BaseRegExtension) flags |= Flags.BaseRegExtension;
			if (vex3.IndexRegExtension) flags |= Flags.IndexRegExtension;
			if (vex3.OperandSizePromotion) flags |= Flags.OperandSizePromotion;
			if (vex3.VectorSize256) flags |= Flags.VectorSize_256;
		}

		public NonLegacyPrefixes(EVex evex)
		{
			throw new NotImplementedException();
		}

		private NonLegacyPrefixes(Flags flags)
		{
			this.flags = flags;
		}
		#endregion

		#region Properties
		public NonLegacyPrefixesForm Form => (NonLegacyPrefixesForm)GetField(Flags.Form_Mask, Flags.Form_Shift);
		public VexType VexType => Form.GetVexType();
		public OpcodeMap OpcodeMap => (OpcodeMap)GetField(Flags.OpcodeMap_Mask, Flags.OpcodeMap_Shift);

		public SimdPrefix? SimdPrefix
		{
			get
			{
				if (Form <= NonLegacyPrefixesForm.RexAndEscapes) return null;
				return (SimdPrefix)GetField(Flags.SimdPrefix_Mask, Flags.SimdPrefix_Shift);
			}
		}

		public bool OperandSizePromotion => (flags & Flags.OperandSizePromotion) != 0;
		public bool ModRegExtension => (flags & Flags.ModRegExtension) != 0;
		public bool BaseRegExtension => (flags & Flags.BaseRegExtension) != 0;
		public bool IndexRegExtension => (flags & Flags.IndexRegExtension) != 0;

		public SseVectorSize VectorSize => (SseVectorSize)GetField(Flags.VectorSize_Mask, Flags.VectorSize_Shift);

		public byte? NonDestructiveReg
		{
			get
			{
				switch (Form)
				{
					case NonLegacyPrefixesForm.Vex2:
					case NonLegacyPrefixesForm.Vex3:
					case NonLegacyPrefixesForm.Xop:
					case NonLegacyPrefixesForm.EVex:
						return (byte)GetField(Flags.NonDestructiveReg_Mask, Flags.NonDestructiveReg_Shift);

					default: return null;
				}
			}
		}
		
		public int SizeInBytes
		{
			get
			{
				switch (Form)
				{
					case NonLegacyPrefixesForm.Escapes: return OpcodeMap.GetEscapeByteCount();
					case NonLegacyPrefixesForm.RexAndEscapes: return 1 + OpcodeMap.GetEscapeByteCount();
					case NonLegacyPrefixesForm.Vex2: return 2;
					case NonLegacyPrefixesForm.Vex3: return 3;
					case NonLegacyPrefixesForm.Xop: return 3;
					case NonLegacyPrefixesForm.EVex: return 4;
					default: throw new UnreachableException();
				}
			}
		}
		#endregion

		#region Methods
		public Rex GetRex()
		{
			if (Form != NonLegacyPrefixesForm.RexAndEscapes) throw new InvalidOperationException();

			return new Rex.Builder
			{
				ModRegExtension = ModRegExtension,
				BaseRegExtension = BaseRegExtension,
				IndexRegExtension = IndexRegExtension,
				OperandSizePromotion = OperandSizePromotion
			};
		}

		public NonLegacyPrefixes WithOpcodeMap(OpcodeMap map)
		{
			if (!Form.CanEncodeOpcodeMap(map))
				throw new ArgumentException("The current non-legacy prefix form cannot encode this opcode map.", nameof(map));
			return new NonLegacyPrefixes((flags & ~Flags.OpcodeMap_Mask)
				| (Flags)((uint)map << (int)Flags.OpcodeMap_Shift));
		}

		public static bool Equals(NonLegacyPrefixes first, NonLegacyPrefixes second) => first.Equals(second);
		public bool Equals(NonLegacyPrefixes other) => flags == other.flags;
		public override bool Equals(object obj) => obj is NonLegacyPrefixes && Equals((NonLegacyPrefixes)obj);
		public override int GetHashCode() => unchecked((int)flags);

		public override string ToString() => Form.ToString();

		private uint GetField(Flags mask, Flags shift)
			=> Bits.MaskAndShiftRight((uint)flags, (uint)mask, (int)shift);

		private static Flags BaseFlags(NonLegacyPrefixesForm type, SimdPrefix simdPrefix, OpcodeMap opcodeMap)
			=> (Flags)(((uint)type << (int)Flags.Form_Shift)
				| ((uint)simdPrefix << (int)Flags.SimdPrefix_Shift)
				| ((uint)opcodeMap << (int)Flags.OpcodeMap_Shift));
		#endregion

		#region Operators
		public static bool operator ==(NonLegacyPrefixes lhs, NonLegacyPrefixes rhs) => Equals(lhs, rhs);
		public static bool operator !=(NonLegacyPrefixes lhs, NonLegacyPrefixes rhs) => !Equals(lhs, rhs);
		#endregion
	}
}
