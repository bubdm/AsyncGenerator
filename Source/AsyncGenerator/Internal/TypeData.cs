﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AsyncGenerator.Internal
{
	internal class TypeData : AbstractData, ITypeAnalyzationResult
	{
		public TypeData(NamespaceData namespaceData, INamedTypeSymbol symbol, TypeDeclarationSyntax node, TypeData parentTypeData = null)
		{
			NamespaceData = namespaceData;
			ParentTypeData = parentTypeData;
			Symbol = symbol;
			Node = node;
		}

		/// <summary>
		/// Base types
		/// </summary>
		public ConcurrentSet<TypeData> BaseTypes { get; } = new ConcurrentSet<TypeData>();

		public TypeData ParentTypeData { get; }

		public NamespaceData NamespaceData { get; }

		public INamedTypeSymbol Symbol { get; }

		public TypeDeclarationSyntax Node { get; }

		public TypeConversion Conversion { get; internal set; }

		public override ISymbol GetSymbol()
		{
			return Symbol;
		}

		public override void Copy()
		{
			base.Copy();
			Conversion = TypeConversion.Copy;
			foreach (var typeData in GetSelfAndDescendantsTypeData().Where(o => o.Conversion != TypeConversion.Ignore))
			{
				typeData.Conversion = TypeConversion.Copy;
			}
		}

		protected override void Ignore()
		{
			Conversion = TypeConversion.Ignore;
			foreach (var typeData in GetSelfAndDescendantsTypeData().Where(o => o.Conversion != TypeConversion.Ignore))
			{
				typeData.Ignore(IgnoreReason.Cascade);
			}
		}

		public bool IsPartial { get; set; }

		public bool IsNewType => GetSelfAndAncestorsTypeData().Any(o => o.Conversion == TypeConversion.NewType);

		public ConcurrentDictionary<MethodDeclarationSyntax, MethodData> Methods { get; } = new ConcurrentDictionary<MethodDeclarationSyntax, MethodData>();

		public ConcurrentDictionary<BaseMethodDeclarationSyntax, BaseMethodData> SpecialMethods { get; } = new ConcurrentDictionary<BaseMethodDeclarationSyntax, BaseMethodData>();

		public ConcurrentDictionary<PropertyDeclarationSyntax, PropertyData> Properties { get; } = new ConcurrentDictionary<PropertyDeclarationSyntax, PropertyData>();

		public ConcurrentDictionary<BaseFieldDeclarationSyntax, BaseFieldData> Fields { get; } = new ConcurrentDictionary<BaseFieldDeclarationSyntax, BaseFieldData>();

		public ConcurrentDictionary<TypeDeclarationSyntax, TypeData> NestedTypes { get; } = new ConcurrentDictionary<TypeDeclarationSyntax, TypeData>();

		public IEnumerable<MethodOrAccessorData> MethodsAndAccessors
		{
			get { return Methods.Values.Cast<MethodOrAccessorData>().Concat(Properties.Values.SelectMany(o => o.GetAccessors())); }
		}

		public override SyntaxNode GetNode()
		{
			return Node;
		}

		public IEnumerable<TypeData> GetSelfAndAncestorsTypeData()
		{
			var current = this;
			while (current != null)
			{
				yield return current;
				current = current.ParentTypeData;
			}
		}

		public IEnumerable<TypeData> GetSelfAndDescendantsTypeData(Func<TypeData, bool> predicate = null)
		{
			return GetSelfAndDescendantsTypeDataRecursively(this, predicate);
		}

		private IEnumerable<TypeData> GetSelfAndDescendantsTypeDataRecursively(TypeData typeData, Func<TypeData, bool> predicate = null)
		{
			if (predicate?.Invoke(typeData) == false)
			{
				yield break;
			}
			yield return typeData;
			foreach (var subTypeData in typeData.NestedTypes.Values)
			{
				if (predicate?.Invoke(subTypeData) == false)
				{
					continue; // We shall never retrun here in order to be always consistent
				}
				foreach (var td in GetSelfAndDescendantsTypeDataRecursively(subTypeData, predicate))
				{
					yield return td;
				}
			}
		}

		public TypeData GetNestedTypeData(TypeDeclarationSyntax node, SemanticModel semanticModel, bool create = false)
		{
			if (NestedTypes.TryGetValue(node, out TypeData typeData))
			{
				return typeData;
			}
			var symbol = semanticModel.GetDeclaredSymbol(node);
			return !create ? null : NestedTypes.GetOrAdd(node, syntax => new TypeData(NamespaceData, symbol, node, this));
		}

		public MethodData GetMethodData(MethodDeclarationSyntax methodNode, SemanticModel semanticModel, bool create = false)
		{
			if (Methods.TryGetValue(methodNode, out MethodData methodData))
			{
				return methodData;
			}
			var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);
			return !create ? null : Methods.GetOrAdd(methodNode, syntax => new MethodData(this, methodSymbol, methodNode));
		}

		public BaseMethodData GetSpecialMethodData(BaseMethodDeclarationSyntax methodNode, SemanticModel semanticModel, bool create = false)
		{
			if (SpecialMethods.TryGetValue(methodNode, out BaseMethodData methodData))
			{
				return methodData;
			}
			var methodSymbol = semanticModel.GetDeclaredSymbol(methodNode);
			return !create ? null : SpecialMethods.GetOrAdd(methodNode, syntax => new BaseMethodData(this, methodSymbol, methodNode));
		}

		public PropertyData GetPropertyData(PropertyDeclarationSyntax node, SemanticModel semanticModel, bool create = false)
		{
			if (Properties.TryGetValue(node, out PropertyData data))
			{
				return data;
			}
			var symbol = semanticModel.GetDeclaredSymbol(node);
			return !create ? null : Properties.GetOrAdd(node, syntax => new PropertyData(this, symbol, node));
		}

		public BaseFieldData GetBaseFieldData(BaseFieldDeclarationSyntax node, SemanticModel semanticModel, bool create = false)
		{
			if (Fields.TryGetValue(node, out BaseFieldData data))
			{
				return data;
			}
			return !create ? null : Fields.GetOrAdd(node, syntax => new BaseFieldData(this, node, semanticModel));
		}


		#region ITypeAnalyzationResult

		private IReadOnlyList<ITypeReferenceAnalyzationResult> _cachedTypeReferences;
		IReadOnlyList<ITypeReferenceAnalyzationResult> ITypeAnalyzationResult.TypeReferences => _cachedTypeReferences ?? (_cachedTypeReferences = References.OfType<TypeDataReference>().ToImmutableArray());

		private IReadOnlyList<IMethodAnalyzationResult> _cachedMethods;
		IReadOnlyList<IMethodAnalyzationResult> ITypeAnalyzationResult.Methods => _cachedMethods ?? (_cachedMethods = Methods.Values.ToImmutableArray());

		IEnumerable<IMethodOrAccessorAnalyzationResult> ITypeAnalyzationResult.MethodsAndAccessors => MethodsAndAccessors;

		private IReadOnlyList<ITypeAnalyzationResult> _cachedNestedTypes;
		IReadOnlyList<ITypeAnalyzationResult> ITypeAnalyzationResult.NestedTypes => _cachedNestedTypes ?? (_cachedNestedTypes = NestedTypes.Values.ToImmutableArray());

		private IReadOnlyList<IPropertyAnalyzationResult> _cachedProperties;
		IReadOnlyList<IPropertyAnalyzationResult> ITypeAnalyzationResult.Properties => _cachedProperties ?? (_cachedProperties = Properties.Values.ToImmutableArray());

		private IReadOnlyList<IFieldAnalyzationResult> _cachedFields;
		IReadOnlyList<IFieldAnalyzationResult> ITypeAnalyzationResult.Fields => _cachedFields ?? (_cachedFields = Fields.Values.ToImmutableArray());

		private IReadOnlyList<IFunctionAnalyzationResult> _cachedSpecialMethods;
		IReadOnlyList<IFunctionAnalyzationResult> ITypeAnalyzationResult.SpecialMethods => _cachedSpecialMethods ?? (_cachedSpecialMethods = SpecialMethods.Values.ToImmutableArray());

		#endregion
	}
}
