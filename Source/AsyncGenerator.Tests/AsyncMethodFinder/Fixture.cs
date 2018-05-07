﻿using System;
using System.Linq;
using System.Threading.Tasks;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Plugins;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using AsyncGenerator.Tests.AsyncMethodFinder.Input;

namespace AsyncGenerator.Tests.AsyncMethodFinder
{
	[TestFixture]
	public class Fixture : BaseFixture
	{
		[Test]
		public Task TestCustomLinqExtensionsAfterTransformation()
		{
			return ReadonlyTest(nameof(CustomLinqExtensions), p => p
				.ConfigureAnalyzation(a => a
					.MethodConversion(symbol => MethodConversion.Smart)
					.AsyncExtensionMethods(e => e.ProjectFile("AsyncGenerator.Tests", "LinqExtensions.cs"))
				)
				.ConfigureTransformation(t => t
					.AfterTransformation(result =>
					{
						AssertValidAnnotations(result);
						Assert.AreEqual(1, result.Documents.Count);
						var document = result.Documents[0];
						Assert.NotNull(document.OriginalModified);
						Assert.AreEqual(GetOutputFile(nameof(CustomLinqExtensions)), document.Transformed.ToFullString());
					})
				)
			);
		}

		[Test]
		public Task TestNUnitAssertThatAfterTransformation()
		{
			return ReadonlyTest(nameof(NUnitAssertThat), p => p
				.ConfigureAnalyzation(a => a
					.MethodConversion(symbol => MethodConversion.Smart)
					.PreserveReturnType(o => true)
				)
				.ConfigureTransformation(t => t
					.AfterTransformation(result =>
					{
						AssertValidAnnotations(result);
						Assert.AreEqual(1, result.Documents.Count);
						var document = result.Documents[0];
						Assert.NotNull(document.OriginalModified);
						Assert.AreEqual(GetOutputFile(nameof(NUnitAssertThat)), document.Transformed.ToFullString());
					})
				)
				.RegisterPlugin<NUnitAsyncCounterpartsFinder>()
			);
		}

		[Test]
		public Task TestIgnoreSomeAsyncMethodsAfterTransformation()
		{
			return ReadonlyTest(nameof(IgnoreSomeAsyncMethods), p => p
				.ConfigureAnalyzation(a => a
					.MethodConversion(symbol => MethodConversion.Smart)
					.SearchForAsyncCounterparts(symbol => symbol.Name == "Write" ? false : true)
				)
				.ConfigureTransformation(t => t
					.AfterTransformation(result =>
					{
						AssertValidAnnotations(result);
						Assert.AreEqual(1, result.Documents.Count);
						var document = result.Documents[0];
						Assert.NotNull(document.OriginalModified);
						Assert.AreEqual(GetOutputFile(nameof(IgnoreSomeAsyncMethods)), document.Transformed.ToFullString());
					})
				)
			);
		}

		[Test]
		public Task TestGenericParameterAfterTransformation()
		{
			return ReadonlyTest(nameof(GenericParameter), p => p
				.ConfigureAnalyzation(a => a
					.MethodConversion(symbol => MethodConversion.Smart)
					.CancellationTokens(true)
				)
				.ConfigureTransformation(t => t
					.AfterTransformation(result =>
					{
						AssertValidAnnotations(result);
						Assert.AreEqual(1, result.Documents.Count);
						var document = result.Documents[0];
						Assert.NotNull(document.OriginalModified);
						Assert.AreEqual(GetOutputFile(nameof(GenericParameter)), document.Transformed.ToFullString());
					})
				)
			);
		}
	}
}
