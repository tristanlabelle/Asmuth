using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public sealed class CodeWriter
	{
		private readonly Stream stream;
		private readonly CodeSegmentType codeSegmentType;

		public CodeWriter(Stream stream, CodeSegmentType codeSegmentType)
		{
			if (stream == null) throw new ArgumentNullException(nameof(stream));
			if (!stream.CanWrite) throw new ArgumentException("Target stream must be writable.", nameof(stream));
			this.stream = stream;
			this.codeSegmentType = codeSegmentType;
		}

		public CodeSegmentType CodeSegmentType => codeSegmentType;

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
			Emit(map, reg.Size == IntegerSize.Byte ? opcode8 : opcode);

			var displacementSize = rm.MinimumDisplacementSize;
			var encoding = rm.Encode(codeSegmentType, reg.Code.GetLow3Bits(), displacementSize);
			Write(encoding.ModRM);
			if (encoding.Sib.HasValue) Write(encoding.Sib.Value);

			switch (displacementSize)
			{
				case DisplacementSize.SByte: Write((sbyte)encoding.Displacement); break;
				case DisplacementSize.SWord: Write((short)encoding.Displacement); break;
				case DisplacementSize.SDword: Write(encoding.Displacement); break;
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
			if (effectiveAddressSize != codeSegmentType.GetDefaultAddressSize())
			{
				if (effectiveAddressSize != codeSegmentType.GetEffectiveAddressSize(@override: true))
					throw new InvalidDataException();
				Write(LegacyPrefix.AddressSizeOverride);
			}

			// Rex
			var rexBuilder = new Rex.Builder();
			if (reg.Size == IntegerSize.Qword)
			{
				rexBuilder.OperandSizePromotion = true;
			}
			else if (reg.Size == IntegerSize.Byte)
			{
				// Might require rex for low byte
				throw new NotImplementedException();
			}

			rexBuilder.ModRegExtension = (reg.Code >= GprCode.R8);
			rexBuilder.BaseRegExtension = effectiveAddress.BaseAsGprCode >= GprCode.R8;
			rexBuilder.IndexRegExtension = effectiveAddress.IndexAsGprCode >= GprCode.R8;

			var rex = rexBuilder.Build();
			if (rex != default)
			{
				if (!codeSegmentType.IsLongMode()) throw new ArgumentException();
				Write(rex);
			}
		} 
		#endregion
	}
}
