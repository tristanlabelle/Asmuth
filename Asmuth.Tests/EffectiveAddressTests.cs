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
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._32, ModRMEnum.FromComponents(mod: 3, reg: 0, rm: (byte)GprCode.DX));
			Assert.IsTrue(effectiveAddress.IsDirect);
			Assert.IsFalse(effectiveAddress.AddressSize.HasValue);
			Assert.AreEqual(GprCode.DX, effectiveAddress.DirectGpr);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.IndexAsGpr.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indirect()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._32, ModRMEnum.FromComponents(mod: 0, reg: 0, rm: (byte)GprCode.DX));
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.IndexAsGpr.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indirect16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._16, ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 7));
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
		}

		[TestMethod]
		public void FromEncoding_Indexed16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._16, ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 0));
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.AreEqual(GprCode.SI, effectiveAddress.Index);
		}

		[TestMethod]
		public void FromEncoding_Absolute16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._16, ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 6),
				sib: null, displacement: short.MaxValue);
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_Absolute32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._32, ModRMEnum.FromComponents(mod: 0, reg: 0, rm: 5),
				sib: null, displacement: int.MaxValue);
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.IsFalse(effectiveAddress.Base.HasValue);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement8()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._32, ModRMEnum.FromComponents(mod: 1, reg: 0, rm: (byte)GprCode.DX),
				sib: null, displacement: sbyte.MaxValue);
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
			Assert.AreEqual(sbyte.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement16()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._16, ModRMEnum.FromComponents(mod: 2, reg: 0, rm: 7),
				sib: null, displacement: short.MaxValue);
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._16, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.B, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
			Assert.AreEqual(short.MaxValue, effectiveAddress.Displacement);
		}

		[TestMethod]
		public void FromEncoding_IndirectWithDisplacement32()
		{
			var effectiveAddress = EffectiveAddress.FromEncoding(
				AddressSize._32, ModRMEnum.FromComponents(mod: 2, reg: 0, rm: (byte)GprCode.DX),
				sib: null, displacement: int.MaxValue);
			Assert.IsTrue(effectiveAddress.IsInMemory);
			Assert.AreEqual(AddressSize._32, effectiveAddress.AddressSize);
			Assert.IsFalse(effectiveAddress.DirectGpr.HasValue);
			Assert.AreEqual(AddressBaseRegister.D, effectiveAddress.Base);
			Assert.IsFalse(effectiveAddress.Index.HasValue);
			Assert.AreEqual(int.MaxValue, effectiveAddress.Displacement);
		}
	}
}
