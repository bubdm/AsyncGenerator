﻿using System;
using Microsoft.CodeAnalysis;

namespace AsyncGenerator.Core.Configuration
{
	public interface IProjectAnalyzeConfiguration
	{
		bool SearchAsyncCounterpartsInInheritedTypes { get; }

		bool ScanMethodBody { get; }

		Predicate<INamedTypeSymbol> ScanForMissingAsyncMembers { get; }

		bool UseCancellationTokens { get; }

		IProjectCancellationTokenConfiguration CancellationTokens { get; }

		bool ConcurrentRun { get; }

		Predicate<IMethodSymbol> AlwaysAwait { get; }
	}
}
