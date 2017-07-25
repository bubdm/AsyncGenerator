﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core;
using AsyncGenerator.Core.Analyzation;
using AsyncGenerator.Core.Configuration;
using AsyncGenerator.Core.Plugins;
using AsyncGenerator.Core.Transformation;
using AsyncGenerator.Extensions;
using AsyncGenerator.Extensions.Internal;
using AsyncGenerator.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SyntaxNodeExtensions = AsyncGenerator.Extensions.Internal.SyntaxNodeExtensions;

namespace AsyncGenerator.Transformation.Internal
{
	internal class ReturnTaskTransformer : IMethodOrAccessorTransformer
	{
		public Task Initialize(Project project, IProjectConfiguration configuration)
		{
			return Task.CompletedTask;
		}

		public MethodTransformerResult Transform(IMethodOrAccessorTransformationResult transformResult,
			ITypeTransformationMetadata typeMetadata, INamespaceTransformationMetadata namespaceMetadata)
		{
			var methodResult = transformResult.AnalyzationResult;
			if (!methodResult.Conversion.HasFlag(MethodConversion.ToAsync))
			{
				return MethodTransformerResult.Skip;
			}
			var methodNode = transformResult.Transformed;
			if (methodNode.GetFunctionBody() == null)
			{
				return Update(methodNode, methodResult, namespaceMetadata);
			}
			if (methodResult.SplitTail || methodResult.PreserveReturnType || !methodResult.OmitAsync)
			{
				if (!methodResult.OmitAsync)
				{
					methodNode = methodNode.AddAsync();
				}
				return Update(methodNode, methodResult, namespaceMetadata);
			}
			var rewriter = new ReturnTaskMethodRewriter(transformResult, namespaceMetadata);
			methodNode = (MethodDeclarationSyntax)rewriter.VisitMethodDeclaration(methodNode);
			return Update(methodNode, methodResult, namespaceMetadata);
		}

		private MethodTransformerResult Update(MethodDeclarationSyntax methodNode, 
			IMethodOrAccessorAnalyzationResult methodResult, INamespaceTransformationMetadata namespaceMetadata)
		{
			methodNode = methodNode.WithIdentifier(Identifier(methodResult.AsyncCounterpartName));
			if (!methodResult.PreserveReturnType && methodResult.Symbol.MethodKind != MethodKind.PropertySet)
			{
				methodNode = methodNode.ReturnAsTask(namespaceMetadata.TaskConflict);
			}
			return MethodTransformerResult.Update(methodNode);
		}
	}


	/// <summary>
	/// Wraps all non taskable returns statements into a <see cref="Task.FromResult{TResult}"/> and conditionally wraps the method body
	/// in a try/catch block (without preconditions) 
	/// </summary>
	internal class ReturnTaskMethodRewriter : CSharpSyntaxRewriter
	{
		private readonly IMethodOrAccessorAnalyzationResult _methodResult;
		private readonly IMethodOrAccessorTransformationResult _transformResult;
		private readonly INamespaceTransformationMetadata _namespaceMetadata;
		private SyntaxKind? _rewritingSyntaxKind;
		private MethodDeclarationSyntax _methodNode;

		public ReturnTaskMethodRewriter(IMethodOrAccessorTransformationResult transformResult, INamespaceTransformationMetadata namespaceMetadata)
		{
			_transformResult = transformResult;
			_methodResult = transformResult.AnalyzationResult;
			_namespaceMetadata = namespaceMetadata;
		}

		public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			_rewritingSyntaxKind = node.Kind();
			_methodNode = node;
			if (!_methodResult.Faulted && 
				(
					(_methodResult.Symbol.ReturnsVoid && node.IsReturnStatementRequired()) || 
					_methodResult.WrapInTryCatch
				)
			)
			{
				node = node.ConvertExpressionBodyToBlock(_transformResult);
			}
			node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
			if (node.GetFunctionBody() is BlockSyntax blockBody)
			{
				return node.WithBody(RewriteFunctionBody(blockBody));
			}
			return node;
		}

		public override SyntaxNode VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
		{
			if (_rewritingSyntaxKind.HasValue)
			{
				return node;
			}
			_rewritingSyntaxKind = node.Kind();
			node =  (AnonymousMethodExpressionSyntax)base.VisitAnonymousMethodExpression(node);
			var bodyBlock = node.GetFunctionBody() as BlockSyntax;
			if (bodyBlock != null)
			{
				return node.WithBody(RewriteFunctionBody(bodyBlock));
			}
			// TODO: handle arrow methods
			return node;
		}

		public override SyntaxNode VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
		{
			if (_rewritingSyntaxKind.HasValue)
			{
				return node;
			}
			_rewritingSyntaxKind = node.Kind();
			node = (ParenthesizedLambdaExpressionSyntax)base.VisitParenthesizedLambdaExpression(node);
			var bodyBlock = node.GetFunctionBody() as BlockSyntax;
			if (bodyBlock != null)
			{
				return node.WithBody(RewriteFunctionBody(bodyBlock));
			}
			// TODO: handle arrow methods
			return node;
		}

		public override SyntaxNode VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
		{
			if (_rewritingSyntaxKind.HasValue)
			{
				return node;
			}
			_rewritingSyntaxKind = node.Kind();
			node = (SimpleLambdaExpressionSyntax)base.VisitSimpleLambdaExpression(node);
			var bodyBlock = node.GetFunctionBody() as BlockSyntax;
			if (bodyBlock != null)
			{
				return node.WithBody(RewriteFunctionBody(bodyBlock));
			}
			// TODO: handle arrow methods
			return node;
		}

		public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
		{
			if (_rewritingSyntaxKind.HasValue)
			{
				return node;
			}
			_rewritingSyntaxKind = node.Kind();
			node = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node);
			var bodyBlock = node.GetFunctionBody() as BlockSyntax;
			if (bodyBlock != null)
			{
				return node.WithBody(RewriteFunctionBody(bodyBlock));
			}
			// TODO: handle arrow methods
			return node;
		}

		//public override SyntaxNode VisitCatchClause(CatchClauseSyntax node)
		//{
		//	// TODO: add a declaration only if there is a throws statement
		//	if (node.Declaration == null)
		//	{
		//		node = node.WithDeclaration(
		//			CatchDeclaration(IdentifierName(Identifier(TriviaList(), "Exception", TriviaList(Space))))
		//				.WithIdentifier(Identifier("x"))
		//				.WithCloseParenToken(Token(TriviaList(), SyntaxKind.CloseParenToken, TriviaList(_eolTrivia))));
		//	}
		//	else if (node.Declaration.Identifier.ValueText == null)
		//	{
		//		node = node.ReplaceNode(node.Declaration, node.Declaration.WithIdentifier(Identifier("x")));
		//	}
		//	return base.VisitCatchClause(node);
		//}

		public override SyntaxNode Visit(SyntaxNode node)
		{
			if (node == null)
			{
				return null;
			}
			// Skip if the statement is a precondition
			if (_methodResult.Preconditions.Count > 0)
			{
				var statement = node as StatementSyntax;
				// First parent shall be the BlockSyntax (body) and second parent shall be the method
				if (statement != null && statement.Parent?.Parent?.IsKind(_rewritingSyntaxKind.GetValueOrDefault()) == true)
				{
					// Here we can safe cast to BlockSyntax as if there is a StatementSyntax is also a BlockSyntax
					var body = (BlockSyntax)statement.Parent;
					for (var i = 0; i < _methodResult.Preconditions.Count; i++)
					{
						if (body.Statements[i].Equals(statement))
						{
							return node;
						}
					}
				}
			}

			// If the expression is returned and does not return a Task then wrap it into Task.FromResult
			var expression = node as ExpressionSyntax;
			if (expression != null && expression.IsReturned() && !expression.GetAnnotations(Annotations.TaskReturned).Any())
			{
				// Before wrapping into a task we need to check if is a conditional expression as we can have a conditional expression and one or both parts can return a Task
				if (node is ConditionalExpressionSyntax conditionalExpression)
				{
					var isWhenTrueTask = conditionalExpression.WhenTrue.GetAnnotations(Annotations.TaskReturned).Any();
					var isWhenFalseTask = conditionalExpression.WhenFalse.GetAnnotations(Annotations.TaskReturned).Any();
					var whenFalse = isWhenFalseTask
						? conditionalExpression.WhenFalse
						: WrapInTaskFromResult(conditionalExpression.WhenFalse);
					var whenTrue = isWhenTrueTask
						? conditionalExpression.WhenTrue
						: WrapInTaskFromResult(conditionalExpression.WhenTrue);
					return conditionalExpression
						.WithWhenFalse(whenFalse)
						.WithWhenTrue(whenTrue);
				}
				return WrapInTaskFromResult(expression);
			}
			return base.Visit(node);
		}

		public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
		{
			// If we have only a return statement rewrite it to a return Task.CompletedTask
			if (node.Expression == null)
			{
				return GetReturnTaskCompleted().WithTriviaFrom(node);
			}
			return base.VisitReturnStatement(node);
		}

		public override SyntaxNode VisitThrowStatement(ThrowStatementSyntax node)
		{
			if (_methodResult.Faulted)
			{
				return node;
			}

			if (node.Expression == null)
			{
				var catchNode = node.Ancestors().OfType<CatchClauseSyntax>().First();
				return ReturnStatement(
					Token(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)),
					WrapInTaskFromException(IdentifierName(catchNode.Declaration.Identifier.ValueText)),
					Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)));
			}

			return ReturnStatement(
					Token(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)),
					WrapInTaskFromException(node.Expression),
					Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)))
				.WithReturnKeyword(Token(TriviaList(node.GetLeadingTrivia()), SyntaxKind.ReturnKeyword, TriviaList(Space)));
		}

		private InvocationExpressionSyntax WrapInTaskFromResult(ExpressionSyntax node)
		{
			return InvocationExpression(
					MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						_namespaceMetadata.TaskConflict 
							? SyntaxNodeExtensions.ConstructNameSyntax("System.Threading.Tasks.Task").WithLeadingTrivia(node.GetLeadingTrivia())
							: IdentifierName(Identifier(TriviaList(node.GetLeadingTrivia()), nameof(Task), TriviaList())),
						GenericName(
								Identifier("FromResult"))
							.WithTypeArgumentList(
								TypeArgumentList(
									SingletonSeparatedList(
										_methodResult.Symbol.ReturnsVoid
											? PredefinedType(Token(SyntaxKind.ObjectKeyword))
											: _methodNode.ReturnType.WithoutTrivia())))))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(node.WithoutLeadingTrivia()))));
		}

		private InvocationExpressionSyntax WrapInTaskFromException(ExpressionSyntax node)
		{
			return InvocationExpression(
					MemberAccessExpression(
						SyntaxKind.SimpleMemberAccessExpression,
						_namespaceMetadata.TaskConflict
							? SyntaxNodeExtensions.ConstructNameSyntax("System.Threading.Tasks.Task").WithLeadingTrivia(node.GetLeadingTrivia())
							: IdentifierName(Identifier(TriviaList(node.GetLeadingTrivia()), nameof(Task), TriviaList())),
						GenericName(
								Identifier("FromException"))
							.WithTypeArgumentList(
								TypeArgumentList(
									SingletonSeparatedList(
										_methodResult.Symbol.ReturnsVoid
											? PredefinedType(Token(SyntaxKind.ObjectKeyword))
											: _methodNode.ReturnType.WithoutTrivia())))))
				.WithArgumentList(
					ArgumentList(
						SingletonSeparatedList(
							Argument(node.WithoutLeadingTrivia()))));
		}

		private BlockSyntax RewriteFunctionBody(BlockSyntax body)
		{
			if (_methodResult.ForwardCall)
			{
				return ForwardCall(body);
			}
			if (_methodResult.Faulted)
			{
				return body;
			}
			if (_methodResult.Symbol.ReturnsVoid && body.IsReturnStatementRequired())
			{
				body = AddReturnStatement(body);
			}
			return _methodResult.WrapInTryCatch ? WrapInsideTryCatch(body) : body;
		}

		private BlockSyntax AddReturnStatement(BlockSyntax node)
		{
			return node.AddStatements(GetReturnTaskCompleted());
		}

		private ReturnStatementSyntax GetReturnTaskCompleted()
		{
			return ReturnStatement(
				Token(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)),
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					_namespaceMetadata.TaskConflict
						? SyntaxNodeExtensions.ConstructNameSyntax("System.Threading.Tasks.Task")
						: IdentifierName(nameof(Task)),
					IdentifierName("CompletedTask")),
				Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia))
			);
		}

		

		private BlockSyntax ForwardCall(BlockSyntax bodyBlock)
		{
			var methodNode = _methodNode;
			var invocation = methodNode.ForwardCall(_methodResult.Symbol, methodNode.Identifier.ValueText);
			var block = Block()
				.WithCloseBraceToken(bodyBlock.CloseBraceToken)
				.WithOpenBraceToken(bodyBlock.OpenBraceToken);
			foreach (var precondition in _methodResult.Preconditions)
			{
				block = block.AddStatements(precondition);
			}

			block = _methodResult.Symbol.ReturnsVoid
				? AddReturnStatement(block.AddStatements(ExpressionStatement(
					invocation.WithLeadingTrivia(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia)),
					Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)))))
				: block.AddStatements(ReturnStatement(
					Token(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)),
					WrapInTaskFromResult(invocation),
					Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia))));
			return WrapInsideTryCatch(block);
		}

		private BlockSyntax WrapInsideTryCatch(BlockSyntax node)
		{
			var indent = _transformResult.IndentTrivia.ToFullString();
			var innerBodyTrivia = Whitespace(_transformResult.BodyLeadingWhitespaceTrivia.ToFullString());
			var bodyLeadTrivia = TriviaList(_transformResult.LeadingWhitespaceTrivia);
			var eolTrivia = TriviaList(_transformResult.EndOfLineTrivia);

			var tryStatement = TryStatement()
				.WithTryKeyword(Token(bodyLeadTrivia, SyntaxKind.TryKeyword, eolTrivia))
				.WithBlock(Block(node.Statements.Skip(_methodResult.Preconditions.Count))
					.WithOpenBraceToken(Token(bodyLeadTrivia, SyntaxKind.OpenBraceToken, eolTrivia))
					.WithCloseBraceToken(Token(bodyLeadTrivia, SyntaxKind.CloseBraceToken, eolTrivia))
				)
				.WithCatches(SingletonList(
					CatchClause()
						.WithCatchKeyword(Token(bodyLeadTrivia, SyntaxKind.CatchKeyword, TriviaList(Space)))
						.WithDeclaration(
							CatchDeclaration(
								_namespaceMetadata.UsingSystem
									? IdentifierName(Identifier(TriviaList(), "Exception", TriviaList(Space)))
									: SyntaxNodeExtensions.ConstructNameSyntax("System.Exception", Space)
								)
								.WithIdentifier(Identifier("ex"))
								.WithCloseParenToken(Token(TriviaList(), SyntaxKind.CloseParenToken, eolTrivia))
						)
						.WithBlock(GetCatchBlock(innerBodyTrivia))
				));
			tryStatement = tryStatement.AppendIndent(indent);
			var newStatements = node.Statements.Take(_methodResult.Preconditions.Count).ToList();
			newStatements.Add(tryStatement);

			return Block(newStatements)
				.WithOpenBraceToken(node.OpenBraceToken)
				.WithCloseBraceToken(node.CloseBraceToken);
		}

		private BlockSyntax GetCatchBlock(SyntaxTrivia innerBodyTrivia)
		{
			return Block(
					SingletonList<StatementSyntax>(
						ReturnStatement()
							.WithExpression(
								InvocationExpression(
										MemberAccessExpression(
											SyntaxKind.SimpleMemberAccessExpression,
											_namespaceMetadata.TaskConflict
												? SyntaxNodeExtensions.ConstructNameSyntax("System.Threading.Tasks.Task")
												: IdentifierName(nameof(Task)),
											GenericName(
													Identifier("FromException"))
												.WithTypeArgumentList(
													TypeArgumentList(
														SingletonSeparatedList(
															_methodResult.Symbol.ReturnsVoid
																? PredefinedType(Token(SyntaxKind.ObjectKeyword))
																: _methodNode.ReturnType.WithoutTrivia())))))
									.WithArgumentList(
										ArgumentList(
											SingletonSeparatedList(
												Argument(
													IdentifierName("ex"))))))
							.WithReturnKeyword(Token(TriviaList(innerBodyTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)))
							.WithSemicolonToken(Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)))
					))
				.WithOpenBraceToken(Token(TriviaList(_transformResult.LeadingWhitespaceTrivia), SyntaxKind.OpenBraceToken, TriviaList(_transformResult.EndOfLineTrivia)))
				.WithCloseBraceToken(Token(TriviaList(_transformResult.LeadingWhitespaceTrivia), SyntaxKind.CloseBraceToken, TriviaList(_transformResult.EndOfLineTrivia)));
		}
	}
}
