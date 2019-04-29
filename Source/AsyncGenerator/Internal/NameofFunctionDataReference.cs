﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AsyncGenerator.Internal
{
	internal class NameofFunctionDataReference : AbstractFunctionDataReference<AbstractData>
	{
		public NameofFunctionDataReference(AbstractData data, ReferenceLocation reference, SimpleNameSyntax referenceNameNode,
			Dictionary<IMethodSymbol, FunctionData> references, bool insideMethodBody)
			: base(data, reference, referenceNameNode, references.First().Key, references.First().Value, insideMethodBody)
		{
			ReferencedFunctions = references;
		}

		public Dictionary<IMethodSymbol, FunctionData> ReferencedFunctions { get; set; }

		public List<BodyFunctionDataReference> RelatedBodyFunctionReferences { get; } = new List<BodyFunctionDataReference>();

		public override ReferenceConversion GetConversion()
		{
			return ReferencedFunctions.Values.Where(o => o != null).Any(o => o.Conversion.HasFlag(MethodConversion.ToAsync)) ||
			       RelatedBodyFunctionReferences.Any(o => o.GetConversion() == ReferenceConversion.ToAsync)
				? ReferenceConversion.ToAsync
				: ReferenceConversion.Ignore;
		}

		public override ReferenceConversion Conversion { get; protected set; }

		public override string AsyncCounterpartName
		{
			get => ReferencedFunctions.Values.FirstOrDefault(o => o != null && o.Conversion.HasFlag(MethodConversion.ToAsync))?.AsyncCounterpartName ??
			       RelatedBodyFunctionReferences.FirstOrDefault()?.AsyncCounterpartName;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartName)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override IMethodSymbol AsyncCounterpartSymbol
		{
			get => ReferencedFunctions.Values.FirstOrDefault(o => o != null && o.Conversion.HasFlag(MethodConversion.ToAsync))?.Symbol ??
			       RelatedBodyFunctionReferences.FirstOrDefault()?.AsyncCounterpartSymbol ??
			       ReferenceFunctionData?.Symbol;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartSymbol)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override FunctionData AsyncCounterpartFunction
		{
			get => ReferencedFunctions.Values.FirstOrDefault(o => o != null && o.Conversion.HasFlag(MethodConversion.ToAsync)) ??
			       RelatedBodyFunctionReferences.FirstOrDefault()?.ReferenceFunctionData;
			set => throw new NotSupportedException($"Setting {nameof(AsyncCounterpartFunction)} for {nameof(CrefFunctionDataReference)} is not supported");
		}

		public override bool IsNameOf => true;

		public override bool IsCref => false;
	}
}
