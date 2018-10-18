using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Asmuth.X86.Xed
{
	public sealed class StaticXedInstructionConverter
	{
		private enum BitsMatchingState : byte
		{
			Initial,
			Post0FEscape,
			PostEscapes,
			PostMainByte5Bits,
			PostMainByte,
			PostMod,
			PostModReg,
			PostModRM,
			PostFirstImmediate,
			End
		}

		private enum FieldValueKind : byte
		{
			EqualConstant,
			NotEqualConstant,
			NotConstant
		}
		
		private readonly struct FieldValue
		{
			private readonly ushort constant;
			public FieldValueKind Kind { get; }

			public FieldValue(FieldValueKind kind, ushort constant)
			{
				this.Kind = kind;
				this.constant = constant;
			}

			public bool IsEquality(ushort value) => Kind == FieldValueKind.EqualConstant && constant == value;
			public bool IsInequality(ushort value) => Kind == FieldValueKind.NotEqualConstant && constant == value;

			public static readonly FieldValue NotConstant = new FieldValue(FieldValueKind.NotConstant, 0);
		}

		public static OpcodeEncoding GetOpcodeEncoding(IEnumerable<XedBlot> pattern)
		{
			var builder = new OpcodeEncoding.Builder();
			GetOpcodeEncoding_BitsAndCalls(pattern, ref builder);
			GetOpcodeEncoding_Equalities(pattern, ref builder);
			return builder.Build();
		}

		private static readonly Regex immediateCalleeRegex = new Regex(
			@"^ (?<se>SE_)? (?<n>SIMM|UIMM|BRDISP|MEMDISP) (?<s>8|16|32|64|v|z)? (_1)? $",
			RegexOptions.IgnorePatternWhitespace);

		private static void GetOpcodeEncoding_BitsAndCalls(IEnumerable<XedBlot> pattern,
			ref OpcodeEncoding.Builder builder)
		{
			var state = BitsMatchingState.Initial;
			foreach (var blot in pattern)
			{
				if (state == BitsMatchingState.Initial
					&& blot.Type == XedBlotType.Equality && blot.Field.Name == "VEXVALID"
					&& blot.Value.Kind == XedBlotValueKind.Constant && blot.Value.Constant > 0)
				{
					// VEX/EVEX/XOP-prefixed opcodes can't have escapes
					// This fixes VTESTPD whose main byte is 0x0F yet has no escapes
					state = BitsMatchingState.PostEscapes;
				}
				if (blot.Type == XedBlotType.Bits)
				{
					MatchBits(blot.Field, blot.BitPattern, ref state, ref builder);
				}
				else if (blot.Type == XedBlotType.Call)
				{
					var callee = blot.Callee;
					var immediateMatch = immediateCalleeRegex.Match(callee);
					if (immediateMatch.Success)
					{
						if (state != BitsMatchingState.PostMainByte
							&& state != BitsMatchingState.PostModRM
							&& state != BitsMatchingState.PostFirstImmediate)
							throw new FormatException();

						var typeName = immediateMatch.Groups["n"].Value;

						ImmediateSizeEncoding immediateSize;
						var bitSizeStr = immediateMatch.Groups["s"].Value;
						if (bitSizeStr == "8") immediateSize = ImmediateSizeEncoding.Byte;
						else if (bitSizeStr == "16") immediateSize = ImmediateSizeEncoding.Word;
						else if (bitSizeStr == "32") immediateSize = ImmediateSizeEncoding.Dword;
						else if (bitSizeStr == "64") immediateSize = ImmediateSizeEncoding.Qword;
						else if (bitSizeStr == "z")
						{
							if (typeName == "SIMM" || typeName == "UIMM" || typeName == "BRDISP")
								immediateSize = ImmediateSizeEncoding.WordOrDword_OperandSize;
							else
								throw new FormatException();
						}
						else if (bitSizeStr == "v")
						{
							if (typeName == "SIMM" || typeName == "UIMM")
								immediateSize = ImmediateSizeEncoding.WordOrDwordOrQword_OperandSize;
							else if (typeName == "MEMDISP")
								immediateSize = ImmediateSizeEncoding.WordOrDwordOrQword_AddressSize;
							else
								throw new FormatException();
						}
						else throw new NotImplementedException();

						builder.ImmediateSize = ImmediateSizeEncoding.Combine(builder.ImmediateSize, immediateSize);
						state = state < BitsMatchingState.PostFirstImmediate
							? BitsMatchingState.PostFirstImmediate : BitsMatchingState.End;
					}
					else if (callee == "MODRM")
					{
						// This decodes the memory RM (and potential SIB byte and displacement)
						if (state != BitsMatchingState.PostModRM) throw new FormatException();
						if (!builder.AddressingForm.HasModRM
							|| builder.AddressingForm.ModRM.Value.Mod != ModRMModEncoding.Memory)
							throw new FormatException();
					}
				}
			}
		}

		private static void MatchBits(XedField field, string bitPattern,
			ref BitsMatchingState state, ref OpcodeEncoding.Builder builder)
		{
			if (field == null)
			{
				if (!XedBitPattern.IsConstant(bitPattern)) throw new FormatException();

				if (bitPattern.Length == 8)
				{
					byte b = Convert.ToByte(bitPattern, fromBase: 2);
					if (state == BitsMatchingState.Initial && b == 0x0F)
					{
						builder.Map = OpcodeMap.Escape0F;
						state = BitsMatchingState.Post0FEscape;
					}
					else if (state == BitsMatchingState.Post0FEscape && b == 0x38)
					{
						builder.Map = OpcodeMap.Escape0F38;
						state = BitsMatchingState.PostEscapes;
					}
					else if (state == BitsMatchingState.Post0FEscape && b == 0x3A)
					{
						builder.Map = OpcodeMap.Escape0F3A;
						state = BitsMatchingState.PostEscapes;
					}
					else if (state == BitsMatchingState.PostMainByte || state == BitsMatchingState.PostModRM)
					{
						// 3D Now! suffix opcode
						builder.ImmediateSize = ImmediateSizeEncoding.Byte;
						builder.Imm8Ext = b;
						state = BitsMatchingState.End;
					}
					else if (state < BitsMatchingState.PostMainByte)
					{
						builder.MainByte = b;
						state = BitsMatchingState.PostMainByte;
					}
					else throw new FormatException();
				}
				else if (bitPattern.Length == 5)
				{
					if (state >= BitsMatchingState.PostMainByte) throw new FormatException();
					builder.MainByte = (byte)(Convert.ToByte(bitPattern, fromBase: 2) << 3);
					builder.AddressingForm = AddressingFormEncoding.MainByteEmbeddedRegister;
					state = BitsMatchingState.PostMainByte5Bits;
				}
				else throw new FormatException();
			}
			else
			{
				if (field.Name == "SRM")
				{
					if (state != BitsMatchingState.PostMainByte5Bits) throw new FormatException();
					if (bitPattern.Length != 3) throw new FormatException();
					if (XedBitPattern.IsConstant(bitPattern))
					{
						// This supports NOP90 as "0b1001_0 SRM[0b000]" - not sure why they chose this encoding
						builder.MainByte |= (byte)XedBitPattern.TryAsConstant(bitPattern).Value.Bits;
						builder.AddressingForm = AddressingFormEncoding.None;
					}
					else if (bitPattern != "rrr") throw new FormatException();
					state = BitsMatchingState.PostMainByte;
				}
				else if (field.Name == "MOD")
				{
					if (state != BitsMatchingState.PostMainByte) throw new FormatException();
					if (!builder.AddressingForm.IsNone) throw new FormatException();
					if (bitPattern.Length != 2) throw new FormatException();
					if (bitPattern == "11") builder.AddressingForm = new ModRMEncoding(ModRMModEncoding.Register);
					else if (bitPattern == "mm") builder.AddressingForm = new ModRMEncoding(ModRMModEncoding.Memory);
					else throw new FormatException();
					state = BitsMatchingState.PostMod;
				}
				else if (field.Name == "REG")
				{
					if (state != BitsMatchingState.PostMod) throw new FormatException();
					if (bitPattern.Length != 3) throw new FormatException();
					if (XedBitPattern.IsConstant(bitPattern))
					{
						byte reg = Convert.ToByte(bitPattern, fromBase: 2);
						builder.AddressingForm = new ModRMEncoding(
							builder.AddressingForm.ModRM.Value.Mod, reg);
					}
					else if (bitPattern != "rrr") throw new FormatException();
					state = BitsMatchingState.PostModReg;
				}
				else if (field.Name == "RM")
				{
					if (state != BitsMatchingState.PostModReg) throw new FormatException();
					if (bitPattern.Length != 3) throw new FormatException();
					if (XedBitPattern.IsConstant(bitPattern))
					{
						byte rm = Convert.ToByte(bitPattern, fromBase: 2);
						var modRM = builder.AddressingForm.ModRM.Value;
						if (modRM.Mod == ModRMModEncoding.Register)
						{
							builder.AddressingForm = new ModRMEncoding(
								ModRMModEncoding.DirectFixedRM, modRM.FixedReg, rm);
						}
						else if (modRM.Mod == ModRMModEncoding.Memory && rm == 0b100)
						{ } // VSIB
						else throw new FormatException();
					}
					else if (bitPattern != "nnn") throw new FormatException();
					state = BitsMatchingState.PostModRM;
				}
				else throw new FormatException();
			}
		}
		
		private static void GetOpcodeEncoding_Equalities(IEnumerable<XedBlot> pattern, ref OpcodeEncoding.Builder builder)
		{
			var fields = GatherFieldValues(pattern);

			if (fields.TryGetValue("MODE", out var mode))
			{
				if (mode.IsEquality(2)) builder.X64 = true;
				else if (mode.IsInequality(2) || mode.IsEquality(0) || mode.IsEquality(1))
					builder.X64 = false; // BNDMOV overloads patterns for 16/32-bit modes, but ignore those
				else throw new FormatException();
			}

			if (fields.TryGetValue("EASZ", out var easz))
			{
				if (easz.IsEquality(1))
				{
					builder.AddressSize = AddressSize._16;
					builder.X64 = false;
				}
				else if (easz.IsEquality(2)) builder.AddressSize = AddressSize._32;
				else if (easz.IsEquality(3))
				{
					builder.AddressSize = AddressSize._64;
					builder.X64 = true;
				}
				else if (easz.IsInequality(1)) { } // Some VEX opcodes don't support 16-bit addressing, ignore for now
				else throw new FormatException();
			}

			if (fields.TryGetValue("EOSZ", out var eosz))
			{
				if (eosz.IsEquality(1))
					builder.OperandSize = OperandSizeEncoding.Word;
				else if (eosz.IsEquality(2) || eosz.IsInequality(1))
					builder.OperandSize = OperandSizeEncoding.Dword;
				else if (eosz.IsEquality(3))
					builder.X64 = true; // If REX.W is required, it will be specified 
				else if (eosz.IsInequality(3))
					builder.OperandSize = OperandSizeEncoding.NoPromotion;
				else throw new FormatException();
			}
			
			// ToConvert:
			// public OperandSizeEncoding OperandSize;

			if (fields.TryGetValue("VEXVALID", out var vexValid))
			{
				if (vexValid.IsEquality(0)) builder.VexType = VexType.None;
				else if (vexValid.IsEquality(1)) builder.VexType = VexType.Vex;
				else if (vexValid.IsEquality(2)) builder.VexType = VexType.EVex;
				else if (vexValid.IsEquality(3)) builder.VexType = VexType.Xop;
				else throw new FormatException();
			}

			if (builder.VexType == VexType.None)
			{
				if (fields.TryGetValue("OSZ", out var osz))
				{
					if (osz.IsEquality(0)) builder.SimdPrefix = SimdPrefix.None;
					else if (osz.IsEquality(1)) builder.SimdPrefix = SimdPrefix._66;
					else throw new FormatException();
				}

				if (fields.TryGetValue("REP", out var rep))
				{
					if (rep.IsEquality(0)) { } // OSZ case
					else if (rep.IsEquality(2)) builder.SimdPrefix = SimdPrefix._F2;
					else if (rep.IsEquality(3)) builder.SimdPrefix = SimdPrefix._F3;
					else if (rep.Kind == FieldValueKind.NotEqualConstant) { } // Ignore, no way to represent
				}
			}
			else
			{
				var vexPrefix = fields["VEX_PREFIX"];
				if (vexPrefix.IsEquality(0)) builder.SimdPrefix = SimdPrefix.None;
				else if (vexPrefix.IsEquality(1)) builder.SimdPrefix = SimdPrefix._66;
				else if (vexPrefix.IsEquality(2)) builder.SimdPrefix = SimdPrefix._F2;
				else if (vexPrefix.IsEquality(3)) builder.SimdPrefix = SimdPrefix._F3;
				else throw new FormatException();

				if (fields.TryGetValue("VL", out var vectorLength))
				{
					if (vectorLength.IsEquality(0)) builder.VectorSize = AvxVectorSize._128;
					else if (vectorLength.IsEquality(1)) builder.VectorSize = AvxVectorSize._256;
					else if (vectorLength.IsEquality(2)) builder.VectorSize = AvxVectorSize._512;
					else throw new FormatException();
				}

				var map = fields["MAP"];
				if (map.IsEquality(0)) builder.Map = OpcodeMap.Default;
				else if (map.IsEquality(1)) builder.Map = OpcodeMap.Escape0F;
				else if (map.IsEquality(2)) builder.Map = OpcodeMap.Escape0F38;
				else if (map.IsEquality(3)) builder.Map = OpcodeMap.Escape0F3A;
				else if (map.IsEquality(8)) builder.Map = OpcodeMap.Xop8;
				else if (map.IsEquality(9)) builder.Map = OpcodeMap.Xop9;
				else if (map.IsEquality(10)) builder.Map = OpcodeMap.Xop10;
				else throw new FormatException();
			}

			if (fields.TryGetValue("REXW", out var rexW))
			{
				if (rexW.IsEquality(0)) builder.OperandSize = OperandSizeEncoding.NoPromotion;
				else if (rexW.IsEquality(1))
				{
					builder.OperandSize = OperandSizeEncoding.Promotion;
					if (builder.VexType == VexType.None) builder.X64 = true;
				}
				else throw new FormatException();
			}
		}

		private static SmallDictionary<string, FieldValue> GatherFieldValues(IEnumerable<XedBlot> pattern)
		{
			var fields = new SmallDictionary<string, FieldValue>();
			foreach (var blot in pattern)
			{
				if (blot.Type == XedBlotType.Equality)
				{
					fields[blot.Field.Name] = blot.Value.Kind == XedBlotValueKind.Constant
						? new FieldValue(FieldValueKind.EqualConstant, blot.Value.Constant)
						: FieldValue.NotConstant;
				}
				else if (blot.Type == XedBlotType.Inequality)
				{
					if (blot.Value.Kind != XedBlotValueKind.Constant) throw new FormatException();
					fields[blot.Field.Name] = new FieldValue(FieldValueKind.NotEqualConstant, blot.Value.Constant);
				}
			}

			return fields;
		}
	}
}
