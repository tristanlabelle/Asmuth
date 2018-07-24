using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Asmuth.X86
{
	[TestClass]
	public sealed class EffectiveAddressFromEncodingTests
	{
		[TestMethod]
		public void FromEncoding_Direct()
		{
			ExceptionAssert.Throws<ArgumentException>(() =>
			{
				EffectiveAddress.FromEncoding(CodeSegmentType._32Bits, new EffectiveAddress.Encoding
				{
					ModRM = ModRMEnum.FromComponents(mod: 3, reg: 0, rm: GprCode.DX)
				});
			});
		}

		[TestMethod]
		public void FromEncoding_Indirect()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._32Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 0, reg: 0, rm: GprCode.DX)
			});
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGpr.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indirect16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._16Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 7)
			});
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indexed16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._16Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 0, reg: 0, rm: (byte)0)
			});
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.AreEqual(GprCode.SI, effectiveAddress.IndexAsGprCode);
		}

		[TestMethod]
		public void FromEncoding_Absolute16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._16Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 6),
				Displacement = short.MaxValue
			});
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_Absolute32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._32Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 5),
				Displacement = int.MaxValue
			});
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement8()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._32Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 1, reg: 0, rm: GprCode.D),
				Displacement = sbyte.MaxValue
			});
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(sbyte.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._16Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 2, reg: 0, rm: 7),
				Displacement = short.MaxValue
			});
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType._32Bits, new EffectiveAddress.Encoding
			{
				ModRM = ModRMEnum.FromComponents(mod: 2, reg: 0, rm: GprCode.D),
				Displacement = int.MaxValue
			});
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}
	}
}
