using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Asmuth.Debugger
{
	public sealed class Synchronized<T>
	{
		public struct EnterScope : IDisposable
		{
			private readonly Synchronized<T> parent;

			internal EnterScope(Synchronized<T> parent)
			{
				this.parent = parent;
			}

			public void Dispose() => parent.Exit();
		}

		private readonly object monitor = new object();
		private T value;

		public Synchronized() { }
		public Synchronized(T initial)
		{
			value = initial;
		}

		public bool IsEntered => Monitor.IsEntered(monitor);

		public EnterScope Enter()
		{
			Monitor.Enter(monitor);
			return new EnterScope(this);
		}

		public T Value
		{
			get
			{
				Contract.Requires(IsEntered);
				return value;
			}
			set
			{
				Contract.Requires(IsEntered);
				this.value = value;
			}
		}

		private void Exit()
		{
			Monitor.Exit(monitor);
		}
	}
}
