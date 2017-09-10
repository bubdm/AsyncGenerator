﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using AsyncGenerator.Core.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AsyncGenerator.Internal
{
	internal abstract class FunctionData : AbstractData, IFunctionAnalyzationResult
	{
		protected FunctionData(IMethodSymbol methodSymbol)
		{
			Symbol = methodSymbol ?? throw new ArgumentNullException(nameof(methodSymbol));
			AsyncCounterpartName = Symbol.GetAsyncName();
		}

		public IMethodSymbol Symbol { get; }

		public abstract TypeData TypeData { get; }

		public MethodConversion Conversion { get; set; }

		public string AsyncCounterpartName { get; set; }

		public override ISymbol GetSymbol()
		{
			return Symbol;
		}

		/// <summary>
		/// References to other methods that are referenced/invoked inside this function/method and are candidates to be async
		/// </summary>
		public ConcurrentSet<BodyReferenceFunctionData> BodyMethodReferences { get; } = new ConcurrentSet<BodyReferenceFunctionData>();

		public IEnumerable<CrefReferenceFunctionData> CrefMethodReferences => ReferencedMembers.OfType<CrefReferenceFunctionData>();

		public ConcurrentDictionary<SyntaxNode, ChildFunctionData> ChildFunctions { get; } = new ConcurrentDictionary<SyntaxNode, ChildFunctionData>();

		public List<LockData> Locks { get; } = new List<LockData>();

		public List<StatementSyntax> Preconditions { get; } = new List<StatementSyntax>();

		public abstract SyntaxNode GetBodyNode();

		public abstract MethodOrAccessorData GetMethodOrAccessorData();

		public abstract BaseMethodData GetBaseMethodData();

		public ChildFunctionData GetChildFunction(SyntaxNode node, SemanticModel semanticModel, bool create = false)
		{
			if (ChildFunctions.TryGetValue(node, out var typeData))
			{
				return typeData;
			}
			if (!create)
			{
				return null;
			}
			
			if (node is AnonymousFunctionExpressionSyntax anonymousFunc)
			{
				var symbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
				return ChildFunctions.GetOrAdd(node, syntax => new AnonymousFunctionData(GetBaseMethodData(), symbol, anonymousFunc, this));
			}

			if (node is LocalFunctionStatementSyntax localFunc)
			{
				var symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
				return ChildFunctions.GetOrAdd(node, syntax => new LocalFunctionData(GetBaseMethodData(), symbol, localFunc, this));
			}
			throw new InvalidOperationException($"Cannot get a ChildFunctionData from syntax node {node}");
		}

		public IEnumerable<FunctionData> GetSelfAndDescendantsFunctions(Func<FunctionData, bool> predicate = null)
		{
			return GetSelfAndDescendantsFunctionsRecursively(this, predicate);
		}

		public IEnumerable<ChildFunctionData> GetDescendantsChildFunctions(Func<ChildFunctionData, bool> predicate = null)
		{
			return ChildFunctions.Values.SelectMany(o => o.GetSelfAndDescendantsFunctionsRecursively(o, predicate));
		}

		/// <summary>
		/// Copy the method with the possibility to have it also async
		/// </summary>
		public void SoftCopy()
		{
			Conversion &= ~MethodConversion.Ignore;
			Conversion |= MethodConversion.Copy;
		}

		public void Copy()
		{
			// TODO: ToAsync and Copy combined
			// Copy can be mixed with Smart, ToAsync and Unknown
			//Conversion &= ~MethodConversion.Ignore;
			//Conversion &= ~MethodConversion.Unknown;
			IgnoredReason = null;
			if (this is MethodOrAccessorData methodData)
			{
				methodData.CancellationTokenRequired = false;
			}
			Conversion = MethodConversion.Copy;

			foreach (var bodyReference in BodyMethodReferences)
			{
				bodyReference.Ignore("Method will be copied");
			}
			foreach (var childFunction in GetDescendantsChildFunctions())
			{
				childFunction.Copy();
			}
		}

		public override void Ignore(string reason, bool explicitlyIgnored = false)
		{
			if (Conversion != MethodConversion.Ignore || IgnoredReason == null)
			{
				IgnoredReason = reason;
			}
			Conversion = MethodConversion.Ignore;
			ExplicitlyIgnored = explicitlyIgnored;
			foreach (var bodyReference in BodyMethodReferences)
			{
				bodyReference.Ignore("Cascade ignored.");
			}
			foreach (var childFunction in GetDescendantsChildFunctions())
			{
				childFunction.Ignore("Cascade ignored.");
			}
		}

		public void ToAsync()
		{
			if (Conversion.HasFlag(MethodConversion.Smart))
			{
				Conversion &= ~MethodConversion.Smart;
			}
			if (Conversion.HasFlag(MethodConversion.Unknown))
			{
				Conversion &= ~MethodConversion.Unknown;
			}
			if (Conversion.HasFlag(MethodConversion.Copy))
			{
				Conversion |= MethodConversion.ToAsync;
			}
			else
			{
				Conversion = MethodConversion.ToAsync;
			}
		}

		private IEnumerable<T> GetSelfAndDescendantsFunctionsRecursively<T>(T functionData, Func<T, bool> predicate = null)
			where T : FunctionData
		{
			if (predicate?.Invoke(functionData) == false)
			{
				yield break;
			}
			yield return functionData;
			foreach (var subTypeData in functionData.ChildFunctions.Values.OfType<T>())
			{
				if (predicate?.Invoke(subTypeData) == false)
				{
					continue; // We shall never retrun here in order to be always consistent
				}
				foreach (var td in GetSelfAndDescendantsFunctionsRecursively(subTypeData, predicate))
				{
					yield return td;
				}
			}
		}

		#region Analyze step

		public bool RewriteYields { get; set; }

		public BodyReferenceFunctionData ArgumentOfFunctionInvocation { get; set; }

		#endregion

		#region Post analyze step

		public bool SplitTail { get; set; }

		public bool OmitAsync { get; set; }

		public bool PreserveReturnType { get; set; }

		public bool WrapInTryCatch { get; set; }

		public bool Faulted { get; set; }

		#endregion

		#region IFunctionAnalyzationResult

		IMethodOrAccessorAnalyzationResult IFunctionAnalyzationResult.GetMethodOrAccessor() => GetMethodOrAccessorData();

		private IReadOnlyList<IBodyFunctionReferenceAnalyzationResult> _cachedMethodReferences;
		IReadOnlyList<IBodyFunctionReferenceAnalyzationResult> IFunctionAnalyzationResult.MethodReferences => _cachedMethodReferences ?? (_cachedMethodReferences = BodyMethodReferences.ToImmutableArray());

		private IReadOnlyList<ITypeReferenceAnalyzationResult> _cachedTypeReferences;
		IReadOnlyList<ITypeReferenceAnalyzationResult> IFunctionAnalyzationResult.TypeReferences => 
			_cachedTypeReferences ?? (_cachedTypeReferences = ReferencedMembers.OfType<ReferenceTypeData>().ToImmutableArray());

		private IReadOnlyList<StatementSyntax> _cachedPreconditions;
		IReadOnlyList<StatementSyntax> IFunctionAnalyzationResult.Preconditions => _cachedPreconditions ?? (_cachedPreconditions = Preconditions.ToImmutableArray());

		private IReadOnlyList<IChildFunctionAnalyzationResult> _cachedChildFunctions;
		IReadOnlyList<IChildFunctionAnalyzationResult> IFunctionAnalyzationResult.ChildFunctions => _cachedChildFunctions ?? (_cachedChildFunctions = ChildFunctions.Values.ToImmutableArray());

		private IReadOnlyList<ILockAnalyzationResult> _cachedLocks;
		IReadOnlyList<ILockAnalyzationResult> IFunctionAnalyzationResult.Locks => _cachedLocks ?? (_cachedLocks = Locks.ToImmutableArray());

		#endregion
	}
}
