using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Encoding.Xed
{
	partial class StaticXedInstructionConverter
	{
		private static IEnumerable<OperandDefinition> GetOperandDefinitions(
			IEnumerable<XedOperand> xedOperands)
		{
			var enumerator = xedOperands.GetEnumerator();
			bool hasCurrent = enumerator.MoveNext();
			while (hasCurrent)
			{
				var operand = enumerator.Current;
				hasCurrent = enumerator.MoveNext();

				var operandKind = operand.Kind;

				OperandSpec spec = null;
				if (operandKind == XedOperandKind.Memory)
				{
					if (operand.Visibility == XedOperandVisibility.Suppressed)
					{
						spec = GetSuppressedMemoryOperandSpec(operand, enumerator, ref hasCurrent);
					}
					else spec = GetMemOperandSpec(operand);
				}
				else if (operandKind == XedOperandKind.Register) spec = GetRegOperandSpec(operand);
				else if (operandKind == XedOperandKind.Immediate) spec = GetImmOperandSpec(operand);
				else if (operandKind == XedOperandKind.RelativeBranch) spec = GetRelOperandSpec(operand);
				else if (operandKind == XedOperandKind.AddressGeneration) spec = OperandSpec.Mem.M;
				else if (operand.Visibility == XedOperandVisibility.Explicit)
					throw new NotImplementedException();

				if (spec != null)
				{
					yield return new OperandDefinition(spec, null, AccessType.None);
				}
			}
		}

		private static OperandSpec GetSuppressedMemoryOperandSpec(XedOperand memory,
			IEnumerator<XedOperand> enumerator, ref bool hasCurrent)
		{
			OperandSpec spec;
			XedOperand @base = null, segment = null, index = null, scale = null;
			while (hasCurrent)
			{
				var nextOperand = enumerator.Current;
				if (nextOperand.IndexInKind != memory.IndexInKind) break;

				if (nextOperand.Kind == XedOperandKind.MemoryBase)
				{
					if (@base != null) throw new FormatException();
					@base = enumerator.Current;
				}
				else if (nextOperand.Kind == XedOperandKind.MemorySegment)
				{
					if (segment != null) throw new FormatException();
					segment = enumerator.Current;
				}
				else if (nextOperand.Kind == XedOperandKind.MemoryIndex)
				{
					if (index != null) throw new FormatException();
					index = enumerator.Current;
				}
				else if (nextOperand.Kind == XedOperandKind.MemoryScale)
				{
					if (scale != null) throw new FormatException();
					scale = enumerator.Current;
				}
				else break;

				hasCurrent = enumerator.MoveNext();
			}

			if (@base == null) throw new FormatException();

			spec = GetSuppressedMemOperandSpec(memory, @base, segment, index);
			return spec;
		}

		private static OperandSpec GetMemOperandSpec(XedOperand operand)
		{
			var dataType = GetDataType(operand.Width);
			return dataType.HasValue ? OperandSpec.Mem.WithDataType(dataType.Value) : OperandSpec.Mem.M;
		}

		private static OperandSpec GetSuppressedMemOperandSpec(XedOperand operand,
			XedOperand baseOperand, XedOperand segmentOperand, XedOperand indexOperand)
		{
			return null; // Not implemented
		}

		private static OperandSpec GetRegOperandSpec(XedOperand operand)
		{
			if (!operand.Value.HasValue) throw new FormatException();
			if (!operand.Width.HasValue) return null; // Probably a pseudo register

			var dataType = GetDataType(operand.Width.Value).Value;

			var registerBlotValue = operand.Value.Value;
			if (registerBlotValue.Kind == XedBlotValueKind.Constant)
			{
				var fieldType = (XedRegisterFieldType)operand.Field.Type;
				var register = GetRegister(fieldType.RegisterTable.ByIndex[operand.Value.Value.Constant - 1]);
				if (!register.HasValue) return null;
				
				return new OperandSpec.FixedReg(register.Value, dataType);
			}

			if (registerBlotValue.Kind != XedBlotValueKind.CallResult)
				throw new FormatException();
			
			var callee = registerBlotValue.Callee;

			// Handle specific registers
			Register? fixedRegister = null;
			if (callee == "OrAX") fixedRegister = Register.A;
			else if (callee == "OrDX") fixedRegister = Register.D;

			if (fixedRegister.HasValue)
				return new OperandSpec.FixedReg(fixedRegister.Value, dataType);

			// Handle register classes
			RegisterClass registerClass;
			if (callee.StartsWith("GPR8")) registerClass = RegisterClass.GprByte;
			else if (callee.StartsWith("GPRv")) registerClass = RegisterClass.GprUnsized;
			else if (callee.StartsWith("MMX")) registerClass = RegisterClass.Mmx;
			else if (callee.StartsWith("X87")) registerClass = RegisterClass.X87;
			else if (callee.StartsWith("XMM")) registerClass = RegisterClass.Xmm;
			else if (callee.StartsWith("YMM")) registerClass = RegisterClass.Ymm;
			else if (callee.StartsWith("ZMM")) registerClass = RegisterClass.Zmm;
			else throw new NotImplementedException();

			return new OperandSpec.Reg(registerClass, dataType);
		}

		private static Register? GetRegister(XedRegister xedRegister)
		{
			if (xedRegister.IsHighByte) throw new NotImplementedException();
			var @class = GetRegisterClass(xedRegister.Class, xedRegister.WidthInBits_X64);
			if (!@class.HasValue) return null;
			return new Register(@class.Value, xedRegister.IndexInClass.GetValueOrDefault());
		}

		private static RegisterClass? GetRegisterClass(string name, int widthInBits)
		{
			switch (name)
			{
				case "gpr":
					if (widthInBits == 8) return RegisterClass.GprByte;
					if (widthInBits == 16) return RegisterClass.GprWord;
					if (widthInBits == 32) return RegisterClass.GprDword;
					if (widthInBits == 64) return RegisterClass.GprQword;
					throw new InvalidOperationException();

				case "x87": return RegisterClass.X87;
				case "pseudo": return null;
				case "pseudox87": return null;
				default: throw new NotImplementedException();
			}
		}

		private static OperandSpec GetImmOperandSpec(XedOperand operand)
		{
			if (!operand.Width.TryGetValue(out var width)) throw new FormatException();
			return OperandSpec.Imm.WithDataType(GetDataType(operand.Width).Value);
		}
		
		private static OperandSpec GetRelOperandSpec(XedOperand operand)
		{
			if (!operand.Width.TryGetValue(out var width)) throw new FormatException();
			if (width.BaseType != XedBaseType.Int) throw new FormatException();
			if (width.BitsPerElement == 0)
			{
				if (width.InBits_16 == 16 && width.InBits_32 == 32 && width.InBits_64 == 32)
					return OperandSpec.Rel.Long16Or32;
			}
			else
			{
				if (width.InBits != width.BitsPerElement) throw new FormatException();
				if (width.BitsPerElement == 8) return OperandSpec.Rel.Short;
				if (width.BitsPerElement == 16) return OperandSpec.Rel.Long16;
				if (width.BitsPerElement == 32) return OperandSpec.Rel.Long32;
			}

			throw new FormatException();
		}

		private static OperandDataType? GetDataType(XedOperandWidth? width)
			=> width.HasValue ? GetDataType(width.Value) : null;

		private static OperandDataType? GetDataType(XedOperandWidth width)
		{
			if ((width.BitsPerElement & 0x7) != 0) throw new NotImplementedException(); // Can't represent bitfields
			var bytesPerElement = width.BitsPerElement / 8;

			var (scalarType, scalarSizeInBytes) = GetScalarTypeAndSizeInBytes(width.BaseType);
			if (scalarSizeInBytes.HasValue && bytesPerElement != scalarSizeInBytes.Value)
				throw new ArgumentException();

			if (width.InBits.TryGetValue(out int widthInBits))
			{
				var widthInBytes = widthInBits / 8;

				// Scalar case
				if (bytesPerElement == 0 || widthInBytes == bytesPerElement)
				{
					if (scalarType.IsInt() && bytesPerElement > 8) scalarType = ScalarType.Untyped;
					return new OperandDataType(scalarType, widthInBytes);
				}

				// Vector case
				var vectorLength = widthInBits / width.BitsPerElement;
				return OperandDataType.FromVector(scalarType, bytesPerElement, vectorLength);
			}
			else
			{
				if (width.BitsPerElement != 0) throw new FormatException();
				if (width.InBits_16 % 8 != 0 || width.InBits_32 % 8 != 0 || width.InBits_64 % 8 != 0)
					throw new InvalidOperationException();
				if (scalarType.IsInt() && (width.InBits_16 > 64 || width.InBits_32 > 64 || width.InBits_64 > 64))
					scalarType = ScalarType.Untyped;
				return OperandDataType.FromVariableSizeInBytes(scalarType,
					width.InBits_16 / 8, width.InBits_32 / 8, width.InBits_64 / 8);
			}
		}

		private static (ScalarType, byte?) GetScalarTypeAndSizeInBytes(XedBaseType baseType)
		{
			switch (baseType)
			{
				case XedBaseType.Struct: return (ScalarType.Untyped, null);
				case XedBaseType.UInt: return (ScalarType.UnsignedInt, null);
				case XedBaseType.Int: return (ScalarType.SignedInt, null);
				case XedBaseType.Float16: return (ScalarType.Ieee754Float, 2);
				case XedBaseType.Single: return (ScalarType.Ieee754Float, 4);
				case XedBaseType.Double: return (ScalarType.Ieee754Float, 8);
				case XedBaseType.LongDouble: return (ScalarType.X87Float80, 10);
				case XedBaseType.LongBCD: return (ScalarType.LongPackedBcd, 10);
				default: throw new NotImplementedException();
			}
		}
	}
}
