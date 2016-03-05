using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed class CodeWriter
	{
		private readonly Stream stream;
		private readonly CodeContext context;

		public CodeWriter(Stream stream, CodeContext context)
		{
			Contract.Requires(stream != null && stream.CanWrite);
			this.stream = stream;
			this.context = context;
		}

		public void Mov(EffectiveAddress dest, Gpr src) => Emit(0x88, 0x89, src, dest);
		public void Mov(Gpr dest, EffectiveAddress src) => Emit(0x8A, 0x8B, dest, src);
		public void Cpuid() => Emit(OpcodeMap.Escape0F, 0xA2);
		public void Nop() => Emit(0x90);
		public void Ret() => Emit(0xC3);

		private void Emit(byte @byte) => stream.WriteByte(@byte);
		private void Emit(LegacyPrefix legacyPrefix) => Emit(legacyPrefix.GetEncodingByte());
		private void Emit(OpcodeMap map)
		{
			switch (map)
			{
				case OpcodeMap.Default: break;
				case OpcodeMap.Escape0F: stream.WriteByte(0x0F); break;
				case OpcodeMap.Escape0F38: stream.WriteByte(0x0F); stream.WriteByte(0x38); break;
				case OpcodeMap.Escape0F3A: stream.WriteByte(0x0F); stream.WriteByte(0x3A); break;
				default: throw new ArgumentOutOfRangeException(nameof(map));
			}
		}

		private void Emit(Rex rex) => Emit((byte)((rex & ~Rex.Reserved_Mask) | Rex.Reserved_Value));
		private void Emit(ModRM modRM) => Emit((byte)modRM);
		private void Emit(Sib sib) => Emit((byte)sib);
		private void Emit(sbyte value) => Emit(unchecked((byte)value));
		private void Emit(short value)
		{
			throw new NotImplementedException();
		}

		private void Emit(int value)
		{
			throw new NotImplementedException();
		}

		private void Emit(OpcodeMap map, byte opcode)
		{
			Emit(map);
			Emit(opcode);
		}

		private void EmitPrefixes(Gpr reg, EffectiveAddress effectiveAddress)
		{
			if (effectiveAddress.RequiresSegmentOverride)
				Emit(LegacyPrefixEnum.GetSegmentOverride(effectiveAddress.Segment));

			var effectiveAddressSize = effectiveAddress.AddressSize;
			if (effectiveAddressSize != context.GetDefaultAddressSize())
			{
				if (effectiveAddressSize != context.GetEffectiveAddressSize(@override: true))
					throw new InvalidDataException();
				Emit(LegacyPrefix.AddressSizeOverride);
			}

			// Rex
			Rex? rex = null;
			if (reg.Part == GprPart.Qword)
			{
				if (context != CodeContext.SixtyFourBit) throw new ArgumentException("reg");
				rex = rex.GetValueOrDefault() | Rex.OperandSize64;
			}
			else if (reg.Size == OperandSize.Byte)
			{
				// Might require rex for low byte
				throw new NotImplementedException();
			}

			if (reg.Code >= GprCode.R8) rex = rex.GetValueOrDefault() | Rex.ModRegExtension;
			if (effectiveAddress.BaseAsGprCode >= GprCode.R8) rex = rex.GetValueOrDefault() | Rex.BaseRegExtension;
			if (effectiveAddress.IndexAsGprCode >= GprCode.R8) rex = rex.GetValueOrDefault() | Rex.IndexRegExtension;
			if (rex.HasValue) Emit(rex.Value);
		}

		private void Emit(OpcodeMap map, byte opcode8, byte opcode, Gpr reg, EffectiveAddress rm)
		{
			EmitPrefixes(reg, rm);
			Emit(map, reg.Size == OperandSize.Byte ? opcode8 : opcode);

			var displacementSize = rm.MinimumDisplacementSize;
			var encoding = rm.Encode(
				context.GetDefaultAddressSize(), reg.Code.GetLow3Bits(), displacementSize);
			Emit(encoding.ModRM);
			if (encoding.Sib.HasValue) Emit(encoding.Sib.Value);
			if (displacementSize.HasValue)
			{
				switch (displacementSize.Value)
				{
					case OperandSize.Byte: Emit((sbyte)encoding.Displacement); break;
					case OperandSize.Word: Emit((short)encoding.Displacement); break;
					case OperandSize.Dword: Emit(encoding.Displacement); break;
					default: throw new UnreachableException();
				}
			}
		}

		private void Emit(byte opcode8, byte opcode, Gpr reg, EffectiveAddress rm)
			=> Emit(OpcodeMap.Default, opcode8, opcode, reg, rm);
	}
}
