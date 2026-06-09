using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM010: Plugin-side Resource Cleanup Analyzer
/// 
/// INVARIANT_THEORY.md:191 - FORBIDDEN: plugin-side resource cleanup
/// 
/// Detects manual resource cleanup in plugins that should be host-managed.
/// All resource lifecycle must be managed through Host DAG Ref Counting.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ResourceCleanupAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.PluginResourceCleanup);
/// F_doc: {Initialize returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Initialize behavior

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeUsingStatement, SyntaxKind.UsingStatement);
    }

    private void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null) return;

        // Check if this is in a plugin class
        var containingClass = invocation.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass == null) return;

        var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
        if (!IsInPluginNamespace(classSymbol)) return;

        var containingType = symbol.ContainingType?.Name;
        var methodName = symbol.Name;

        // ALLOWED: System disposable types (managed by runtime)
        var allowedSystemTypes = new[] { "HttpClient", "HttpMessageInvoker", "SemaphoreSlim", "CancellationTokenSource" };
        if (allowedSystemTypes.Contains(containingType))
        {
            return;
        }

        // FORBIDDEN: Direct Dispose() calls on resources
        if (methodName == "Dispose" && containingType != "IAsyncDisposable")
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.PluginResourceCleanup,
                invocation.GetLocation(),
                $"{containingType}.{methodName}()"
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // FORBIDDEN: File.Close(), Stream.Close() etc.
        var forbiddenCloseTypes = new[] { "FileStream", "StreamWriter", "StreamReader", "BinaryWriter", "BinaryReader" };
        if (methodName == "Close" && forbiddenCloseTypes.Contains(containingType))
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.PluginResourceCleanup,
                invocation.GetLocation(),
                $"{containingType}.{methodName}()"
            );
            context.ReportDiagnostic(diagnostic);
        }
    }

    private void AnalyzeUsingStatement(SyntaxNodeAnalysisContext context)
    {
        var usingStatement = (UsingStatementSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        // Check if this is in a plugin class
        var containingClass = usingStatement.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
        if (containingClass == null) return;

        var classSymbol = semanticModel.GetDeclaredSymbol(containingClass);
        if (!IsInPluginNamespace(classSymbol)) return;

        // FORBIDDEN: using statements that manage plugin-side resources
        // The host must manage all resource lifecycles through DAG Ref Counting
        var declaration = usingStatement.Declaration;
        if (declaration != null)
        {
            foreach (var variable in declaration.Variables)
            {
                var variableSymbol = semanticModel.GetDeclaredSymbol(variable) as ILocalSymbol;
                if (variableSymbol?.Type != null)
                {
                    var typeName = variableSymbol.Type.Name;
                    // ALLOWED: HttpClient is managed by runtime
                    var allowedSystemTypes = new[] { "HttpClient", "HttpMessageInvoker", "SemaphoreSlim", "CancellationTokenSource" };
                    if (allowedSystemTypes.Contains(typeName))
                    {
                        return;
                    }
                    var forbiddenTypes = new[] { "FileStream", "StreamWriter", "StreamReader", "BinaryWriter", "BinaryReader", "WebClient" };
                    
                    if (forbiddenTypes.Contains(typeName))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.PluginResourceCleanup,
                            usingStatement.UsingKeyword.GetLocation(),
                            $"using {typeName}"
                        );
                        context.ReportDiagnostic(diagnostic);
                        return;
                    }
                }
            }
        }
    }

    private static bool IsInPluginNamespace(INamedTypeSymbol classSymbol)
    {
        var namespaceName = classSymbol?.ContainingNamespace?.ToString() ?? "";
        return namespaceName.Contains(".Plugins.");
    }
}
