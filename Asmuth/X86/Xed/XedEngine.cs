using System;
using System.Collections.Generic;
using System.Text;

namespace Asmuth.X86.Xed
{
	public sealed class XedEngine
	{
		public XedRegisterTable RegisterTable { get; }
		public Func<string, XedSymbol> SymbolResolver { get; }

		public XedEngine(XedRegisterTable registerTable, Func<string, XedSymbol> symbolResolver)
		{
			this.RegisterTable = registerTable ?? throw new ArgumentNullException(nameof(registerTable));
			this.SymbolResolver = symbolResolver ?? throw new ArgumentNullException(nameof(symbolResolver));
		}

		public byte[] Encode(XedInstruction instruction, bool x64, IntegerSize operandSize)
		{
			throw new NotImplementedException();
		}
	}
}
