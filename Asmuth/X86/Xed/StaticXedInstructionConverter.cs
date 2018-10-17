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

#pragma warning disable CS0660, CS0661 // Improper ==/!=, operator overloading hacks
		private readonly struct FieldValue
#pragma warning restore CS0660, CS0661
		{
			private readonly ushort constant;
			private readonly FieldValueKind kind;

			public FieldValue(FieldValueKind kind, ushort constant)
			{
				this.kind = kind;
				this.constant = constant;
			}

			public static readonly FieldValue NotConstant = new FieldValue(FieldValueKind.NotConstant, 0);

			public static bool operator ==(FieldValue value, ushort constant)
				=> value.kind == FieldValueKind.EqualConstant && value.constant == constant;
			public static bool operator !=(FieldValue value, ushort constant)
				=> value.kind == FieldValueKind.NotEqualConstant && value.constant != constant;
		}

		public static OpcodeEncoding GetOpcodeEncoding(IEnumerable<XedBlot> pattern)
		{
			var builder = new OpcodeEncoding.Builder();
			GetOpcodeEncoding_BitsAndCalls(pattern, ref builder);
			GetOpcodeEncoding_Equalities(pattern, ref builder);
			return builder.Build();
		}

		private static readonly Regex immediateCalleeRegex = new Regex(
			@"^ (?<se>SE_)? (?<n>SIMM|UIMM|BRDISP|MEMDISP) (?<s>8|16|32|64|v|z)? (?<2>_1)? $",
			RegexOptions.IgnorePatternWhitespace);

		private static void GetOpcodeEncoding_BitsAndCalls(IEnumerable<XedBlot> pattern,
			ref OpcodeEncoding.Builder builder)
		{
			var state = BitsMatchingState.Initial;
			foreach (var blot in pattern)
			{
				if (blot.Type == XedBlotType.Bits)
					MatchBits(blot.Field, blot.BitPattern, ref state, ref builder);
				else if (blot.Type == XedBlotType.Call)
				{
					var callee = blot.Callee;
					var immediateMatch = immediateCalleeRegex.Match(callee);
					if (immediateMatch.Success)
					{
						bool isFirstImmediate = !immediateMatch.Groups["2"].Success;
						if (isFirstImmediate
							? (state != BitsMatchingState.PostMainByte && state != BitsMatchingState.PostModRM)
							: state != BitsMatchingState.PostFirstImmediate)
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
						state = isFirstImmediate
							? BitsMatchingState.PostFirstImmediate
							: BitsMatchingState.End;
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
				if (state >= BitsMatchingState.PostMainByte) throw new FormatException();

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
						builder.Imm8Ext = b;
						state = BitsMatchingState.End;
					}
					else
					{
						builder.MainByte = b;
						state = BitsMatchingState.PostMainByte;
					}
				}
				else if (bitPattern.Length == 5)
				{
					builder.MainByte = (byte)(Convert.ToByte(bitPattern, fromBase: 2) << 3);
					builder.AddressingForm = AddressingFormEncoding.MainByteEmbeddedRegister;
					state = BitsMatchingState.PostMainByte5Bits;
				}
				else throw new FormatException();
			}
			else
			{
				if (field.Name == "SVN")
				{
					if (state != BitsMatchingState.PostMainByte5Bits) throw new FormatException();
					if (bitPattern.Length != 3) throw new FormatException();
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
						if (modRM.Mod != ModRMModEncoding.Register) throw new FormatException();
						builder.AddressingForm = new ModRMEncoding(
							ModRMModEncoding.DirectFixedRM, modRM.FixedReg, rm);
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
				if (mode == 2) builder.X64 = true;
				else if (mode != 2) builder.X64 = false;
				else throw new FormatException();
			}

			if (fields.TryGetValue("EASZ", out var easz))
			{
				if (easz == 1) builder.AddressSize = AddressSize._16;
				else if (easz == 2) builder.AddressSize = AddressSize._32;
				else if (easz == 3) builder.AddressSize = AddressSize._64;
				else if (easz != 1) { } // Some VEX opcodes don't support 16-bit addressing, ignore for now
				else throw new FormatException();
			}

			if (fields.TryGetValue("REXW", out var rexW))
			{
				if (rexW == 0) builder.OperandSizePromotion = false;
				else if (rexW == 1) builder.OperandSizePromotion = true;
				else throw new FormatException();
			}

			if (fields.TryGetValue("VEXVALID", out var vexValid))
			{
				if (vexValid == 0) builder.VexType = VexType.None;
				else if (vexValid == 1) builder.VexType = VexType.Vex;
				else if (vexValid == 2) builder.VexType = VexType.EVex;
				else if (vexValid == 3) builder.VexType = VexType.Xop;
				else throw new FormatException();
			}

			if (builder.VexType == VexType.None)
			{
				if (fields.TryGetValue("OSZ", out var osz))
				{
					if (osz == 0) builder.SimdPrefix = SimdPrefix.None;
					else if (osz == 1) builder.SimdPrefix = SimdPrefix._66;
					else throw new FormatException();
				}

				if (fields.TryGetValue("REP", out var rep))
				{
					if (rep == 0) { } // OSZ case
					else if (rep == 2) builder.SimdPrefix = SimdPrefix._F2;
					else if (rep == 3) builder.SimdPrefix = SimdPrefix._F3;
					else throw new FormatException();
				}
			}
			else
			{
				var vexPrefix = fields["VEX_PREFIX"];
				if (vexPrefix == 0) builder.SimdPrefix = SimdPrefix.None;
				else if (vexPrefix == 1) builder.SimdPrefix = SimdPrefix._66;
				else if (vexPrefix == 2) builder.SimdPrefix = SimdPrefix._F2;
				else if (vexPrefix == 3) builder.SimdPrefix = SimdPrefix._F3;
				else throw new FormatException();

				var vectorLength = fields["VL"];
				if (vectorLength == 0) builder.VectorSize = AvxVectorSize._128;
				else if (vectorLength == 1) builder.VectorSize = AvxVectorSize._256;
				else if (vectorLength == 2) builder.VectorSize = AvxVectorSize._512;
				else throw new FormatException();

				var map = fields["MAP"];
				if (vectorLength == 0) builder.Map = OpcodeMap.Default;
				else if (vectorLength == 1) builder.Map = OpcodeMap.Escape0F;
				else if (vectorLength == 2) builder.Map = OpcodeMap.Escape0F38;
				else if (vectorLength == 3) builder.Map = OpcodeMap.Escape0F3A;
				else if (vectorLength == 8) builder.Map = OpcodeMap.Xop8;
				else if (vectorLength == 9) builder.Map = OpcodeMap.Xop9;
				else if (vectorLength == 10) builder.Map = OpcodeMap.Xop10;
				else throw new FormatException();
			}

			// ToConvert:
			// public AddressSize? AddressSize;
			// public OperandSizeEncoding OperandSize;
			// public byte? Imm8Ext;
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
