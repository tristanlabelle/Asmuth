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

		#region Emit (full instructions)
		private void Emit(byte opcode) => Write(opcode);

		private void Emit(OpcodeMap map, byte opcode)
		{
			Write(map);
			Write(opcode);
		}

		private void Emit(OpcodeMap map, byte opcode8, byte opcode, Gpr reg, EffectiveAddress rm)
		{
			WritePrefixes(reg, rm);
			Emit(map, reg.Size == OperandSize.Byte ? opcode8 : opcode);

			var displacementSize = rm.MinimumDisplacementSize;
			var encoding = rm.Encode(
				context.GetDefaultAddressSize(), reg.Code.GetLow3Bits(), displacementSize);
			Write(encoding.ModRM);
			if (encoding.Sib.HasValue) Write(encoding.Sib.Value);

			switch (displacementSize)
			{
				case DisplacementSize._8: Write((sbyte)encoding.Displacement); break;
				case DisplacementSize._16: Write((short)encoding.Displacement); break;
				case DisplacementSize._32: Write(encoding.Displacement); break;
			}
		}

		private void Emit(byte opcode8, byte opcode, Gpr reg, EffectiveAddress rm)
			=> Emit(OpcodeMap.Default, opcode8, opcode, reg, rm);
		#endregion

		#region Write (instruction components)
		private void Write(byte value) => stream.WriteByte(value);

		private void Write(LegacyPrefix legacyPrefix) => Write(legacyPrefix.GetEncodingByte());

		private void Write(OpcodeMap map)
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

		private void Write(Rex rex) => Write((byte)((rex & ~Rex.Reserved_Mask) | Rex.Reserved_Value));
		private void Write(ModRM modRM) => Write((byte)modRM);
		private void Write(Sib sib) => Write((byte)sib);
		private void Write(sbyte value) => Write(unchecked((byte)value));

		private void Write(short value)
		{
			throw new NotImplementedException();
		}

		private void Write(int value)
		{
			throw new NotImplementedException();
		}

		private void WritePrefixes(Gpr reg, EffectiveAddress effectiveAddress)
		{
			if (effectiveAddress.RequiresSegmentOverride)
				Write(LegacyPrefixEnum.GetSegmentOverride(effectiveAddress.Segment));

			var effectiveAddressSize = effectiveAddress.AddressSize;
			if (effectiveAddressSize != context.GetDefaultAddressSize())
			{
				if (effectiveAddressSize != context.GetEffectiveAddressSize(@override: true))
					throw new InvalidDataException();
				Write(LegacyPrefix.AddressSizeOverride);
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
			if (rex.HasValue) Write(rex.Value);
		} 
		#endregion
	}
}
