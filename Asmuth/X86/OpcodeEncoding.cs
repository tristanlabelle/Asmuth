using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Asmuth.X86
{
	public enum OperandSizeEncoding : byte
	{
		Any,
		// No Byte because r/m8 are different opcodes
		Word,
		Dword
		// No Qword because disambiguation is done through bool? X64/OperandSizePromotion
	}

	public static class OperandSizeEncodingEnum
	{
		public static IntegerSize? AsIntegerSize(this OperandSizeEncoding value)
		{
			switch (value)
			{
				case OperandSizeEncoding.Any: return null;
				case OperandSizeEncoding.Word: return IntegerSize.Word;
				case OperandSizeEncoding.Dword: return IntegerSize.Dword;
				default: throw new ArgumentOutOfRangeException(nameof(value));
			}
		}
	}

	public readonly partial struct OpcodeEncoding
	{
		#region Builder Struct
		public struct Builder
		{
			public bool? X64;
			public AddressSize? AddressSize;
			public OperandSizeEncoding OperandSize;
			public VexType VexType;
			public SseVectorSize? VectorSize;
			public bool? OperandSizePromotion;
			public SimdPrefix? SimdPrefix;
			public OpcodeMap Map;
			public byte MainByte;
			public ModRMEncoding ModRM;
			public int ImmediateSizeInBytes;
			public byte? Imm8Ext;

			public void Validate()
			{
				if (AddressSize == X86.AddressSize._64 && X64 != true)
					throw new ArgumentException("64-bit addresses imply X64 mode.");
				if (AddressSize == X86.AddressSize._16 && X64 != false)
					throw new ArgumentException("16-bit addresses imply IA32 mode.");
				if (OperandSize == OperandSizeEncoding.Word && X64 != false)
					throw new ArgumentException("16-bit operands imply IA32 mode.");
				if (Map == OpcodeMap.Default && SimdPrefix.HasValue)
					throw new ArgumentException("Default opcode map implies no SIMD prefix.");
				if (VexType != VexType.None && !SimdPrefix.HasValue)
					throw new ArgumentException("Vex encoding implies SIMD prefixes.");
				if (VexType == VexType.None && VectorSize.HasValue)
					throw new ArgumentException("Escape-based non-legacy prefixes implies ignored VEX.L.");
				if (ModRM == ModRMEncoding.MainByteReg && MainOpcodeByte.GetEmbeddedReg(MainByte) != 0)
					throw new ArgumentException("Main byte-embedded reg implies multiple-of-8 main byte.");
				if (Imm8Ext.HasValue && ImmediateSizeInBytes != 1)
					throw new ArgumentException("imm8 opcode extension implies 8-bit immediate.");
			}

			public OpcodeEncoding Build() => new OpcodeEncoding(ref this);

			public static implicit operator OpcodeEncoding(Builder builder) => builder.Build();
		}
		#endregion

		#region Packed Fields
		// 0b00AABBCC: Nullable<X64>, Nullable<AddressSize>, Nullable<OperandSize>
		private readonly byte contextFields;
		public bool? X64 => AsBool_ZeroIsNull((contextFields >> 4) & 3);
		public AddressSize? AddressSize => (AddressSize?)AsInt_ZeroIsNull((contextFields >> 2) & 3);
		public OperandSizeEncoding OperandSize => (OperandSizeEncoding)(contextFields & 3);
		private static byte MakeContextFields(bool? x64, AddressSize? addressSize, OperandSizeEncoding operandSize)
			=> (byte)((AsZeroIsNull(x64) << 4)
			| (AsZeroIsNull((int?)addressSize) << 2)
			| (byte)operandSize);

		// 0b00AABBCC: VexType, Nullable<VectorSize>, Nullable<OperandSizePromotion>
		private readonly byte vexFields;
		public VexType VexType => (VexType)((vexFields >> 4) & 3);
		public SseVectorSize? VectorSize => (SseVectorSize?)AsInt_ZeroIsNull((vexFields >> 2) & 3);
		public bool? OperandSizePromotion => AsBool_ZeroIsNull(vexFields & 3);
		private static byte MakeVexFields(VexType vexType, SseVectorSize? vectorSize, bool? operandSizePromotion)
			=> (byte)(((int)vexType << 4)
			| (AsZeroIsNull((int?)vectorSize) << 2)
			| AsZeroIsNull(operandSizePromotion));

		// 0b0AAABBBB: Nullable<SimdPrefix>, Map
		private readonly byte mapFields;
		public SimdPrefix? SimdPrefix => (SimdPrefix?)AsInt_ZeroIsNull((mapFields >> 4) & 7);
		public OpcodeMap Map => (OpcodeMap)(mapFields & 0xF);
		private static byte MakeMapFields(SimdPrefix? simdPrefix, OpcodeMap map)
			=> (byte)((AsZeroIsNull((int?)simdPrefix) << 4) | (int)map);

		public byte MainByte { get; }
		public ModRMEncoding ModRM { get; }

		// 0b000ABBBB: HasImm8Ext, ImmediateSizeInBytes
		private readonly byte immFields;
		private readonly byte imm8Ext;
		public int ImmediateSizeInBytes => immFields & 0xF;
		public byte? Imm8Ext => immFields == 0b10001 ? (byte?)imm8Ext : null;
		private static byte MakeImmFields(int sizeInBytes, byte? imm8Ext)
			=> (byte)(imm8Ext.HasValue ? 0x10 | sizeInBytes : sizeInBytes);
		#endregion

		public OpcodeEncoding(ref Builder builder)
		{
			builder.Validate();
			contextFields = MakeContextFields(builder.X64, builder.AddressSize, builder.OperandSize);
			vexFields = MakeVexFields(builder.VexType, builder.VectorSize, builder.OperandSizePromotion);
			mapFields = MakeMapFields(builder.SimdPrefix, builder.Map);
			MainByte = builder.MainByte;
			ModRM = builder.ModRM;
			immFields = MakeImmFields(builder.ImmediateSizeInBytes, builder.Imm8Ext);
			imm8Ext = builder.Imm8Ext.GetValueOrDefault();
		}

		public byte MainByteMask => ModRM.MainByteMask;
		public bool HasModRM => ModRM.IsPresent;
		public int? ImmediateSizeInBits => ImmediateSizeInBytes * 8;

		public bool IsValidInCodeSegment(CodeSegmentType codeSegmentType)
		{
			if (!X64.HasValue) return true;
			return X64.Value
				? codeSegmentType == CodeSegmentType.X64
				: codeSegmentType != CodeSegmentType.X64;
		}

		#region Matching
		public bool IsMatchUpToMainByte(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes, byte mainByte)
		{
			if (!IsValidInCodeSegment(codeSegmentType)) return false;

			var effectiveAddressSize = codeSegmentType.GetEffectiveAddressSize(legacyPrefixes);
			if (effectiveAddressSize != AddressSize.GetValueOrDefault(effectiveAddressSize)) return false;

			if (nonLegacyPrefixes.VexType != VexType) return false;
			if (nonLegacyPrefixes.VectorSize != VectorSize.GetValueOrDefault(nonLegacyPrefixes.VectorSize)) return false;

			var integerSize = codeSegmentType.GetIntegerOperandSize(legacyPrefixes.HasOperandSizeOverride, nonLegacyPrefixes.OperandSizePromotion);
			if (integerSize != OperandSize.AsIntegerSize().GetValueOrDefault(integerSize)) return false;
			
			var potentialSimdPrefix = nonLegacyPrefixes.SimdPrefix ?? legacyPrefixes.PotentialSimdPrefix;
			if (potentialSimdPrefix != SimdPrefix.GetValueOrDefault(potentialSimdPrefix)) return false;

			if (nonLegacyPrefixes.OpcodeMap != Map) return false;
			if ((mainByte & MainByteMask) != MainByte) return false;

			return true;
		}

		public bool IsMatch(CodeSegmentType codeSegmentType,
			ImmutableLegacyPrefixList legacyPrefixes, NonLegacyPrefixes nonLegacyPrefixes,
			byte mainByte, ModRM? modRM, byte? imm8)
		{
			if (!IsMatchUpToMainByte(codeSegmentType, legacyPrefixes, nonLegacyPrefixes, mainByte)) return false;
			if (!ModRM.IsValid(modRM)) return false;
			if (imm8.HasValue != (ImmediateSizeInBytes == 1)) return false;
			if (Imm8Ext.HasValue && imm8.Value != Imm8Ext.Value) return false;
			return true;
		}

		public bool IsMatch(in Instruction instruction)
		{
			var imm8 = instruction.ImmediateSizeInBytes == 1 ? instruction.ImmediateData.GetByte(0) : (byte?)null;
			return IsMatch(instruction.CodeSegmentType,
				instruction.LegacyPrefixes, instruction.NonLegacyPrefixes, instruction.MainOpcodeByte,
				instruction.ModRM, imm8);
		}
		#endregion
		
		public VexEncoding ToVexEncoding()
		{
			if (VexType == VexType.None) throw new InvalidOperationException();
			return new VexEncoding.Builder
			{
				Type = VexType,
				RegOperand = VexRegOperand.Invalid, // Lost in translation
				VectorSize = VectorSize,
				SimdPrefix = SimdPrefix.Value,
				OpcodeMap = Map,
				OperandSizePromotion = OperandSizePromotion
			}.Build();
		}

		#region ToString
		public override string ToString()
		{
			var str = new StringBuilder(30);

			str.Append('[');

			if (X64.HasValue)
				str.Append(X64.Value ? "x64 " : "ia32 ");

			if (AddressSize.HasValue)
				str.AppendFormat(CultureInfo.InvariantCulture, "a{0} ",
					AddressSize.Value.InBits());

			if (OperandSize != OperandSizeEncoding.Any)
				str.AppendFormat(CultureInfo.InvariantCulture, "o{0} ",
					OperandSize.AsIntegerSize().Value.InBits());

			str.Length--; // Remove '[' or space
			if (str.Length > 0) str.Append("] ");

			if (VexType == VexType.None) AppendNonVexPrefixes(str);
			else
			{
				str.Append(ToVexEncoding().ToIntelStyleString());
				str.Append(' ');
			}

			// String tail: opcode byte and what follows: 0B /r ib

			// The opcode itself
			str.AppendFormat(CultureInfo.InvariantCulture, "{0:x2}", MainByte);
			if (ModRM == ModRMEncoding.MainByteReg)
				str.Append("+r");

			if (ModRM.IsPresent)
			{
				str.Append(' ');
				str.Append(ModRM.ToString());
			}

			if (ImmediateSizeInBytes > 0)
			{
				str.AppendFormat(CultureInfo.InvariantCulture, " i{0}", ImmediateSizeInBits * 8);
			}

			return str.ToString();
		}

		private void AppendNonVexPrefixes(StringBuilder str)
		{
			// 66 REX.W 0F 38
			if (SimdPrefix.HasValue)
			{
				switch (SimdPrefix.Value)
				{
					case X86.SimdPrefix.None: str.Append("np "); break;
					case X86.SimdPrefix._66: str.Append("66 "); break;
					case X86.SimdPrefix._F3: str.Append("f3 "); break;
					case X86.SimdPrefix._F2: str.Append("f2 "); break;
					default: throw new UnreachableException();
				}
			}

			if (OperandSizePromotion == true)
				str.Append("rex.w ");

			switch (Map)
			{
				case OpcodeMap.Default: break;
				case OpcodeMap.Escape0F: str.Append("0f "); break;
				case OpcodeMap.Escape0F38: str.Append("0f 38 "); break;
				case OpcodeMap.Escape0F3A: str.Append("0f 3a "); break;
				default: throw new UnreachableException();
			}
		}
		#endregion

		private static int AsZeroIsNull(bool? value)
			=> value.HasValue ? (value.Value ? 2 : 1) : 0;

		private static int AsZeroIsNull(int? value)
			=> value.GetValueOrDefault(-1) + 1;

		private static bool? AsBool_ZeroIsNull(int value)
			=> value == 0 ? (bool?)null : value > 1;

		private static int? AsInt_ZeroIsNull(int value)
			=> value == 0 ? (int?)null : value - 1;
	}
}
