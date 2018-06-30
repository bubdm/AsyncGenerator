﻿using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using AsyncGenerator.Extensions.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AsyncGenerator.Internal
{
	internal abstract class AbstractData : IAnalyzationResult
	{
		/// <summary>
		/// References that are used inside this data (eg. variable declaration, typeof, cref, nameof, method invocation)
		/// </summary>
		public ConcurrentSet<IDataReference> References { get; } = new ConcurrentSet<IDataReference>();

		/// <summary>
		/// References to other members that are using this data (eg. variable declaration, typeof, cref, nameof)
		/// </summary>
		public ConcurrentSet<IDataReference> SelfReferences { get; } = new ConcurrentSet<IDataReference>();

		/// <summary>
		/// An enumerable of all members inside the project that are referencing this data. This can be used only when references of this data were scanned.
		/// </summary>
		public IEnumerable<AbstractData> ReferencedBy => SelfReferences.Select(o => o.Data);

		public IEnumerable<FunctionData> ReferencedByFunctions => ReferencedBy.OfType<FunctionData>();

		public bool HasAnyActiveReference()
		{
			foreach (var data in ReferencedBy)
			{
				if (data is AccessorData accessorData)
				{
					if (accessorData.Conversion.HasAnyFlag(MethodConversion.ToAsync, MethodConversion.Copy) ||
					    accessorData.PropertyData.Conversion == PropertyConversion.Copy)
					{
						return true;
					}
				}
				else if (data is FunctionData functionData)
				{
					if (functionData.Conversion.HasAnyFlag(MethodConversion.ToAsync, MethodConversion.Copy))
					{
						return true;
					}
				}
				else if (data is PropertyData propertyData)
				{
					if (propertyData.Conversion == PropertyConversion.Copy)
					{
						return true;
					}
				}
				else if (data is BaseFieldData field)
				{
					if (field.Variables.Any(o => o.Conversion == FieldVariableConversion.Copy))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Get the syntax node of the function
		/// </summary>
		public abstract SyntaxNode GetNode();

		public abstract ISymbol GetSymbol();

		public IgnoreReason IgnoredReason { get; private set; }

		public void SetIgnoreSeverity(DiagnosticSeverity severity)
		{
			IgnoredReason = IgnoredReason?.WithSeverity(severity);
		}

		public bool ExplicitlyIgnored { get; private set; }

		public FileLinePositionSpan GetLineSpan()
		{
			return GetNode()?.GetLineSpan() ?? default(FileLinePositionSpan);
		}

		/// <summary>
		/// Can be null
		/// </summary>
		private List<DiagnosticData> Diagnostics { get; set; }

		public IEnumerable<DiagnosticData> GetDiagnostics()
		{
			return Diagnostics ?? Enumerable.Empty<DiagnosticData>();
		}

		public void AddDiagnostic(string description, DiagnosticSeverity diagnosticSeverity)
		{
			(Diagnostics ?? (Diagnostics = new List<DiagnosticData>())).Add(new DiagnosticData(description, diagnosticSeverity));
		}

		public void Ignore(IgnoreReason reason, bool explicitlyIgnored = false)
		{
			if (IgnoredReason != null)
			{
				return;
			}
			IgnoredReason = reason;
			ExplicitlyIgnored = explicitlyIgnored;
			Ignore();
		}

		public virtual void Copy()
		{
			if (IgnoredReason == null)
			{
				return;
			}
			IgnoredReason = null;
			ExplicitlyIgnored = false;
		}

		protected virtual void Ignore()
		{
		}

		#region IAnalyzationResult


		private IReadOnlyList<IReferenceAnalyzationResult> _cachedReferences;
		IReadOnlyList<IReferenceAnalyzationResult> IAnalyzationResult.References => _cachedReferences ?? (_cachedReferences = References.ToImmutableArray());

		private IReadOnlyList<IReferenceAnalyzationResult> _cachedSelfReferences;
		IReadOnlyList<IReferenceAnalyzationResult> IAnalyzationResult.SelfReferences => _cachedSelfReferences ?? (_cachedSelfReferences = SelfReferences.ToImmutableArray());

		IEnumerable<IAnalyzationResult> IAnalyzationResult.ReferencedBy => ReferencedBy;

		#endregion
	}
}
