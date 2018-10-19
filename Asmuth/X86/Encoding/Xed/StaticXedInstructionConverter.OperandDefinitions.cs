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
			throw new NotImplementedException();
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
				return new OperandSpec.FixedReg(register);
			}
			else if (registerBlotValue.Kind == XedBlotValueKind.CallResult)
			{
				var callee = registerBlotValue.Callee;
				if (callee.Contains("XMM")) return OperandSpec.Reg.Xmm;
				if (callee.Contains("YMM")) return OperandSpec.Reg.Ymm;
				if (callee.Contains("ZMM")) return OperandSpec.Reg.Zmm;
			}

			throw new NotImplementedException();
		}

		private static Register GetRegister(XedRegister xedRegister)
		{
			if (xedRegister.IsHighByte) throw new NotImplementedException();
			var @class = GetRegisterClass(xedRegister.Class, xedRegister.WidthInBits_X64);
			return new Register(@class, xedRegister.IndexInClass.GetValueOrDefault());
		}

		private static RegisterClass GetRegisterClass(string name, int width)
		{
			if (name == "x87") return RegisterClass.X87;
			throw new NotImplementedException();
		}

		private static OperandSpec GetImmOperandSpec(XedOperand operand)
		{
			if (!operand.Width.TryGetValue(out var width)) throw new FormatException();

			throw new NotImplementedException();
		}
	}
}
