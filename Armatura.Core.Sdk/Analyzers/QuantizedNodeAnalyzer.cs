using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM007: QuantizedNode Inheritance Analyzer
/// 
/// Enforces that all plugin classes must:
/// - Inherit from QuantizedNode
/// - Override ExecuteQuantumAsync() method
/// - Not use free-form async Task methods
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class QuantizedNodeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.MissingQuantizedNode,
            DiagnosticDescriptors.FreeFormAsync
        );

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null) return;

        // Only analyze classes in plugin namespaces
        if (!IsInPluginNamespace(classSymbol)) return;

        // Check if this is a plugin interface implementation
        bool isPlugin = IsPluginImplementation(classSymbol);
        if (!isPlugin) return;

        // Check if it inherits from QuantizedNode
        bool inheritsQuantizedNode = InheritsFromQuantizedNode(classSymbol);
        
        // Check if it uses CQRS pattern (IQueryPlugin/ICommandPlugin) which uses adapters
        bool usesCqrsPattern = ImplementsInterface(classSymbol, "IQueryPlugin") || 
                               ImplementsInterface(classSymbol, "ICommandPlugin");

        if (!inheritsQuantizedNode && !usesCqrsPattern)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.MissingQuantizedNode,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
        }

        // Check for free-form async methods
        if (inheritsQuantizedNode)
        {
            CheckForFreeFormAsync(context, classDeclaration, classSymbol);
        }
    }

    private static bool IsInPluginNamespace(INamedTypeSymbol classSymbol)
    {
        var namespaceName = classSymbol.ContainingNamespace?.ToString() ?? "";
        return namespaceName.Contains(".Plugins.");
    }

    private static bool IsPluginImplementation(INamedTypeSymbol classSymbol)
    {
        var pluginInterfaces = new[] { "IQueryPlugin", "ICommandPlugin", "IAsyncDisposable" };
        
        return classSymbol.AllInterfaces.Any(i => pluginInterfaces.Contains(i.Name)) ||
               classSymbol.BaseType?.Name == "QuantizedNode";
    }

    private static bool ImplementsInterface(INamedTypeSymbol classSymbol, string interfaceName)
    {
        return classSymbol.AllInterfaces.Any(i => i.Name == interfaceName);
    }

    private static bool InheritsFromQuantizedNode(INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "QuantizedNode")
                return true;
            baseType = baseType.BaseType;
        }
        return false;
    }

    private void CheckForFreeFormAsync(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
            if (methodSymbol == null) continue;

            // Skip the required ExecuteQuantumAsync method
            if (methodSymbol.Name == "ExecuteQuantumAsync" &&
                methodSymbol.IsOverride &&
                methodSymbol.DeclaredAccessibility == Accessibility.Public)
            {
                continue;
            }

            // Skip InitializeAsync and DisposeAsync (allowed lifecycle methods)
            if (methodSymbol.Name == "InitializeAsync" || methodSymbol.Name == "DisposeAsync")
            {
                continue;
            }

            // Check if method returns Task/ValueTask (free-form async)
            if (ReturnsTask(methodSymbol) && methodSymbol.DeclaredAccessibility == Accessibility.Public)
            {
                // Check if method uses async/await pattern
                if (method.Modifiers.Any(SyntaxKind.AsyncKeyword) ||
                    ContainsAsyncOperations(context, method))
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.FreeFormAsync,
                        method.Identifier.GetLocation(),
                        methodSymbol.Name
                    );
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }
    }

    private static bool ReturnsTask(IMethodSymbol methodSymbol)
    {
        var returnType = methodSymbol.ReturnType;
        return returnType.Name == "Task" || 
               returnType.Name == "ValueTask" ||
               (returnType.OriginalDefinition?.Name == "Task") ||
               (returnType.OriginalDefinition?.Name == "ValueTask");
    }

    private bool ContainsAsyncOperations(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        var body = method.Body ?? (SyntaxNode?)method.ExpressionBody;
        if (body == null) return false;

        // Check for await expressions
        var awaitExpressions = body.DescendantNodes().OfType<AwaitExpressionSyntax>();
        if (awaitExpressions.Any()) return true;

        // Check for Task.Run, Task.Delay, Task.WhenAll, etc.
        var invocations = body.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null) continue;

            var containingType = symbol.ContainingType?.Name;
            if (containingType == "Task" && symbol.IsStatic)
            {
                return true;
            }
        }

        return false;
    }
}
