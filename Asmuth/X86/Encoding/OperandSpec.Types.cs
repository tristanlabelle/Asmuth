using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Asmuth.X86.Encoding
{
	partial class OperandSpec
	{
		// PUSH CS
		public sealed class FixedReg : OperandSpec, IWithReg
		{
			public Register Register { get; }
			public RegisterClass RegisterClass => Register.Class;
			public OperandDataType DataType { get; }

			public FixedReg(Register register, OperandDataType dataType)
			{
				if (dataType.TotalSizeInBytes > register.SizeInBytes)
					throw new ArgumentException("Incompatible register data type.");
				this.Register = register;
				this.DataType = dataType;
			}

			public FixedReg(RegisterClass @class, byte index, OperandDataType dataType)
				: this(new Register(@class, index), dataType) { }
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Register;

			public override bool IsValidField(OperandField field) => false;

			public override OperandDataType? TryGetDataType(AddressSize addressSize, IntegerSize operandSize)
				=> DataType;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
				=> textWriter.Write(Register.Name);

			public override string ToString() => Register.Name;

			public static readonly FixedReg A_Untyped = new FixedReg(Register.A, OperandDataType.WordOrDwordOrQword);
			public static readonly FixedReg D_Untyped = new FixedReg(Register.D, OperandDataType.WordOrDwordOrQword);
			public static readonly FixedReg AL_Untyped = new FixedReg(Register.AL, OperandDataType.Byte);
			public static readonly FixedReg CL_Untyped = new FixedReg(Register.CL, OperandDataType.Byte);
			public static readonly FixedReg AX_Untyped = new FixedReg(Register.AX, OperandDataType.Word);
			public static readonly FixedReg CX_Untyped = new FixedReg(Register.CX, OperandDataType.Word);
			public static readonly FixedReg DX_Untyped = new FixedReg(Register.DX, OperandDataType.Word);
			public static readonly FixedReg Eax_Untyped = new FixedReg(Register.Eax, OperandDataType.Dword);
			public static readonly FixedReg Ecx_Untyped = new FixedReg(Register.Ecx, OperandDataType.Dword);
			public static readonly FixedReg Edx_Untyped = new FixedReg(Register.Edx, OperandDataType.Dword);
			public static readonly FixedReg Rax_Untyped = new FixedReg(Register.Rax, OperandDataType.Dword);
			public static readonly FixedReg Rcx_Untyped = new FixedReg(Register.Rcx, OperandDataType.Dword);

			public static readonly FixedReg ST0_F80 = new FixedReg(Register.ST0, OperandDataType.F80);
			public static readonly FixedReg Xmm0_Untyped = new FixedReg(Register.Xmm0, OperandDataType.Untyped128);

			public static readonly FixedReg ES = new FixedReg(Register.ES, OperandDataType.U16);
			public static readonly FixedReg CS = new FixedReg(Register.CS, OperandDataType.U16);
			public static readonly FixedReg SS = new FixedReg(Register.SS, OperandDataType.U16);
			public static readonly FixedReg DS = new FixedReg(Register.DS, OperandDataType.U16);
			public static readonly FixedReg FS = new FixedReg(Register.FS, OperandDataType.U16);
			public static readonly FixedReg GS = new FixedReg(Register.GS, OperandDataType.U16);
		}

		// PUSH r32
		public sealed class Reg : OperandSpec, IWithReg
		{
			public RegisterClass RegisterClass { get; }
			public RegisterFamily RegisterFamily => RegisterClass.Family;
			public OperandDataType DataType { get; }

			public Reg(RegisterClass @class, OperandDataType dataType)
			{
				if (dataType.TotalSizeInBytes > @class.SizeInBytes)
					throw new ArgumentException("Incompatible register data type.");
				this.RegisterClass = @class;
				this.DataType = dataType;
			}

			public RegOrMem OrMem() => new RegOrMem(RegisterClass, DataType);
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Register;

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

			public override string ToString() => $"{RegisterClass.Name}:{DataType}";

			public static readonly Reg Gpr16Or32Or64_Untyped = new Reg(RegisterClass.GprUnsized, OperandDataType.WordOrDwordOrQword);
			public static readonly Reg Gpr8_Untyped = new Reg(RegisterClass.GprByte, OperandDataType.Byte);
			public static readonly Reg Gpr16_Untyped = new Reg(RegisterClass.GprWord, OperandDataType.Word);
			public static readonly Reg Gpr32_Untyped = new Reg(RegisterClass.GprDword, OperandDataType.Dword);
			public static readonly Reg Gpr64_Untyped = new Reg(RegisterClass.GprQword, OperandDataType.Qword);
			public static readonly Reg X87 = new Reg(RegisterClass.X87, OperandDataType.F80);
			public static readonly Reg Mmx_Untyped = new Reg(RegisterClass.Mmx, OperandDataType.Qword);
			public static readonly Reg Xmm_Untyped = new Reg(RegisterClass.Xmm, OperandDataType.Untyped128);
			public static readonly Reg Ymm_Untyped = new Reg(RegisterClass.Ymm, OperandDataType.Untyped256);
			public static readonly Reg Zmm_Untyped = new Reg(RegisterClass.Zmm, OperandDataType.Untyped512);
			public static readonly Reg AvxOpmask_Untyped = new Reg(RegisterClass.AvxOpmask, OperandDataType.Byte); // FIXME: Incorrect data type
			public static readonly Reg Bound_Untyped = new Reg(RegisterClass.Bound, OperandDataType.Byte); // FIXME: Incorrect data type
			public static readonly Reg Segment = new Reg(RegisterClass.Segment, OperandDataType.U16);
			public static readonly Reg Debug = new Reg(RegisterClass.DebugUnsized, OperandDataType.DwordOrQword);
			public static readonly Reg Control = new Reg(RegisterClass.ControlUnsized, OperandDataType.DwordOrQword);
		}

		// FDIV m32fp
		public sealed class Mem : OperandSpec
		{
			// Can be null for LEA, which doesn't actually access the address
			public OperandDataType? DataType { get; }

			private Mem(OperandDataType? dataType) => this.DataType = dataType;
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Memory;

			public override bool IsValidField(OperandField field) => field == OperandField.BaseReg;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (DataType == OperandDataType.Byte) textWriter.Write("byte ptr ");
				else if (DataType == OperandDataType.Word) textWriter.Write("word ptr ");
				else if (DataType == OperandDataType.Dword) textWriter.Write("dword ptr ");
				else if (DataType == OperandDataType.Qword) textWriter.Write("qword ptr ");
				textWriter.Write(instruction.GetRMEffectiveAddress().ToString(ip, vectorSib: false));
			}

			public override string ToString() => DataType.HasValue ? "m:" + DataType.Value.ToString() : "m";

			public static Mem WithDataType(OperandDataType dataType)
			{
				if (!dataType.IsVector && !dataType.HasDependentScalarSize)
				{
					var sizeInBytes = dataType.ScalarSizeInBytes.Value;
					switch (dataType.ScalarType)
					{
						case ScalarType.Untyped:
							return Untyped(sizeInBytes);

						case ScalarType.SignedInt:
							if (sizeInBytes == 1) return I8;
							if (sizeInBytes == 2) return I16;
							if (sizeInBytes == 4) return I32;
							if (sizeInBytes == 8) return I64;
							break;

						case ScalarType.Ieee754Float:
							if (sizeInBytes == 2) return F16;
							if (sizeInBytes == 4) return F32;
							if (sizeInBytes == 8) return F64;
							break;

						case ScalarType.X87Float80:
							if (sizeInBytes == 10) return F80;
							break;

						case ScalarType.NearPointer:
							if (sizeInBytes == 2) return NearPtr16;
							if (sizeInBytes == 4) return NearPtr32;
							if (sizeInBytes == 8) return NearPtr64;
							break;

						case ScalarType.FarPointer:
							if (sizeInBytes == 4) return FarPtr16;
							if (sizeInBytes == 6) return FarPtr32;
							if (sizeInBytes == 10) return FarPtr64;
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
			public RegisterClass RegisterClass { get; }
			public RegisterFamily RegisterFamily => RegisterClass.Family;
			public OperandDataType DataType { get; }

			public RegOrMem(RegisterClass registerClass, OperandDataType dataType)
			{
				if (dataType.HasDependentScalarSize == registerClass.IsSized)
					throw new ArgumentException();

				if (dataType.HasDependentScalarSize)
				{
					throw new NotImplementedException();
				}
				else
				{
					if (dataType.TotalSizeInBytes > registerClass.SizeInBytes)
						throw new ArgumentException();
				}

				this.RegisterClass = registerClass;
				this.DataType = dataType;
			}

			public OperandSpec GetSpec(bool isRegister) => isRegister
				? (OperandSpec)new Reg(RegisterClass, DataType)
				: (OperandSpec)Mem.WithDataType(DataType);
			
			public override OperandDataLocation DataLocation => OperandDataLocation.RegisterOrMemory;

			public override bool IsValidField(OperandField field)
				=> field == OperandField.BaseReg;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				if (!instruction.ModRM.HasValue) throw new ArgumentOutOfRangeException(nameof(field));

				var actualSpec = GetSpec(isRegister: instruction.ModRM.Value.IsDirect);
				actualSpec.Format(textWriter, instruction, field, ip);
			}

			public override string ToString()
			{
				return (RegisterClass.Family == RegisterFamily.Gpr ? "r" : GetSpec(isRegister: true).ToString())
					+ "/" + GetSpec(isRegister: false).ToString();
			}

			RegisterClass IWithReg.RegisterClass => RegisterClass;

			public static readonly RegOrMem RM8_Untyped = new RegOrMem(RegisterClass.GprByte, OperandDataType.Byte);
			public static readonly RegOrMem RM16_Untyped = new RegOrMem(RegisterClass.GprWord, OperandDataType.Word);
			public static readonly RegOrMem RM32_Untyped = new RegOrMem(RegisterClass.GprDword, OperandDataType.Dword);
			public static readonly RegOrMem RM64_Untyped = new RegOrMem(RegisterClass.GprQword, OperandDataType.Qword);
			public static readonly RegOrMem RM16_NearPtr = new RegOrMem(RegisterClass.GprWord, OperandDataType.NearPtr16);
			public static readonly RegOrMem RM32_NearPtr = new RegOrMem(RegisterClass.GprDword, OperandDataType.NearPtr32);
			public static readonly RegOrMem RM64_NearPtr = new RegOrMem(RegisterClass.GprQword, OperandDataType.NearPtr64);
			public static readonly RegOrMem Mmx_Untyped = new RegOrMem(RegisterClass.Mmx, OperandDataType.Qword);
			public static readonly RegOrMem Xmm_Untyped = new RegOrMem(RegisterClass.Xmm, OperandDataType.Untyped128);
			public static readonly RegOrMem Ymm_Untyped = new RegOrMem(RegisterClass.Ymm, OperandDataType.Untyped256);
			public static readonly RegOrMem Zmm_Untyped = new RegOrMem(RegisterClass.Zmm, OperandDataType.Untyped512);
		}

		// Memory operand with vector SIB
		// VGATHERDPD xmm1, vm32x, xmm2
		public sealed class VMem : OperandSpec
		{
			public AvxVectorSize IndexRegSize { get; }
			public IntegerSize IndicesSize { get; }

			public VMem(AvxVectorSize indexRegSize, IntegerSize indicesSize)
			{
				if (indicesSize != IntegerSize.Dword && indicesSize != IntegerSize.Qword)
					throw new ArgumentOutOfRangeException(nameof(indicesSize));

				this.IndexRegSize = indexRegSize;
				this.IndicesSize = indicesSize;
			}

			public RegisterClass IndexRegClass => IndexRegSize.GetRegisterClass();
			public int MaxIndexCount => IndexRegSize.InBytes() / IndicesSize.InBytes();
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Memory;

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

			public static readonly VMem VM32X = new VMem(AvxVectorSize._128, IntegerSize.Dword);
			public static readonly VMem VM32Y = new VMem(AvxVectorSize._256, IntegerSize.Dword);
			public static readonly VMem VM32Z = new VMem(AvxVectorSize._512, IntegerSize.Dword);
			public static readonly VMem VM64X = new VMem(AvxVectorSize._128, IntegerSize.Qword);
			public static readonly VMem VM64Y = new VMem(AvxVectorSize._256, IntegerSize.Qword);
			public static readonly VMem VM64Z = new VMem(AvxVectorSize._512, IntegerSize.Qword);
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
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Memory;

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
			
			public override OperandDataLocation DataLocation => OperandDataLocation.InstructionStream;

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

			public override string ToString() => "imm:" + DataType.ToString();

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
			
			public override OperandDataLocation DataLocation => OperandDataLocation.Constant;

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
			public BranchOffsetSize OffsetSize { get; }

			private Rel(BranchOffsetSize offsetSize) => this.OffsetSize = offsetSize;
			
			public override OperandDataLocation DataLocation => OperandDataLocation.InstructionStream;

			public override bool IsValidField(OperandField field) => field == OperandField.Immediate;

			public override void Format(TextWriter textWriter, in Instruction instruction, OperandField? field, ulong? ip)
			{
				var integerSize = OffsetSize.ToIntegerSize(instruction.Prefixes.IntegerOperandSize);
				if (integerSize.InBytes() != instruction.ImmediateSizeInBytes)
					throw new InvalidOperationException("Instruction immediate size doesn't match operand.");

				long offset = instruction.ImmediateData.AsInt(integerSize);
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

			public override string ToString()
			{
				switch (OffsetSize)
				{
					case BranchOffsetSize.Short: return "rel8";
					case BranchOffsetSize.Long16: return "rel16";
					case BranchOffsetSize.Long32: return "rel32";
					case BranchOffsetSize.Long16Or32: return "rel16/32";
					default: throw new UnreachableException();
				}
			}

			public static Rel WithOffsetSize(BranchOffsetSize offsetSize)
			{
				if (offsetSize == BranchOffsetSize.Short) return Short;
				if (offsetSize == BranchOffsetSize.Long16) return Long16;
				if (offsetSize == BranchOffsetSize.Long32) return Long32;
				if (offsetSize == BranchOffsetSize.Long16Or32) return Long16Or32;
				throw new ArgumentOutOfRangeException(nameof(offsetSize));
			}

			public static Rel WithOffsetSize(IntegerSize offsetSize)
			{
				if (offsetSize == IntegerSize.Byte) return Short;
				if (offsetSize == IntegerSize.Word) return Long16;
				if (offsetSize == IntegerSize.Dword) return Long32;
				throw new ArgumentOutOfRangeException(nameof(offsetSize));
			}

			public static readonly Rel Short = new Rel(BranchOffsetSize.Short);
			public static readonly Rel Long16 = new Rel(BranchOffsetSize.Long16);
			public static readonly Rel Long32 = new Rel(BranchOffsetSize.Long32);
			public static readonly Rel Long16Or32 = new Rel(BranchOffsetSize.Long16Or32);
		}
	}
}
