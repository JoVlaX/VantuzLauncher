using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM011: Component Scope Invariant Violation
/// 
/// INVARIANT_THEORY.md §2.3 - Component Scope Invariant
/// 
/// Detects violations of the level hierarchy:
/// - Level 4 (Product) implementing Level 2 (Plugin) interfaces
/// - Level N components may only implement interfaces from Level N or N-1
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ComponentScopeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.ComponentScopeViolation);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
    }

    private void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        
        // Get semantic model for symbol information
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        
        if (classSymbol == null) return;

        // Determine the architectural level of this component
        var componentLevel = DetermineComponentLevel(classSymbol);
        if (componentLevel == null) return; // Not a Vantuz component

        // Check all implemented interfaces
        foreach (var interfaceSymbol in classSymbol.AllInterfaces)
        {
            var interfaceLevel = DetermineInterfaceLevel(interfaceSymbol);
            if (interfaceLevel == null) continue; // Not a Vantuz interface

            // Check invariant: Level N may only implement Level N or N-1
            if (interfaceLevel < componentLevel - 1)
            {
                // Violation: implementing interface from too low level
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.ComponentScopeViolation,
                    classDeclaration.Identifier.GetLocation(),
                    componentLevel,
                    classSymbol.Name,
                    interfaceLevel,
                    interfaceSymbol.Name
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    /// Determines the architectural level of a component based on its namespace
    /// Level 1: Vantuz.Core (Contracts)
    /// Level 2: Vantuz.Host, Vantuz.Plugins.* (Plugins)
    /// Level 3: Vantuz.Builder (Tools)
    /// Level 4: VantuzLauncher (Application)
    /// </summary>
    private static int? DetermineComponentLevel(INamedTypeSymbol symbol)
    {
        var namespaceName = symbol.ContainingNamespace?.ToString() ?? "";

        // Level 1: Core contracts
        if (namespaceName.StartsWith("Vantuz.Core"))
            return 1;

        // Level 2: Host and Plugins
        if (namespaceName.StartsWith("Vantuz.Host") ||
            namespaceName.StartsWith("Vantuz.Plugins"))
            return 2;

        // Level 3: Builder/Tools
        if (namespaceName.StartsWith("Vantuz.Builder"))
            return 3;

        // Level 4: Root application
        if (namespaceName == "VantuzLauncher")
            return 4;

        return null; // Not a Vantuz component
    }

    /// <summary>
    /// Determines the architectural level of an interface
    /// </summary>
    private static int? DetermineInterfaceLevel(INamedTypeSymbol interfaceSymbol)
    {
        var namespaceName = interfaceSymbol.ContainingNamespace?.ToString() ?? "";

        // Level 1: Core contracts
        if (namespaceName.StartsWith("Vantuz.Core"))
            return 1;

        // Level 2: Host and Plugins
        if (namespaceName.StartsWith("Vantuz.Host") ||
            namespaceName.StartsWith("Vantuz.Plugins"))
            return 2;

        // Level 3: Builder
        if (namespaceName.StartsWith("Vantuz.Builder"))
            return 3;

        return null; // Not a Vantuz interface
    }
}
