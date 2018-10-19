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
						spec = GetSuppressedMemoryOperand(operand, enumerator, ref hasCurrent);
					}
					else spec = GetMemOperandSpec(operand);
				}
				else if (operandKind == XedOperandKind.Register) spec = GetRegOperandSpec(operand);
				else if (operandKind == XedOperandKind.Immediate) spec = GetImmOperandSpec(operand);
				else if (operand.Visibility == XedOperandVisibility.Explicit)
					throw new NotImplementedException();

				if (spec != null)
				{
					yield return new OperandDefinition(spec, null, AccessType.None);
				}
			}
		}

		private static OperandSpec GetSuppressedMemoryOperand(XedOperand memory,
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
			var dataType = GetDataType(operand.Width, operand.XType);
			return dataType.HasValue ? OperandSpec.Mem.WithDataType(dataType.Value) : OperandSpec.Mem.M;
		}

		private static OperandSpec GetSuppressedMemOperandSpec(XedOperand operand,
			XedOperand baseOperand, XedOperand segmentOperand, XedOperand indexOperand)
		{
			throw new NotImplementedException();
		}

		private static OperandSpec GetRegOperandSpec(XedOperand operand)
		{
			if (!operand.Value.HasValue) throw new FormatException();
			var registerBlotValue = operand.Value.Value;
			if (registerBlotValue.Kind == XedBlotValueKind.Constant)
			{
				var fieldType = (XedRegisterFieldType)operand.Field.Type;
				var register = GetRegister(fieldType.RegisterTable.ByIndex[operand.Value.Value.Constant - 1]);
				if (!register.HasValue) return null;

				var dataType = new OperandDataType(ScalarType.Untyped, register.Value.SizeInBytes.Value);
				return new OperandSpec.FixedReg(register.Value, dataType);
			}
			else if (registerBlotValue.Kind == XedBlotValueKind.CallResult)
			{
				var callee = registerBlotValue.Callee;
				if (callee.Contains("X87")) return OperandSpec.Reg.X87;
				if (callee.Contains("XMM")) return OperandSpec.Reg.Xmm;
				if (callee.Contains("YMM")) return OperandSpec.Reg.Ymm;
				if (callee.Contains("ZMM")) return OperandSpec.Reg.Zmm;
			}

			throw new NotImplementedException();
		}

		private static Register? GetRegister(XedRegister xedRegister)
		{
			if (xedRegister.IsHighByte) throw new NotImplementedException();
			var @class = GetRegisterClass(xedRegister.Class, xedRegister.WidthInBits_X64);
			if (!@class.HasValue) return null;
			return new Register(@class.Value, xedRegister.IndexInClass.GetValueOrDefault());
		}

		private static RegisterClass? GetRegisterClass(string name, int width)
		{
			if (name == "x87") return RegisterClass.X87;
			if (name == "pseudox87") return null;
			throw new NotImplementedException();
		}

		private static OperandSpec GetImmOperandSpec(XedOperand operand)
		{
			if (!operand.Width.TryGetValue(out var width)) throw new FormatException();

			throw new NotImplementedException();
		}

		private static OperandDataType? GetDataType(XedOperandWidth? width, XedXType? xtype)
			=> width.HasValue ? GetDataType(width.Value, xtype.Value) : null;

		private static OperandDataType? GetDataType(XedOperandWidth width, XedXType xtype)
		{
			if (xtype != width.XType) throw new NotImplementedException(); // Don't know what this means
			if ((width.BitsPerElement & 0x7) != 0) throw new NotImplementedException(); // Can't represent bitfields
			var (scalarType, scalarSizeInBytes) = GetScalarTypeAndSizeInBytes(width.BaseType);
			if (scalarSizeInBytes.HasValue && width.XType.BitsPerElement != scalarSizeInBytes.Value * 8)
				throw new ArgumentException();
			if (width.WidthInBits == width.BitsPerElement)
				return new OperandDataType(scalarType, width.BitsPerElement >> 3);
			throw new NotImplementedException();
		}

		private static (ScalarType, byte?) GetScalarTypeAndSizeInBytes(XedBaseType baseType)
		{
			switch (baseType)
			{
				case XedBaseType.Struct: return (ScalarType.Untyped, null);
				case XedBaseType.UInt: return (ScalarType.UnsignedInt, null);
				case XedBaseType.Int: return (ScalarType.SignedInt, null);
				case XedBaseType.Float16: return (ScalarType.Float, 2);
				case XedBaseType.Single: return (ScalarType.Float, 4);
				case XedBaseType.Double: return (ScalarType.Float, 8);
				case XedBaseType.LongDouble: return (ScalarType.Float, 10);
				default: throw new NotImplementedException();
			}
		}
	}
}
