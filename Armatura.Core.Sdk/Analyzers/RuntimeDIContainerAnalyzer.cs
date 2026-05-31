using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM009: Runtime DI Container Analyzer
/// 
/// INVARIANT_THEORY.md:159 - FORBIDDEN: runtime DI containers
/// 
/// Detects violations of the compile-time dependency rule:
/// - ServiceCollection.BuildServiceProvider() calls
/// - Runtime service provider creation
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class RuntimeDIContainerAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.RuntimeDIContainer);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Get the method symbol
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null) return;

        // Check for ServiceCollection.BuildServiceProvider()
        var containingType = symbol.ContainingType?.Name;
        var methodName = symbol.Name;

        if (containingType == "ServiceCollection" && methodName == "BuildServiceProvider")
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.RuntimeDIContainer,
                invocation.GetLocation(),
                methodName
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // Check for IServiceProvider creation through other means
        if (IsRuntimeDIServiceProviderCreation(symbol))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.RuntimeDIContainer,
                invocation.GetLocation(),
                methodName
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool IsRuntimeDIServiceProviderCreation(IMethodSymbol symbol)
    {
        var containingType = symbol.ContainingType?.Name;
        var methodName = symbol.Name;

        // ServiceProvider construction patterns
        if (containingType == "ServiceProvider" && methodName == ".ctor")
            return true;

        // ActivatorUtilities patterns that bypass compile-time checks
        if (containingType == "ActivatorUtilities" && 
            (methodName.StartsWith("CreateInstance") || methodName.StartsWith("GetService")))
            return true;

        return false;
    }
}
