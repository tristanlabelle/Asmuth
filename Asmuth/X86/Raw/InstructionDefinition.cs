using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86.Raw
{
	public sealed class InstructionDefinition
	{
		#region Fields
		private string mnemonic;
		private Opcode opcode;
		private InstructionEncoding encoding;
		private CpuidFeatureFlags requiredFeatureFlags;
		private EFlags? affectedFlags;
		private IList<OperandDefinition> operands;
		#endregion

		#region Constructor
		private InstructionDefinition() { }
		#endregion

		#region Properties
		public string Mnemonic => mnemonic;
		public Opcode Opcode => opcode;
		public Opcode OpcodeFixedMask => encoding.GetOpcodeFixedMask();
		public InstructionEncoding Encoding => encoding;
		public CpuidFeatureFlags RequiredFeatureFlags => requiredFeatureFlags;
		public EFlags? AffectedFlags => affectedFlags;
		public IReadOnlyList<OperandDefinition> Operands => (IReadOnlyList<OperandDefinition>)operands;
		public string IntelStyleEncodingString => GetIntelStyleEncodingString(opcode, encoding);
		#endregion

		#region Methods
		public bool IsMatch(Opcode opcode)
		{
			// The caller cannot distinguish between legacy and SIMD prefixes
			Opcode fixedMask = encoding.GetOpcodeFixedMask();
			return (opcode & fixedMask) == (this.opcode & fixedMask)
				|| (opcode.WithSimdPrefix(SimdPrefix.None) & fixedMask) == (this.opcode & fixedMask);
		}

		public override string ToString() => Mnemonic;

		public static IEnumerable<InstructionDefinition> MergeHeterogeneous(IEnumerable<InstructionDefinition> instructions)
		{
			Contract.Requires(instructions != null);

			// Group by masked opcode
			var groups = MultiValueDictionary<ulong, InstructionDefinition>.Create<List<InstructionDefinition>>();
			foreach (var instruction in instructions)
			{
				var opcodeFixedMask = instruction.Encoding.GetOpcodeFixedMask();
				ulong key = ((ulong)opcodeFixedMask << 32) | (ulong)(instruction.Opcode & opcodeFixedMask);
				groups.Add(key, instruction);
			}
			
			// Merge every group
			foreach (List<InstructionDefinition> group in groups.Values)
			{
				MergeHomogeneous(group);
				foreach (var instruction in group)
					yield return instruction;
			}
		}

		private static void MergeHomogeneous(IList<InstructionDefinition> group)
		{
			Contract.Requires(group != null);

			if (group.Count <= 1) return;

			throw new NotImplementedException();
		}

		public static string GetIntelStyleEncodingString(Opcode opcode, InstructionEncoding encoding)
		{
			var str = new StringBuilder(30);

			var xexType = opcode & Opcode.XexType_Mask;
			if (xexType == Opcode.XexType_Legacy)
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

			// TODO: Append immediates

			return str.ToString();
		}
		#endregion

		#region Builder class
		public sealed class Builder
		{
			private InstructionDefinition instruction = CreateEmpty();

			#region Properties
			public string Mnemonic
			{
				get { return instruction.mnemonic; }
				set { instruction.mnemonic = value; }
			}

			public Opcode Opcode
			{
				get { return instruction.opcode; }
				set { instruction.opcode = value; }
			}

			public InstructionEncoding Encoding
			{
				get { return instruction.encoding; }
				set { instruction.encoding = value; }
			}

			public CpuidFeatureFlags RequiredFeatureFlags
			{
				get { return instruction.requiredFeatureFlags; }
				set { instruction.requiredFeatureFlags = value; }
			}

			public EFlags? AffectedFlags
			{
				get { return instruction.affectedFlags; }
				set { instruction.affectedFlags = value; }
			}

			public IList<OperandDefinition> Operands => instruction.operands;
			#endregion

			#region Methods
			public InstructionDefinition Build(bool reuse = true)
			{
				instruction.opcode &= instruction.Encoding.GetOpcodeFixedMask();
				var result = instruction;
				instruction = reuse ? CreateEmpty() : null;
				return result;
			}

			private static InstructionDefinition CreateEmpty()
			{
				var instruction = new InstructionDefinition();
				instruction.operands = new List<OperandDefinition>();
				return instruction;
			}
			#endregion
		}
		#endregion
	}

	public struct OperandDefinition
	{
		public readonly OperandEncoding Encoding;
		public readonly OperandFields Field;
		public readonly AccessType AccessType;

		public OperandDefinition(OperandFields field, OperandEncoding encoding, AccessType accessType)
		{
			this.Field = field;
			this.Encoding = encoding;
			this.AccessType = accessType;
		}
	}
}
