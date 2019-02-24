﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AsyncGenerator.Analyzation;
using AsyncGenerator.Core.Configuration;
using AsyncGenerator.Core.Plugins;
using AsyncGenerator.Core.Transformation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace AsyncGenerator.Transformation.Internal
{
	internal class YieldRewriter : CSharpSyntaxRewriter
	{
		private readonly IMethodOrAccessorTransformationResult _transformResult;

		public YieldRewriter(IMethodOrAccessorTransformationResult transformResult)
		{
			_transformResult = transformResult;
		}

		// TODO: handle name collision
		public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
		{
			TypeSyntax yieldType;
			if (node.ReturnType is GenericNameSyntax enumerableReturnType) // IEnumerable<T>
			{
				yieldType = enumerableReturnType.TypeArgumentList.Arguments.First();
			}
			else // IEnumerable
			{
				yieldType = PredefinedType(Token(SyntaxKind.ObjectKeyword));
			}

			var variable = LocalDeclarationStatement(
				VariableDeclaration(
					IdentifierName(Identifier(TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), "var", TriviaList(Space))))
					.WithVariables(
						SingletonSeparatedList(
							VariableDeclarator(Identifier(TriviaList(), "yields", TriviaList(Space)))
								.WithInitializer(
									EqualsValueClause(
										ObjectCreationExpression(
												GenericName(
														Identifier(TriviaList(), "List", TriviaList()))
													.WithTypeArgumentList(
														TypeArgumentList(
															SingletonSeparatedList(yieldType))))
											.WithNewKeyword(
												Token(TriviaList(), SyntaxKind.NewKeyword, TriviaList(Space)))
											.WithArgumentList(ArgumentList()))
										.WithEqualsToken(Token(TriviaList(), SyntaxKind.EqualsToken, TriviaList(Space)))))))
				.WithSemicolonToken(Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)));

			node = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
			return node.WithBody(
				node.Body.WithStatements(
					node.Body.Statements
						.Insert(0, variable)
						.Add(GetYieldReturnStatement())));
		}

		public override SyntaxNode VisitYieldStatement(YieldStatementSyntax node)
		{
			if (node.IsKind(SyntaxKind.YieldBreakStatement))
			{
				return GetYieldReturnStatement(node.GetLeadingTrivia());
			}

			var addYield = ExpressionStatement(
				InvocationExpression(
						MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							IdentifierName(Identifier(TriviaList(node.GetLeadingTrivia()), "yields", TriviaList())),
							IdentifierName("Add")))
					.WithArgumentList(
						ArgumentList(
							SingletonSeparatedList(
								Argument(node.Expression)))))
				.WithSemicolonToken(Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)));
			return addYield;
		}

		private ReturnStatementSyntax GetYieldReturnStatement(SyntaxTriviaList? leadingTrivia = null)
		{
			return ReturnStatement(IdentifierName("yields"))
				.WithReturnKeyword(Token(leadingTrivia ?? TriviaList(_transformResult.BodyLeadingWhitespaceTrivia), SyntaxKind.ReturnKeyword, TriviaList(Space)))
				.WithSemicolonToken(Token(TriviaList(), SyntaxKind.SemicolonToken, TriviaList(_transformResult.EndOfLineTrivia)));
		}
	}
}
