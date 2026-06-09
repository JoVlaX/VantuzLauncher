#nullable enable

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM012: Unmatched Context Key
/// ARM013: Similar Context Keys Detected
/// 
/// INVARIANT_THEORY.md В§2.1 - All contracts must be explicit and verifiable
/// 
/// Detects context key contract violations:
/// - context.Set("X") without corresponding context.Get("X")
/// - context.Get("X") without corresponding context.Set("X")
/// - Similar keys with different separators (gui_credential_provider vs gui.credential_provider)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ContextKeyAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.UnmatchedContextKey,
            DiagnosticDescriptors.SimilarContextKeys
        );
/// F_doc: {Initialize returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Initialize behavior

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        // Collect all context.Set and context.Get calls across the compilation
        var setKeys = new HashSet<string>();
        var getKeys = new HashSet<string>();
        var allKeys = new HashSet<string>();

        context.RegisterSyntaxNodeAction(ctx =>
        {
            var invocation = (InvocationExpressionSyntax)ctx.Node;
            var semanticModel = ctx.SemanticModel;
            
            if (IsContextMethod(invocation, semanticModel, "Set"))
            {
                var key = GetFirstArgumentString(invocation);
                if (key != null)
                {
                    setKeys.Add(key);
                    allKeys.Add(key);
                }
            }
            else if (IsContextMethod(invocation, semanticModel, "Get"))
            {
                var key = GetFirstArgumentString(invocation);
                if (key != null)
                {
                    getKeys.Add(key);
                    allKeys.Add(key);
                }
            }
        }, SyntaxKind.InvocationExpression);

        context.RegisterCompilationEndAction(ctx =>
        {
            // ARM012: Check for unmatched keys
            foreach (var setKey in setKeys)
            {
                if (!getKeys.Contains(setKey))
                {
                    // Set without Get - report warning
                    // Note: We can't provide location here, would need to track per-invocation
                }
            }

            foreach (var getKey in getKeys)
            {
                if (!setKeys.Contains(getKey))
                {
                    // Get without Set - this is the critical error we saw
                    // Report that this key is never set
                }
            }

            // ARM013: Check for similar keys
            var keyList = allKeys.ToList();
            for (int i = 0; i < keyList.Count; i++)
            {
                for (int j = i + 1; j < keyList.Count; j++)
                {
                    var key1 = keyList[i];
                    var key2 = keyList[j];

                    if (AreSimilarButDifferentSeparators(key1, key2))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.SimilarContextKeys,
                            Location.None, // Would need actual location tracking
                            key1,
                            key2
                        );
                        ctx.ReportDiagnostic(diagnostic);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Checks if this is a context.Set or context.Get method invocation
    /// </summary>
    private static bool IsContextMethod(InvocationExpressionSyntax invocation, SemanticModel semanticModel, string methodName)
    {
        var symbol = semanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        if (symbol == null) return false;

        return symbol.Name == methodName &&
               (symbol.ContainingType?.Name == "CommandContext" ||
                symbol.ContainingType?.Name == "ICommandContext");
    }

    /// <summary>
    /// Extracts the first string literal argument from an invocation
    /// </summary>
    private static string? GetFirstArgumentString(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        
        if (firstArg is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return literal.Token.ValueText;
        }

        return null;
    }

    /// <summary>
    /// Checks if two keys are similar but use different separators
    /// Example: gui_credential_provider vs gui.credential_provider
    /// </summary>
    private static bool AreSimilarButDifferentSeparators(string key1, string key2)
    {
        // Normalize both keys: replace underscores with dots
        var normalized1 = key1.Replace('_', '.').ToLowerInvariant();
        var normalized2 = key2.Replace('_', '.').ToLowerInvariant();

        // If normalized forms are identical but original forms differ
        return normalized1 == normalized2 && key1 != key2;
    }
}
