﻿using Geeks.GeeksProductivityTools;
using Geeks.GeeksProductivityTools.Menus.Cleanup;
using Geeks.VSIX.TidyCSharp.Menus.Cleanup.SyntaxNodeExtractors;
using Geeks.VSIX.TidyCSharp.Menus.Cleanup.SyntaxNodeValidators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geeks.VSIX.TidyCSharp.Cleanup
{
    public class MSharpModelCleaner : CodeCleanerCommandRunnerBase, ICodeCleaner
    {
        public override SyntaxNode CleanUp(SyntaxNode initialSourceNode)
        {
            if (App.DTE.ActiveDocument.ProjectItem.ProjectItems.ContainingProject.Name == "#Model")
                return ChangeMethodHelper(initialSourceNode);
            return initialSourceNode;
        }
        SyntaxNode ChangeMethodHelper(SyntaxNode initialSourceNode)
        {
            initialSourceNode = new LocalTimeRewriter(ProjectItemDetails.SemanticModel)
                .Visit(initialSourceNode);
            initialSourceNode = this.RefreshResult(initialSourceNode);
            initialSourceNode = new CascadeDeleteRewriter(ProjectItemDetails.SemanticModel)
                .Visit(initialSourceNode);
            initialSourceNode = this.RefreshResult(initialSourceNode);
            initialSourceNode = new CalculatedGetterRewriter(ProjectItemDetails.SemanticModel)
                .Visit(initialSourceNode);
            initialSourceNode = this.RefreshResult(initialSourceNode);
            initialSourceNode = new TransientDatabaseModeRewriter(ProjectItemDetails.SemanticModel)
                .Visit(initialSourceNode);
            return initialSourceNode;
        }

        class LocalTimeRewriter : CSharpSyntaxRewriter
        {
            SemanticModel semanticModel;
            public LocalTimeRewriter(SemanticModel semanticModel) => this.semanticModel = semanticModel;

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                var methodName = methodSymbol?.Name;
                var methodType = methodSymbol?.ReturnType.OriginalDefinition?.ToString();
                var acceptedArguments = new string[]
                {
                    "\"c#:LocalTime.UtcNow\"","\"c#:LocalTime.Now\"",
                    "cs(\"LocalTime.UtcNow\")","cs(\"LocalTime.Now\")"
                };
                if (methodName == "Default" && methodType == "MSharp.DateTimeProperty")
                {
                    if (node.ArgumentsCountShouldBe(1) &&
                        node.FirstArgumentShouldBeIn(acceptedArguments))
                    {
                        return SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression, node.GetLeftSideExpression(), SyntaxFactory.IdentifierName("DefaultToNow")),
                            SyntaxFactory.ArgumentList());
                    }
                }
                return base.VisitInvocationExpression(node);
            }
        }
        class CascadeDeleteRewriter : CSharpSyntaxRewriter
        {
            SemanticModel semanticModel;
            public CascadeDeleteRewriter(SemanticModel semanticModel) => this.semanticModel = semanticModel;
            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var methodSymbol = (semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol);
                var methodName = methodSymbol?.Name;

                if (node.ArgumentsCountShouldBe(1) &&
                    node.FirstArgumentShouldBe("CascadeAction.CascadeDelete") &&
                    methodName == "OnDelete")
                {
                    return SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                node.GetLeftSideExpression(),
                                SyntaxFactory.IdentifierName("CascadeDelete")),
                            SyntaxFactory.ArgumentList());
                }
                return base.VisitInvocationExpression(node);
            }
        }
        class CalculatedGetterRewriter : CSharpSyntaxRewriter
        {
            SemanticModel semanticModel;
            public CalculatedGetterRewriter(SemanticModel semanticModel) => this.semanticModel = semanticModel;

            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var calculatedInvocation = node.DescendantNodesAndSelfOfType<InvocationExpressionSyntax>()
                        .Where(x => (semanticModel.GetSymbolInfo(x).Symbol as IMethodSymbol)?.Name == "Calculated").FirstOrDefault();
                var getterInvocation = node.DescendantNodesAndSelfOfType<InvocationExpressionSyntax>()
                        .Where(x => (semanticModel.GetSymbolInfo(x).Symbol as IMethodSymbol)?.Name == "Getter").FirstOrDefault();
                if (calculatedInvocation == null || getterInvocation == null)
                    return base.VisitInvocationExpression(node);

                if ((calculatedInvocation.ArgumentsCountShouldBe(0) ||
                    (calculatedInvocation.ArgumentsCountShouldBe(1) &&
                    calculatedInvocation.FirstArgumentShouldBe("true"))) &&
                    getterInvocation.ArgumentsCountShouldBe(1))
                {
                    var newNode = node.ReplaceNodes(
                        node.DescendantNodesAndSelfOfType<InvocationExpressionSyntax>()
                        .Where(x => x.MethodNameShouldBeIn(new string[] { "Getter", "Calculated" })),
                        (nde1, nde2) =>
                        {
                            if (nde1.MethodNameShouldBe("Calculated"))
                                return nde1.GetLeftSideExpression();
                            else if (nde1.MethodNameShouldBe("Getter"))
                                return SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                    nde2.GetLeftSideExpression(), SyntaxFactory.IdentifierName("CalculatedFrom")),
                                    nde1.ArgumentList);
                            else return nde2;
                        });
                    return newNode;
                }
                return base.VisitInvocationExpression(node);
            }
        }
        class TransientDatabaseModeRewriter : CSharpSyntaxRewriter
        {
            SemanticModel semanticModel;
            public TransientDatabaseModeRewriter(SemanticModel semanticModel) => this.semanticModel = semanticModel;
            public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                var methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;
                var methodName = methodSymbol?.Name;

                if (node.ArgumentsCountShouldBe(1) &&
                    node.FirstArgumentShouldBe("DatabaseOption.Transient") &&
                    methodName == "DatabaseMode")
                {
                    var newNode = node.ReplaceNode(node.ArgumentList, SyntaxFactory.ArgumentList());

                    newNode = newNode.ReplaceNode(newNode.DescendantNodesAndSelfOfType<IdentifierNameSyntax>()
                            .FirstOrDefault(x => x.Identifier.ToString() == "DatabaseMode"),
                                SyntaxFactory.IdentifierName("Transient"));
                    return newNode;
                }
                return base.VisitInvocationExpression(node);
            }
        }
    }
}
