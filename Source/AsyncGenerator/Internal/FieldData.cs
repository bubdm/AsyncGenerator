﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AsyncGenerator.Internal
{
	internal class LocalVariableData : AbstractData
	{
		public LocalVariableData(FunctionData functionData, ILocalSymbol symbol, VariableDeclaratorSyntax node, IMethodSymbol declaredMethodSymbol, FunctionData declaredFunctionData)
		{
			FunctionData = functionData;
			Symbol = symbol;
			Node = node;
			DeclaredMethodSymbol = declaredMethodSymbol;
			DeclaredFunctionData = declaredFunctionData;
		}

		public VariableDeclaratorSyntax Node { get; }

		public IMethodSymbol DeclaredMethodSymbol { get; }

		public FunctionData DeclaredFunctionData { get; set; }

		public ILocalSymbol Symbol { get; }

		public FunctionData FunctionData { get; }

		protected override void Ignore()
		{
			base.Ignore();
			foreach (var selfReference in SelfReferences)
			{
				if (!(selfReference is LocalVariableDataReference localVariableReference))
				{
					continue;
				}
				foreach (var bodyFunctionReference in localVariableReference.RelatedBodyFunctionReferences)
				{
					bodyFunctionReference.Ignore(IgnoreReason.Cascade);
				}
			}
		}

		public override SyntaxNode GetNode()
		{
			return Node;
		}

		public override ISymbol GetSymbol()
		{
			return Symbol;
		}
	}

	internal class BaseFieldData : AbstractData, IFieldAnalyzationResult
	{
		public BaseFieldData(TypeData typeData, BaseFieldDeclarationSyntax node, SemanticModel semanticModel)
		{
			TypeData = typeData;
			Node = node;
			Symbol = semanticModel.GetSymbolInfo(node.Declaration.Type).Symbol ?? throw new InvalidOperationException($"symbol for field type {node.Declaration.Type} was not found");
			var list = new List<FieldVariableDeclaratorData>();
			foreach (var variable in node.Declaration.Variables)
			{
				var symbol = semanticModel.GetDeclaredSymbol(variable);
				list.Add(new FieldVariableDeclaratorData(this, symbol, variable));
			}
			Variables = list.AsReadOnly();
		}

		public IReadOnlyList<FieldVariableDeclaratorData> Variables { get; }

		/// <summary>
		/// Represent the field type symbol
		/// </summary>
		public ISymbol Symbol { get; }

		public TypeData TypeData { get; }

		public BaseFieldDeclarationSyntax Node { get; }

		#region IFieldAnalyzationResult

		IReadOnlyList<IFieldVariableDeclaratorResult> IFieldAnalyzationResult.Variables => Variables;

		private IReadOnlyList<ITypeReferenceAnalyzationResult> _cachedTypeReferences;
		IReadOnlyList<ITypeReferenceAnalyzationResult> IFieldAnalyzationResult.TypeReferences => 
			_cachedTypeReferences ?? (_cachedTypeReferences = References.OfType<TypeDataReference>().ToImmutableArray());

		#endregion

		public override SyntaxNode GetNode() => Node;

		public override ISymbol GetSymbol() => Symbol;

		protected override void Ignore()
		{
			base.Ignore();
			foreach (var variable in Variables)
			{
				variable.Ignore(IgnoreReason.Cascade, ExplicitlyIgnored);
			}
		}
	}
}
