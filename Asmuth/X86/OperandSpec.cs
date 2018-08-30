using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Asmuth.X86
{
	public abstract class OperandSpec
	{
		private OperandSpec() { } // Disallow external inheritance

		// Used for NASM's "size match"
		public abstract IntegerSize? ImpliedIntegerOperandSize { get; }

		public abstract bool IsValidField(OperandField field);

		public abstract void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip = null);

		public string Format(in Instruction instruction, OperandField? field, ulong? ip = null)
		{
			var stringWriter = new StringWriter();
			Format(stringWriter, in instruction, field, ip);
			return stringWriter.ToString();
		}

		public abstract override string ToString();

		public interface IWithReg
		{
			RegisterClass RegisterClass { get; }
		}

		// PUSH CS
		public sealed class FixedReg : OperandSpec, IWithReg
		{
			public Register Register { get; }
			public RegisterClass RegisterClass => Register.Class;

			public FixedReg(Register register) => this.Register = register;
			public FixedReg(RegisterClass @class, byte index)
				: this(new Register(@class, index)) {}

			public override IntegerSize? ImpliedIntegerOperandSize => Register.IsSizedGpr
				? IntegerSizeEnum.TryFromBytes(Register.SizeInBytes.Value) : null;

			public override bool IsValidField(OperandField field) => false;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
				=> textWriter.Write(Register.Name);

			public override string ToString() => Register.Name;

			public static readonly FixedReg AL = new FixedReg(Register.AL);
			public static readonly FixedReg CL = new FixedReg(Register.CL);
			public static readonly FixedReg AX = new FixedReg(Register.AX);
			public static readonly FixedReg CX = new FixedReg(Register.CX);
			public static readonly FixedReg DX = new FixedReg(Register.DX);
			public static readonly FixedReg Eax = new FixedReg(Register.Eax);
			public static readonly FixedReg Ecx = new FixedReg(Register.Ecx);
			public static readonly FixedReg Edx = new FixedReg(Register.Edx);
			public static readonly FixedReg Rax = new FixedReg(Register.Rax);
			public static readonly FixedReg Rcx = new FixedReg(Register.Rcx);

			public static readonly FixedReg ST0 = new FixedReg(Register.ST0);
			public static readonly FixedReg Xmm0 = new FixedReg(Register.Xmm0);

			public static readonly FixedReg ES = new FixedReg(Register.ES);
			public static readonly FixedReg CS = new FixedReg(Register.CS);
			public static readonly FixedReg SS = new FixedReg(Register.SS);
			public static readonly FixedReg DS = new FixedReg(Register.DS);
			public static readonly FixedReg FS = new FixedReg(Register.FS);
			public static readonly FixedReg GS = new FixedReg(Register.GS);
		}

		// PUSH r32
		public sealed class Reg : OperandSpec, IWithReg
		{
			public RegisterClass RegisterClass { get; }
			public RegisterFamily RegisterFamily => RegisterClass.Family;

			public Reg(RegisterClass @class)
			{
				this.RegisterClass = @class;
			}

			public RegOrMem OrMem(OperandDataType dataType) => new RegOrMem(this, Mem.WithDataType(dataType));

			public override IntegerSize? ImpliedIntegerOperandSize => RegisterClass.IsSized
				? IntegerSizeEnum.TryFromBytes(RegisterClass.SizeInBytes.Value) : null;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.ModReg
				|| field == OperandField.BaseReg
				|| field == OperandField.NonDestructiveReg;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				byte regCode;
				if (field == OperandField.ModReg)
				{
					regCode = instruction.ModRM.HasValue
						? instruction.ModRM.Value.Reg
						: MainOpcodeByte.GetEmbeddedReg(instruction.MainOpcodeByte);
					if (instruction.NonLegacyPrefixes.ModRegExtension)
						regCode |= 0b1000;
				}
				else if (field == OperandField.BaseReg)
				{
					if (!instruction.ModRM.HasValue
						|| instruction.ModRM.Value.IsMemoryRM)
					{
						throw new InvalidOperationException("Instruction does not encode a mod base register.");
					}

					regCode = instruction.ModRM.Value.RM;
					if (instruction.NonLegacyPrefixes.BaseRegExtension)
						regCode |= 0b1000;
				}
				else if (field == OperandField.NonDestructiveReg)
				{
					if (!instruction.NonLegacyPrefixes.NonDestructiveReg.HasValue)
						throw new InvalidOperationException("Instruction does not encode a non-destructive register.");

					regCode = instruction.NonLegacyPrefixes.NonDestructiveReg.Value;
				}
				else
				{
					throw new ArgumentOutOfRangeException(nameof(field));
				}
				
				// Delegate to Gpr class to handle potential high byte register
				if (RegisterClass == RegisterClass.GprByte)
					textWriter.Write(Gpr.Byte((GprCode)regCode, instruction.NonLegacyPrefixes.Form != NonLegacyPrefixesForm.Escapes).Name);
				else
					textWriter.Write(new Register(RegisterClass, regCode).Name);
			}

			public override string ToString() => RegisterClass.Name;

			public static readonly Reg GprUnsized = new Reg(RegisterClass.GprUnsized);
			public static readonly Reg Gpr8 = new Reg(RegisterClass.GprByte);
			public static readonly Reg Gpr16 = new Reg(RegisterClass.GprWord);
			public static readonly Reg Gpr32 = new Reg(RegisterClass.GprDword);
			public static readonly Reg Gpr64 = new Reg(RegisterClass.GprQword);
			public static readonly Reg X87 = new Reg(RegisterClass.X87);
			public static readonly Reg Mmx = new Reg(RegisterClass.Mmx);
			public static readonly Reg Xmm = new Reg(RegisterClass.Xmm);
			public static readonly Reg Ymm = new Reg(RegisterClass.Ymm);
			public static readonly Reg Zmm = new Reg(RegisterClass.Zmm);
			public static readonly Reg AvxOpmask = new Reg(RegisterClass.AvxOpmask);
			public static readonly Reg Bound = new Reg(RegisterClass.Bound);
			public static readonly Reg Segment = new Reg(RegisterClass.Segment);
			public static readonly Reg Debug = new Reg(RegisterClass.DebugUnsized);
			public static readonly Reg Control = new Reg(RegisterClass.ControlUnsized);
		}

		// FDIV m32fp
		public sealed class Mem : OperandSpec
		{
			// Can be null for LEA, which doesn't actually access the address
			public OperandDataType? DataType { get; }
			public int SizeInBytes => DataType.HasValue ? DataType.Value.TotalSizeInBytes : 0;
			public int SizeInBits => SizeInBytes * 8;

			private Mem(OperandDataType? dataType) => this.DataType = dataType;

			public override IntegerSize? ImpliedIntegerOperandSize
				=> DataType.HasValue ? DataType.Value.GetImpliedGprSize() : null;

			public override bool IsValidField(OperandField field) => field == OperandField.BaseReg;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (DataType == OperandDataType.Byte) textWriter.Write("byte ptr ");
				else if (DataType == OperandDataType.Word) textWriter.Write("word ptr ");
				else if (DataType == OperandDataType.Dword) textWriter.Write("dword ptr ");
				else if (DataType == OperandDataType.Qword) textWriter.Write("qword ptr ");
				textWriter.Write(instruction.GetRMEffectiveAddress().ToString(ip, vectorSib: false));
			}

			public override string ToString() 
				=> SizeInBytes == 0 ? "m" : ("m" + SizeInBits.ToString());

			public static Mem WithDataType(OperandDataType dataType)
			{
				if (!dataType.IsVector)
				{
					switch (dataType.ScalarType)
					{
						case ScalarType.Untyped: return Untyped(dataType.TotalSizeInBytes);

						case ScalarType.SignedInt:
							if (dataType.ScalarSizeInBytes == 1) return I8;
							if (dataType.ScalarSizeInBytes == 2) return I16;
							if (dataType.ScalarSizeInBytes == 4) return I32;
							if (dataType.ScalarSizeInBytes == 8) return I64;
							break;

						case ScalarType.Float:
							if (dataType.ScalarSizeInBytes == 2) return F16;
							if (dataType.ScalarSizeInBytes == 4) return F32;
							if (dataType.ScalarSizeInBytes == 8) return F64;
							if (dataType.ScalarSizeInBytes == 10) return F80;
							break;

						case ScalarType.NearPointer:
							if (dataType.ScalarSizeInBytes == 2) return NearPtr16;
							if (dataType.ScalarSizeInBytes == 4) return NearPtr32;
							if (dataType.ScalarSizeInBytes == 8) return NearPtr64;
							break;

						case ScalarType.FarPointer:
							if (dataType.ScalarSizeInBytes == 4) return FarPtr16;
							if (dataType.ScalarSizeInBytes == 6) return FarPtr32;
							if (dataType.ScalarSizeInBytes == 10) return FarPtr64;
							break;
					}
				}

				return new Mem(dataType);
			}

			public static Mem Untyped(int sizeInBytes)
			{
				switch (sizeInBytes)
				{
					case 1: return M8;
					case 2: return M16;
					case 4: return M32;
					case 8: return M64;
					case 10: return M80;
					case 16: return M128;
					case 32: return M256;
					case 64: return M512;
					default: return new Mem(new OperandDataType(ScalarType.Untyped, sizeInBytes));
				}
			}

			public static readonly Mem M = new Mem(null);
			public static readonly Mem M8 = new Mem(OperandDataType.Byte);
			public static readonly Mem M16 = new Mem(OperandDataType.Word);
			public static readonly Mem M32 = new Mem(OperandDataType.Dword);
			public static readonly Mem M64 = new Mem(OperandDataType.Qword);
			public static readonly Mem M80 = new Mem(OperandDataType.Untyped80);
			public static readonly Mem M128 = new Mem(OperandDataType.Untyped128);
			public static readonly Mem M256 = new Mem(OperandDataType.Untyped256);
			public static readonly Mem M512 = new Mem(OperandDataType.Untyped512);

			public static readonly Mem I8 = new Mem(OperandDataType.I8);
			public static readonly Mem I16 = new Mem(OperandDataType.I16);
			public static readonly Mem I32 = new Mem(OperandDataType.I32);
			public static readonly Mem I64 = new Mem(OperandDataType.I64);

			public static readonly Mem F16 = new Mem(OperandDataType.F16);
			public static readonly Mem F32 = new Mem(OperandDataType.F32);
			public static readonly Mem F64 = new Mem(OperandDataType.F64);
			public static readonly Mem F80 = new Mem(OperandDataType.F80);

			public static readonly Mem NearPtr16 = new Mem(OperandDataType.NearPtr16);
			public static readonly Mem NearPtr32 = new Mem(OperandDataType.NearPtr32);
			public static readonly Mem NearPtr64 = new Mem(OperandDataType.NearPtr64);

			public static readonly Mem FarPtr16 = new Mem(OperandDataType.FarPtr16);
			public static readonly Mem FarPtr32 = new Mem(OperandDataType.FarPtr32);
			public static readonly Mem FarPtr64 = new Mem(OperandDataType.FarPtr64);
		}

		// NEG r/m8
		public sealed class RegOrMem : OperandSpec, IWithReg
		{
			public Reg RegSpec { get; }
			public Mem MemSpec { get; }

			public RegOrMem(Reg regSpec, Mem memSpec)
			{
				if (regSpec == null) throw new ArgumentNullException(nameof(regSpec));
				if (memSpec == null) throw new ArgumentNullException(nameof(memSpec));

				var regSize = regSpec.RegisterClass.SizeInBytes.Value;
				var memSize = memSpec.SizeInBytes;
				bool allowSubregister = regSpec.RegisterFamily == RegisterFamily.Sse
					|| regSpec.RegisterFamily == RegisterFamily.AvxOpmask;
				if (allowSubregister ? (regSize < memSize) : (regSize != memSize))
					throw new ArgumentException("RM mem size inconsistent with register class.");
				
				this.RegSpec = regSpec;
				this.MemSpec = memSpec;
			}

			public override IntegerSize? ImpliedIntegerOperandSize => RegSpec.ImpliedIntegerOperandSize;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.BaseReg;

			public OperandSpec GetActualSpec(ModRM modRM)
				=> modRM.IsDirect ? (OperandSpec)RegSpec: (OperandSpec)MemSpec;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (!instruction.ModRM.HasValue) throw new ArgumentOutOfRangeException(nameof(field));

				var actualSpec = GetActualSpec(instruction.ModRM.Value);
				actualSpec.Format(textWriter, instruction, field, ip);
			}

			public override string ToString()
			{
				return (RegSpec.RegisterClass.Family == RegisterFamily.Gpr ? "r" : RegSpec.ToString())
					+ "/" + MemSpec.ToString();
			}

			RegisterClass IWithReg.RegisterClass => RegSpec.RegisterClass;

			public static readonly RegOrMem RM8_Untyped = new RegOrMem(Reg.Gpr8, Mem.M8);
			public static readonly RegOrMem RM16_Untyped = new RegOrMem(Reg.Gpr16, Mem.M16);
			public static readonly RegOrMem RM32_Untyped = new RegOrMem(Reg.Gpr32, Mem.M32);
			public static readonly RegOrMem RM64_Untyped = new RegOrMem(Reg.Gpr64, Mem.M64);
			public static readonly RegOrMem RM16_NearPtr = new RegOrMem(Reg.Gpr16, Mem.NearPtr16);
			public static readonly RegOrMem RM32_NearPtr = new RegOrMem(Reg.Gpr32, Mem.NearPtr32);
			public static readonly RegOrMem RM64_NearPtr = new RegOrMem(Reg.Gpr64, Mem.NearPtr64);
			public static readonly RegOrMem Mmx_Untyped = new RegOrMem(Reg.Mmx, Mem.M64);
			public static readonly RegOrMem Xmm_Untyped = new RegOrMem(Reg.Xmm, Mem.M128);
			public static readonly RegOrMem Ymm_Untyped = new RegOrMem(Reg.Ymm, Mem.M256);
			public static readonly RegOrMem Zmm_Untyped = new RegOrMem(Reg.Zmm, Mem.M512);
		}

		// Memory operand with vector SIB
		// VGATHERDPD xmm1, vm32x, xmm2
		public sealed class VMem : OperandSpec
		{
			public SseVectorSize IndexRegSize { get; }
			public IntegerSize IndicesSize { get; }

			public VMem(SseVectorSize indexRegSize, IntegerSize indicesSize)
			{
				if (indicesSize != IntegerSize.Dword && indicesSize != IntegerSize.Qword)
					throw new ArgumentOutOfRangeException(nameof(indicesSize));

				this.IndexRegSize = indexRegSize;
				this.IndicesSize = indicesSize;
			}

			public RegisterClass IndexRegClass => IndexRegSize.GetRegisterClass();
			public int MaxIndexCount => IndexRegSize.InBytes() / IndicesSize.InBytes();

			public override IntegerSize? ImpliedIntegerOperandSize => null;

			public override bool IsValidField(OperandField field) => field == OperandField.BaseReg;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				throw new NotImplementedException();
			}

			public override string ToString()
			{
				return "vm" + IndicesSize.InBits().ToString(CultureInfo.InvariantCulture)
					+ IndexRegClass.Name[0];
			}

			public static readonly VMem VM32X = new VMem(SseVectorSize._128, IntegerSize.Dword);
			public static readonly VMem VM32Y = new VMem(SseVectorSize._256, IntegerSize.Dword);
			public static readonly VMem VM32Z = new VMem(SseVectorSize._512, IntegerSize.Dword);
			public static readonly VMem VM64X = new VMem(SseVectorSize._128, IntegerSize.Qword);
			public static readonly VMem VM64Y = new VMem(SseVectorSize._256, IntegerSize.Qword);
			public static readonly VMem VM64Z = new VMem(SseVectorSize._512, IntegerSize.Qword);
		}

		// MOV EAX,moffs32
		public sealed class MOffs : OperandSpec
		{
			public AddressSize AddressSize { get; }
			public OperandDataType DataType { get; }

			public MOffs(AddressSize addressSize, OperandDataType dataType)
			{
				this.AddressSize = addressSize;
				this.DataType = dataType;
			}

			public override IntegerSize? ImpliedIntegerOperandSize => DataType.GetImpliedGprSize();

			public override bool IsValidField(OperandField field) => field == OperandField.Immediate;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (instruction.EffectiveAddressSize != AddressSize)
					throw new InvalidOperationException();
				var segmentBase = instruction.LegacyPrefixes.SegmentOverride ?? SegmentRegister.DS;

				ulong address = instruction.ImmediateData.AsUInt(AddressSize.ToIntegerSize());

				var addressFormat = "x" + (instruction.EffectiveAddressSize.InBytes() * 2);

				textWriter.Write(segmentBase.GetName());
				textWriter.Write(":[0x");
				textWriter.Write(address.ToString(addressFormat, CultureInfo.InvariantCulture));
				textWriter.Write(']');
			}

			public override string ToString() => "moffs" + DataType.TotalSizeInBits;
		}

		// PUSH imm32 
		public sealed class Imm : OperandSpec
		{
			public OperandDataType DataType { get; }

			private Imm(OperandDataType dataType)
			{
				Debug.Assert(!dataType.IsVector);
				this.DataType = dataType;
			}

			public override IntegerSize? ImpliedIntegerOperandSize => DataType.GetImpliedGprSize();

			public override bool IsValidField(OperandField field)
				=> field == OperandField.Immediate || field == OperandField.SecondImmediate;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (DataType.TotalSizeInBytes > instruction.ImmediateSizeInBytes)
					throw new InvalidOperationException("Instruction doesn't have an immediate big enough to encode operand.");
				if (field != OperandField.Immediate && field != OperandField.SecondImmediate)
					throw new ArgumentOutOfRangeException(nameof(field));

				if (DataType.TotalSizeInBytes != instruction.ImmediateSizeInBytes)
					throw new NotImplementedException("Multiple immediates.");

				if ((DataType.ScalarType != ScalarType.Untyped
					&& DataType.ScalarType != ScalarType.SignedInt
					&& DataType.ScalarType != ScalarType.UnsignedInt
					&& DataType.ScalarType != ScalarType.NearPointer)
					|| DataType.IsVector)
					throw new NotImplementedException("Formatting non-integral immediates.");

				var value = instruction.ImmediateData.RawStorage;
				textWriter.Write(value < 0x10
					? value.ToString(CultureInfo.InvariantCulture)
					: "0x" + value.ToString("x", CultureInfo.InvariantCulture));
			}

			public override string ToString() => "imm" + DataType.ScalarSizeInBytes;

			public static Imm WithDataType(OperandDataType dataType)
			{
				if (dataType.IsVector)
					throw new ArgumentException("Immediates cannot be vectored.", nameof(dataType));

				switch (dataType.ScalarType)
				{
					case ScalarType.Untyped:
						if (dataType.ScalarSizeInBytes == 1) return Byte;
						if (dataType.ScalarSizeInBytes == 2) return Word;
						if (dataType.ScalarSizeInBytes == 4) return Dword;
						if (dataType.ScalarSizeInBytes == 8) return Qword;
						break;

					case ScalarType.SignedInt:
						if (dataType.ScalarSizeInBytes == 1) return I8;
						if (dataType.ScalarSizeInBytes == 2) return I16;
						if (dataType.ScalarSizeInBytes == 4) return I32;
						if (dataType.ScalarSizeInBytes == 8) return I64;
						break;

					case ScalarType.UnsignedInt:
						if (dataType.ScalarSizeInBytes == 1) return U8;
						if (dataType.ScalarSizeInBytes == 2) return U16;
						if (dataType.ScalarSizeInBytes == 4) return U32;
						if (dataType.ScalarSizeInBytes == 8) return U64;
						break;

					case ScalarType.NearPointer:
						if (dataType.ScalarSizeInBytes == 2) return NearPtr16;
						if (dataType.ScalarSizeInBytes == 4) return NearPtr32;
						if (dataType.ScalarSizeInBytes == 8) return NearPtr64;
						break;

					case ScalarType.FarPointer:
						if (dataType.ScalarSizeInBytes == 4) return FarPtr16;
						if (dataType.ScalarSizeInBytes == 6) return FarPtr32;
						if (dataType.ScalarSizeInBytes == 10) return FarPtr64;
						break;
				}

				return new Imm(dataType);
			}

			public static readonly Imm Byte = new Imm(OperandDataType.Byte);
			public static readonly Imm Word = new Imm(OperandDataType.Word);
			public static readonly Imm Dword = new Imm(OperandDataType.Dword);
			public static readonly Imm Qword = new Imm(OperandDataType.Qword);

			public static readonly Imm I8 = new Imm(OperandDataType.I8);
			public static readonly Imm I16 = new Imm(OperandDataType.I16);
			public static readonly Imm I32 = new Imm(OperandDataType.I32);
			public static readonly Imm I64 = new Imm(OperandDataType.I64);

			public static readonly Imm U8 = new Imm(OperandDataType.U8);
			public static readonly Imm U16 = new Imm(OperandDataType.U16);
			public static readonly Imm U32 = new Imm(OperandDataType.U32);
			public static readonly Imm U64 = new Imm(OperandDataType.U64);

			public static readonly Imm NearPtr16 = new Imm(OperandDataType.NearPtr16);
			public static readonly Imm NearPtr32 = new Imm(OperandDataType.NearPtr32);
			public static readonly Imm NearPtr64 = new Imm(OperandDataType.NearPtr64);

			public static readonly Imm FarPtr16 = new Imm(OperandDataType.FarPtr16);
			public static readonly Imm FarPtr32 = new Imm(OperandDataType.FarPtr32);
			public static readonly Imm FarPtr64 = new Imm(OperandDataType.FarPtr64);
		}

		// SAL r/m8, 1 
		public sealed class Const : OperandSpec
		{
			public byte Value { get; }

			public Const(byte value) => this.Value = value;

			public override IntegerSize? ImpliedIntegerOperandSize => IntegerSize.Byte;

			public override bool IsValidField(OperandField field) => false;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				textWriter.Write(Value < 0x10
					? Value.ToString(CultureInfo.InvariantCulture)
					: "0x" + Value.ToString("x2", CultureInfo.InvariantCulture));
			}

			public override string ToString() => Value.ToString();

			public static readonly Const Zero = new Const(0);
			public static readonly Const One = new Const(1);
		}

		// JMP rel8
		public sealed class Rel : OperandSpec
		{
			public IntegerSize OffsetSize { get; }

			private Rel(IntegerSize offsetSize) => this.OffsetSize = offsetSize;

			public override IntegerSize? ImpliedIntegerOperandSize => null;

			public override bool IsValidField(OperandField field) => field == OperandField.Immediate;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (OffsetSize.InBytes() != instruction.ImmediateSizeInBytes)
					throw new InvalidOperationException("Instruction immediate size doesn't match operand.");

				long offset = instruction.ImmediateData.AsInt(OffsetSize);
				if (ip.HasValue)
				{
					ulong address = (ulong)((long)ip.Value + offset);
					textWriter.Write("0x");
					var addressFormat = "x" + (instruction.EffectiveAddressSize.InBytes() * 2);
					textWriter.Write(offset.ToString(addressFormat, CultureInfo.InvariantCulture));
				}
				else
				{
					if (offset >= 0) textWriter.Write('+');
					textWriter.Write(offset.ToString(CultureInfo.InvariantCulture));
				}
			}

			public override string ToString() => "rel" + OffsetSize.InBits();

			public static Rel WithOffsetSize(IntegerSize offsetSize)
			{
				if (offsetSize == IntegerSize.Byte) return Short;
				if (offsetSize == IntegerSize.Word) return Long16;
				if (offsetSize == IntegerSize.Dword) return Long32;
				throw new ArgumentOutOfRangeException(nameof(offsetSize));
			}

			public static readonly Rel Short = new Rel(IntegerSize.Byte);
			public static readonly Rel Long16 = new Rel(IntegerSize.Word);
			public static readonly Rel Long32 = new Rel(IntegerSize.Dword);
		}
	}
}
