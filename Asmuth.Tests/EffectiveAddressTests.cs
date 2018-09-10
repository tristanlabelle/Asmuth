using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
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
				EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default32, new EffectiveAddress.Encoding(
					ModRM.WithDirectRM(reg: 0, rm: GprCode.DX)));
			});
		}

		[TestMethod]
		public void FromEncoding_Indirect()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default32, new EffectiveAddress.Encoding(
				new ModRM(ModRMMod.Indirect, reg: 0, rm: GprCode.DX)));
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGpr.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indirect16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default16, new EffectiveAddress.Encoding(
				new ModRM(ModRMMod.Indirect, reg: 0, rm: 7)));
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indexed16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default16, new EffectiveAddress.Encoding(
				new ModRM(ModRMMod.Indirect, reg: 0, rm: (byte)0)));
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.AreEqual(GprCode.SI, effectiveAddress.IndexAsGprCode);
		}

		[TestMethod]
		public void FromEncoding_Absolute16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default16, new EffectiveAddress.Encoding(
				ModRM.WithAbsoluteRM_16(reg: 0), (Sib?)null, short.MaxValue));
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_Absolute32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default32, new EffectiveAddress.Encoding(
				ModRM.WithAbsoluteRM_32(reg: 0), (Sib?)null, int.MaxValue));
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement8()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default32, new EffectiveAddress.Encoding(
				new ModRM(mod: ModRMMod.IndirectDisp8, reg: 0, rm: GprCode.D), (Sib?)null, sbyte.MaxValue));
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(sbyte.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default16, new EffectiveAddress.Encoding(
				new ModRM(mod: ModRMMod.IndirectLongDisp, reg: 0, rm: 7), (Sib?)null, short.MaxValue));
			
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.IA32_Default32, new EffectiveAddress.Encoding(
				new ModRM(mod: ModRMMod.IndirectLongDisp, reg: 0, rm: GprCode.D), (Sib?)null, int.MaxValue));
			
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGprCode.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}
		
		[TestMethod]
		public void FromEncoding_SibZeroIndex()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.X64, new EffectiveAddress.Encoding(
				ModRM.WithSib(ModRMMod.Indirect, reg: 0),
				new Sib(SibScale._1, index: null, @base: GprCode.B)));

			Assert.AreEqual(AddressSize._64, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.AreEqual(null, effectiveAddress.IndexAsGprCode);
		}

		[TestMethod]
		public void FromEncoding_RegExtensions()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(CodeSegmentType.X64, new EffectiveAddress.Encoding(
				EffectiveAddress.EncodingFlags.BaseRegExtension | EffectiveAddress.EncodingFlags.IndexRegExtension,
				ModRM.WithSib(ModRMMod.Indirect, reg: 0),
				new Sib(SibScale._1, index: GprCode.C, @base: GprCode.B)));

			Assert.AreEqual(AddressSize._64, effectiveAddress.AddressSize);
			Assert.AreEqual(AddressBaseRegister.B + 8, effectiveAddress.Base);
			Assert.AreEqual(GprCode.C + 8, effectiveAddress.IndexAsGprCode);
		}
	}
}
