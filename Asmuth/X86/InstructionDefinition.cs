using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed partial class InstructionDefinition
	{
		public struct Data
		{
			public string Mnemonic;
			public Opcode Opcode;
			public InstructionEncoding Encoding;
			public CpuidFeatureFlags RequiredFeatureFlags;
			public Flags? AffectedFlags;
		}

		#region Fields
		private readonly Data data;
		private readonly IReadOnlyList<OperandDefinition> operands;
		#endregion

		#region Constructor
		public InstructionDefinition(ref Data data, IEnumerable<OperandDefinition> operands)
		{
			Contract.Requires(operands != null);
			this.data = data;
			this.data.Opcode &= this.data.Encoding.GetOpcodeFixedMask();
			this.operands = operands.ToArray();
		}
		#endregion

		#region Properties
		public string Mnemonic => data.Mnemonic;
		public Opcode Opcode => data.Opcode;
		public Opcode OpcodeFixedMask => data.Encoding.GetOpcodeFixedMask();
		public InstructionEncoding Encoding => data.Encoding;
		public CpuidFeatureFlags RequiredFeatureFlags => data.RequiredFeatureFlags;
		public Flags? AffectedFlags => data.AffectedFlags;
		public IReadOnlyList<OperandDefinition> Operands => operands;
		#endregion

		#region Methods
		public bool IsMatch(Opcode opcode)
		{
			Opcode fixedMask = Encoding.GetOpcodeFixedMask();
			return (opcode & fixedMask) == (this.Opcode & fixedMask);
		}

		public string GetEncodingString() => GetEncodingString(Opcode, Encoding);

		public override string ToString() => Mnemonic;

		public static string GetEncodingString(Opcode opcode, InstructionEncoding encoding)
		{
			var str = new StringBuilder(30);

			var xexType = opcode & Opcode.XexType_Mask;
			if (xexType == Opcode.XexType_LegacyOrRex)
			{
				// Legacy Xex: 66 REX.W 0F 38
				switch (opcode & Opcode.SimdPrefix_Mask)
				{
					case Opcode.SimdPrefix_None: break;
					case Opcode.SimdPrefix_66: str.Append("66 "); break;
					case Opcode.SimdPrefix_F2: str.Append("F2 "); break;
					case Opcode.SimdPrefix_F3: str.Append("F3 "); break;
					default: throw new UnreachableException();
				}

				if ((encoding & InstructionEncoding.RexW_Mask) != InstructionEncoding.RexW_Ignored
					&& (opcode & Opcode.RexW) == Opcode.RexW)
				{
					str.Append("REX.W ");
				}

				switch (opcode & Opcode.Map_Mask)
				{
					case Opcode.Map_Default: break;
					case Opcode.Map_0F: str.Append("0F "); break;
					case Opcode.Map_0F38: str.Append("0F 38 "); break;
					case Opcode.Map_0F3A: str.Append("0F 3A "); break;
					default: throw new UnreachableException();
				}
			}
			else
			{
				// Vex/Xop/EVex: VEX.NDS.LIG.66.0F3A.WIG
				switch (xexType)
				{
					case Opcode.XexType_Vex: str.Append("VEX"); break;
					case Opcode.XexType_Xop: str.Append("XOP"); break;
					case Opcode.XexType_EVex: str.Append("EVEX"); break;
					default: throw new UnreachableException();
				}

				// TODO: Pretty print .NDS or similar

				switch (encoding & InstructionEncoding.VexL_Mask)
				{
					case InstructionEncoding.VexL_Fixed:
						switch (opcode & Opcode.VexL_Mask)
						{
							case Opcode.VexL_0: str.Append(".L0"); break;
							case Opcode.VexL_1: str.Append(".L1"); break;
							case Opcode.VexL_2: str.Append(".L2"); break;
							default: throw new UnreachableException();
						}
						break;

					case InstructionEncoding.VexL_Ignored:
						str.Append(".LIG");
						break;

					default: throw new NotImplementedException();
				}

				switch (opcode & Opcode.SimdPrefix_Mask)
				{
					case Opcode.SimdPrefix_None: break;
					case Opcode.SimdPrefix_66: str.Append(".66"); break;
					case Opcode.SimdPrefix_F2: str.Append(".F2"); break;
					case Opcode.SimdPrefix_F3: str.Append(".F3"); break;
					default: throw new UnreachableException();
				}

				if (xexType == Opcode.XexType_Xop)
				{
					switch (opcode & Opcode.Map_Mask)
					{
						case Opcode.Map_Xop8: str.Append(".M8"); break;
						case Opcode.Map_Xop9: str.Append(".M9"); break;
						case Opcode.Map_Xop10: str.Append(".M10"); break;
						default: throw new UnreachableException();
					}
				}
				else
				{
					switch (opcode & Opcode.Map_Mask)
					{
						case Opcode.Map_0F: str.Append(".0F"); break;
						case Opcode.Map_0F38: str.Append(".0F38"); break;
						case Opcode.Map_0F3A: str.Append(".0F3A"); break;
						default: throw new UnreachableException();
					}
				}

				switch (encoding & InstructionEncoding.RexW_Mask)
				{
					case InstructionEncoding.RexW_Fixed:
						str.Append((opcode & Opcode.RexW) == Opcode.RexW ? ".W1" : ".W0");
						break;

					case InstructionEncoding.RexW_Ignored:
						str.Append(".WIG");
						break;

					default: throw new NotImplementedException();
				}

				str.Append(' ');
			}

			// String tail: opcode byte and what follows  0B /r ib

			// The opcode itself
			str.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", opcode.GetMainByte());

			// Suffixes
			switch (encoding & InstructionEncoding.OpcodeFormat_Mask)
			{
				case InstructionEncoding.OpcodeFormat_FixedByte: break;
				case InstructionEncoding.OpcodeFormat_EmbeddedRegister: str.Append("+r"); break;
				case InstructionEncoding.OpcodeFormat_EmbeddedConditionCode: str.Append("+cc"); break;
				default: throw new UnreachableException();
			}

			switch (encoding & InstructionEncoding.ModRM_Mask)
			{
				case InstructionEncoding.ModRM_Fixed:
					str.AppendFormat(CultureInfo.InvariantCulture, " {0:X2}", opcode.GetExtraByte());
					break;

				case InstructionEncoding.ModRM_FixedModReg:
					str.AppendFormat(CultureInfo.InvariantCulture, " {0:X2}+r", opcode.GetExtraByte());
					break;

				case InstructionEncoding.ModRM_FixedReg:
					str.Append(" /");
					str.Append((char)('0' + (opcode.GetExtraByte() >> 3)));
					break;

				case InstructionEncoding.ModRM_Any: str.Append(" /r"); break;
				case InstructionEncoding.ModRM_None: break;
				default: throw new UnreachableException();
			}

			AppendImmediate(str, encoding.GetFirstImmediateSize());
			AppendImmediate(str, encoding.GetSecondImmediateSize());

			return str.ToString();
		}

		private static void AppendImmediate(StringBuilder str, ImmediateSize size)
		{
			switch (size)
			{
				case ImmediateSize.Zero: break;
				case ImmediateSize.Fixed8: str.Append(" ib"); break;
				case ImmediateSize.Fixed16: str.Append(" iw"); break;
				case ImmediateSize.Fixed32: str.Append(" id"); break;
				case ImmediateSize.Fixed64: str.Append(" iq"); break;
				case ImmediateSize.Operand16Or32: str.Append(" iwd"); break;
				case ImmediateSize.Operand16Or32Or64: str.Append(" iwdq"); break;
				case ImmediateSize.Address16Or32: str.Append(" rel"); break;
				default: throw new ArgumentException(nameof(size));
			}
		}
		#endregion
	}

	public struct OperandDefinition
	{
		public readonly OperandEncoding Encoding;
		public readonly OperandField Field;
		public readonly AccessType AccessType;

		public OperandDefinition(OperandField field, OperandEncoding encoding, AccessType accessType)
		{
			this.Field = field;
			this.Encoding = encoding;
			this.AccessType = accessType;
		}
	}
}
