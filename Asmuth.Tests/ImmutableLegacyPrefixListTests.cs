using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asmuth.X86
{
	[TestClass]
	public class ImmutableLegacyPrefixListTests
	{
		[TestMethod]
		public void Test()
		{
			// []
			var list = ImmutableLegacyPrefixList.Empty;
			Assert.AreEqual(0, list.Count);

			// [aso]
			list = ImmutableLegacyPrefixList.Add(list, LegacyPrefix.AddressSizeOverride);
			Assert.AreEqual(1, list.Count);
			Assert.AreEqual(LegacyPrefix.AddressSizeOverride, list[0]);
			Assert.AreEqual(LegacyPrefix.AddressSizeOverride, list.Single()); // Test enumerator

			// [aso, ds]
			list = ImmutableLegacyPrefixList.Add(list, LegacyPrefix.DSSegmentOverride);
			Assert.AreEqual(2, list.Count);
			Assert.AreEqual(LegacyPrefix.AddressSizeOverride, list[0]);
			Assert.AreEqual(LegacyPrefix.DSSegmentOverride, list[1]);

			// [aso, oso, ds]
			list = ImmutableLegacyPrefixList.Insert(list, 1, LegacyPrefix.OperandSizeOverride);
			Assert.AreEqual(3, list.Count);
			Assert.AreEqual(LegacyPrefix.AddressSizeOverride, list[0]);
			Assert.AreEqual(LegacyPrefix.OperandSizeOverride, list[1]);
			Assert.AreEqual(LegacyPrefix.DSSegmentOverride, list[2]);

			// [aso, ds]
			list = ImmutableLegacyPrefixList.RemoveAt(list, 1);
			Assert.AreEqual(2, list.Count);
			Assert.AreEqual(LegacyPrefix.AddressSizeOverride, list[0]);
			Assert.AreEqual(LegacyPrefix.DSSegmentOverride, list[1]);
		}
	}
}
