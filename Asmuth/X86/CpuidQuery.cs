using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	public partial struct CpuidQuery : IEquatable<CpuidQuery>
	{
		#region Fields
		private readonly uint function;
		private readonly byte inputEcx;
		private readonly byte outputRegisterAndHasInputEcxFlag;
		private readonly byte firstBit;
		private readonly byte bitCount;
		#endregion

		#region Constructor
		public CpuidQuery(uint function, byte? inputEcx, GprCode outputGpr, uint mask)
		{
			Contract.Requires(outputGpr >= 0 && (int)outputGpr < 4);
			Contract.Requires(Bits.IsContiguous(mask));

			this.function = function;
			this.inputEcx = inputEcx.GetValueOrDefault();
			this.outputRegisterAndHasInputEcxFlag = (byte)outputGpr;
			if (inputEcx.HasValue) this.outputRegisterAndHasInputEcxFlag |= 0x80;
			throw new NotImplementedException();
		}

		public CpuidQuery(uint function, GprCode outputGpr, uint mask)
		{
			Contract.Requires(outputGpr >= 0 && (int)outputGpr < 4);
			Contract.Requires(Bits.IsContiguous(mask));

			this.function = function;
			this.inputEcx = 0;
			this.outputRegisterAndHasInputEcxFlag = (byte)outputGpr;
			throw new NotImplementedException();
		}

		public CpuidQuery(uint function, GprCode outputGpr)
		{
			Contract.Requires(outputGpr >= 0 && (int)outputGpr < 4);

			this.function = function;
			this.inputEcx = 0;
			this.outputRegisterAndHasInputEcxFlag = (byte)outputGpr;
			this.firstBit = 0;
			this.bitCount = 32;
		}

		public static CpuidQuery FromBit(uint function, GprCode outputGpr, int bitIndex)
		{
			Contract.Requires(bitIndex >= 0 && bitIndex < 32);
			return new CpuidQuery(function, outputGpr, 1U << bitIndex);
		}
		#endregion

		#region Properties
		public uint Function => function;
		public bool IsExtended => (function & 0x80000000) != 0;
		public byte? InputEcx => HasInputEcx ? inputEcx : (byte?)null;
		public GprCode OutputGpr => (GprCode)(outputRegisterAndHasInputEcxFlag & 0x7F);
		public bool IsBit => bitCount == 1;
		private bool HasInputEcx => (outputRegisterAndHasInputEcxFlag & 0x80) == 0x80;
		#endregion

		#region Methods
		public bool Equals(CpuidQuery other)
		{
			return function == other.function
				&& inputEcx == other.inputEcx
				&& outputRegisterAndHasInputEcxFlag == other.outputRegisterAndHasInputEcxFlag
				&& firstBit == other.firstBit
				&& bitCount == other.bitCount;
		}

		public override bool Equals(object obj) => obj is CpuidQuery && Equals((CpuidQuery)obj);
		public static bool Equals(CpuidQuery first, CpuidQuery second) => first.Equals(second);
		public static bool operator ==(CpuidQuery lhs, CpuidQuery rhs) => Equals(lhs, rhs);
		public static bool operator !=(CpuidQuery lhs, CpuidQuery rhs) => !Equals(lhs, rhs);

		public override int GetHashCode()
		{
			return unchecked((int)function)
				^ unchecked((int)inputEcx << 24)
				^ ((int)outputRegisterAndHasInputEcxFlag << 16)
				^ ((int)firstBit << 8)
				^ ((int)bitCount << 0);
		}

		public override string ToString()
		{
			// Fn8000_00001_EDX[30:31]
			var str = new StringBuilder(23);

			str.Append("Fn");
			str.AppendFormat(CultureInfo.InvariantCulture, "X4", function >> 16);
			str.Append('_');
			str.AppendFormat(CultureInfo.InvariantCulture, "X4", function & 0xFFFF);
			str.Append('_');

			// E[ABCD]X
			str.Append(Gpr.GetName(OutputGpr, GprPart.Dword));

			if (bitCount > 0 && bitCount < 32)
			{
				str.Append('[');
				if (bitCount == 1)
				{
					str.AppendFormat(CultureInfo.InvariantCulture, "D", firstBit);
				}
				else
				{
					str.AppendFormat(CultureInfo.InvariantCulture, "D", firstBit + bitCount);
					str.Append(':');
					str.AppendFormat(CultureInfo.InvariantCulture, "D", firstBit);
				}
				str.Append(']');
			}

			return str.ToString();
		}
		#endregion
	}
}
