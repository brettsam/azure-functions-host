// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.


using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.Azure.WebJobs.Script.Description.DotNet.Compilation.CSharp.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    internal class SyncMethodAnalyzer : DiagnosticAnalyzer
    {
        private const string Title = "Use Async methods when possible.";
        private const string MessageFormat = "The '{0}' method has an Async version: '{1}'.";
        private readonly DiagnosticDescriptor _supportedRule;

        public SyncMethodAnalyzer()
        {
            _supportedRule = new DiagnosticDescriptor(DotNetConstants.UseAsyncMethod,
               Title, MessageFormat, "Function", DiagnosticSeverity.Warning, true);
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(_supportedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            var invocation = (InvocationExpressionSyntax)context.Node;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccessCall)
            {
                string methodName = memberAccessCall.Name.ToString();

                INamedTypeSymbol taskSymbol = context.SemanticModel.Compilation.GetTypeByMetadataName(typeof(Task).FullName);

                // If the return type is not Task or Task<>, let's see if we can find one that is.
                IMethodSymbol methodCallSymbol = context.SemanticModel.GetSymbolInfo(memberAccessCall).Symbol as IMethodSymbol;
                INamedTypeSymbol returnTypeSymbol = methodCallSymbol.ReturnType as INamedTypeSymbol;
                if (!methodCallSymbol.ReturnType.Equals(taskSymbol) &&
                    !(returnTypeSymbol.IsGenericType && returnTypeSymbol.BaseType.Equals(taskSymbol)))
                {
                    TypeInfo owningType = context.SemanticModel.GetTypeInfo(memberAccessCall.Expression);

                    IMethodSymbol asyncMember = owningType.Type.GetMembers($"{methodName}Async")
                        .OfType<IMethodSymbol>()
                        .Where(p => p.ReturnType.Equals(taskSymbol) || p.ReturnType.BaseType.Equals(taskSymbol))
                        .FirstOrDefault();

                    if (asyncMember != null)
                    {
                        var diagnostic = Diagnostic.Create(_supportedRule, context.Node.GetLocation(), methodName, asyncMember.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }
    }
}
