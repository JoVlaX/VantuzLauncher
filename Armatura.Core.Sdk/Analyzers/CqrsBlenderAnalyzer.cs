#nullable enable

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Armatura.Core.Sdk.Analyzers;

/// <summary>
/// ARM005: CQRS Blender Detection Analyzer
/// 
/// Detects violations of the CQRS Separation Invariant:
/// - Class implements both IQueryPlugin and ICommandPlugin
/// - Query class contains mutation operations (File.Write, Directory.Create, etc.)
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class CqrsBlenderAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.CqrsBlender);
/// F_doc: {Initialize returns incorrect result or throws unexpectedly} E_doc: Unit test or static analysis verifies Initialize behavior

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

        // Check if class implements both IQueryPlugin and ICommandPlugin
        bool implementsQuery = ImplementsInterface(classSymbol, "IQueryPlugin");
        bool implementsCommand = ImplementsInterface(classSymbol, "ICommandPlugin");

        if (implementsQuery && implementsCommand)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.CqrsBlender,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name
            );
            context.ReportDiagnostic(diagnostic);
            return;
        }

        // If it's a Query class, check for mutation operations
        if (implementsQuery)
        {
            CheckQueryForMutations(context, classDeclaration, classSymbol);
        }
    }

    private static bool ImplementsInterface(INamedTypeSymbol classSymbol, string interfaceName)
    {
        return classSymbol.AllInterfaces.Any(i => i.Name == interfaceName);
    }

    private void CheckQueryForMutations(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        // Check methods for mutation patterns
        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            var methodSymbol = context.SemanticModel.GetDeclaredSymbol(method);
            if (methodSymbol == null) continue;

            // Check method body for mutation calls
            if (method.Body != null)
            {
                CheckMethodBodyForMutations(context, method.Body, classSymbol.Name, method.Identifier.GetLocation());
            }

            // Check expression body
            if (method.ExpressionBody != null)
            {
                CheckExpressionBodyForMutations(context, method.ExpressionBody, classSymbol.Name, method.Identifier.GetLocation());
            }
        }
    }

    private void CheckMethodBodyForMutations(SyntaxNodeAnalysisContext context, SyntaxNode body, string className, Location location)
    {
        // Check for File.Write*, File.Delete, Directory.Create, etc.
        var invocationExpressions = body.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocationExpressions)
        {
            var symbol = context.SemanticModel.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (symbol == null) continue;

            var containingType = symbol.ContainingType?.Name;
            var methodName = symbol.Name;

            // Check for mutation operations
            if (IsMutationOperation(containingType, methodName))
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.CqrsBlender,
                    location,
                    className
                );
                context.ReportDiagnostic(diagnostic);
                return;
            }
        }
    }

    private void CheckExpressionBodyForMutations(SyntaxNodeAnalysisContext context, ArrowExpressionClauseSyntax expressionBody, string className, Location location)
    {
        CheckMethodBodyForMutations(context, expressionBody, className, location);
    }

    private static bool IsMutationOperation(string? containingType, string methodName)
    {
        if (string.IsNullOrEmpty(containingType)) return false;

        // File operations that mutate state
        if (containingType == "File")
        {
            return methodName.StartsWith("Write") ||
                   methodName.StartsWith("Append") ||
                   methodName.StartsWith("Create") ||
                   methodName.StartsWith("Delete") ||
                   methodName.StartsWith("Move") ||
                   methodName.StartsWith("Copy") ||
                   methodName == "SetAttributes";
        }

        // Directory operations that mutate state
        if (containingType == "Directory")
        {
            return methodName.StartsWith("Create") ||
                   methodName.StartsWith("Delete") ||
                   methodName.StartsWith("Move") ||
                   methodName == "SetCurrentDirectory";
        }

        // StreamWriter/FileStream mutations
        if (containingType == "StreamWriter" || containingType == "FileStream")
        {
            return methodName.StartsWith("Write") ||
                   methodName == "Flush" ||
                   methodName == "Dispose";
        }

        // HttpClient Post/Put/Delete (Query should only use Get)
        if (containingType == "HttpClient")
        {
            return methodName == "Post" ||
                   methodName == "PostAsync" ||
                   methodName == "Put" ||
                   methodName == "PutAsync" ||
                   methodName == "Delete" ||
                   methodName == "DeleteAsync" ||
                   methodName == "Patch" ||
                   methodName == "PatchAsync";
        }

        return false;
    }
}
