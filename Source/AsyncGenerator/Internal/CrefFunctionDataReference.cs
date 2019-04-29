﻿using System;
using System.Collections.Generic;
using System.Linq;
using AsyncGenerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AsyncGenerator.Internal
{
	internal class CrefFunctionDataReference : AbstractFunctionDataReference<AbstractData>
	{
		public CrefFunctionDataReference(AbstractData data, ReferenceLocation reference, SimpleNameSyntax referenceNameNode,
			IMethodSymbol referenceSymbol, FunctionData referenceFunctionData, bool insideMethodBody)
			: base(data, reference, referenceNameNode, referenceSymbol, referenceFunctionData, insideMethodBody)
		{
		}

		public List<BodyFunctionDataReference> RelatedBodyFunctionReferences { get; } = new List<BodyFunctionDataReference>();

		public override ReferenceConversion GetConversion()
		{
			return (ReferenceFunctionData?.Conversion.HasFlag(MethodConversion.ToAsync) == true) || RelatedBodyFunctionReferences.Any(o => o.GetConversion() == ReferenceConversion.ToAsync)
				? ReferenceConversion.ToAsync
				: ReferenceConversion.Ignore;
		}

		public override ReferenceConversion Conversion { get; protected set; }

		public override string AsyncCounterpartName
		{
			get => ReferenceFunctionData?.Conversion.HasFlag(MethodConversion.ToAsync) == true
				? ReferenceFunctionData.AsyncCounterpartName
				: RelatedBodyFunctionReferences.FirstOrDefault()?.AsyncCounterpartName;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartName)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override IMethodSymbol AsyncCounterpartSymbol
		{
			get => ReferenceFunctionData?.Conversion.HasFlag(MethodConversion.ToAsync) == true
				? ReferenceFunctionData.Symbol
				: RelatedBodyFunctionReferences.FirstOrDefault()?.AsyncCounterpartSymbol;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartSymbol)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override FunctionData AsyncCounterpartFunction
		{
			get => ReferenceFunctionData?.Conversion.HasFlag(MethodConversion.ToAsync) == true
				? ReferenceFunctionData
				: null;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartFunction)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override bool IsCref => true;

		public override bool IsNameOf => false;
	}
}
