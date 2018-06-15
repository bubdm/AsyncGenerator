﻿using System;
using System.Threading.Tasks;

namespace AsyncGenerator.Core
{
	[Flags]
	public enum AsyncCounterpartsSearchOptions
	{
		/// <summary>
		/// <para>With this option a method should be qualified as an async counterpart if the following conditions are met:</para>
		/// <para>1. The async method is defined in the same type</para>
		/// <para>2. The async method has the same number of parameters and they should be the same, except for delegates that can be an async counterpart of the original</para>
		/// <para>3. The async method return type should be the same type as the original wrapped in a <see cref="Task"/>. 
		/// The async method can also return the same type, but it should have at least one delegate parameter that has an async 
		/// counterpart (eg. <see cref="Task.Run(System.Action)"/> and <see cref="Task.Run(Func{Task})"/>).</para>
		/// </summary>
		Default = 1,
		/// <summary>
		/// With this option a method should be qualified as an async counterpart only if the async method parameters are exactly the same as the original ones.
		/// This option should limit the <see cref="Default"/> constraints.
		/// </summary>
		EqualParameters = 2,
		/// <summary>
		/// With this option a method should be qualified as an async counterpart if it is defined in an inherited type.
		/// This option should extend the <see cref="Default"/> constraints.
		/// </summary>
		SearchInheritedTypes = 4,
		/// <summary>
		/// With this option a method should be qualified as an async counterpart if has an additional parameter of type <see cref="System.Threading.CancellationToken"/>.
		/// This options should extend the <see cref="Default"/> constraints.
		/// </summary>
		HasCancellationToken = 8,
		/// <summary>
		/// With this option a method should be qualified as an async counterpart even if the return type do not match
		/// This options should extend the <see cref="Default"/> constraints.
		/// </summary>
		IgnoreReturnType = 16
	}
}
