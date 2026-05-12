using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hecton8.PlatformNeutralityAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class PlatformNeutralityAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor BackslashPathLiteral = new DiagnosticDescriptor(
            "H8POSIX001",
            "Hardcoded backslash path literal",
            "Hardcoded backslash path literal blocks POSIX path neutrality: '{0}'",
            "PlatformNeutrality",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor PathCombineUsage = new DiagnosticDescriptor(
            "H8POSIX002",
            "Path.Combine requires platform PAL review",
            "Path.Combine usage must be routed through the HECTON platform path PAL for persisted/runtime paths",
            "PlatformNeutrality",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor Win32DllImport = new DiagnosticDescriptor(
            "H8POSIX003",
            "Windows-only native import",
            "Windows-only native import is forbidden for Steam Deck/POSIX builds: '{0}'",
            "PlatformNeutrality",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor WindowsOnlyApi = new DiagnosticDescriptor(
            "H8POSIX004",
            "Windows-only namespace or API",
            "Windows-only namespace/API is forbidden for platform-neutral code: '{0}'",
            "PlatformNeutrality",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly string[] WindowsDlls =
        {
            "kernel32.dll",
            "user32.dll",
            "gdi32.dll",
            "winmm.dll",
            "shell32.dll",
            "advapi32.dll",
            "ole32.dll",
        };

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(BackslashPathLiteral, PathCombineUsage, Win32DllImport, WindowsOnlyApi);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeStringLiteral, SyntaxKind.StringLiteralExpression);
            context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeAttribute, SyntaxKind.Attribute);
            context.RegisterSyntaxNodeAction(AnalyzeUsing, SyntaxKind.UsingDirective);
            context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        }

        private static void AnalyzeStringLiteral(SyntaxNodeAnalysisContext context)
        {
            LiteralExpressionSyntax literal = (LiteralExpressionSyntax)context.Node;
            string? value = literal.Token.ValueText;
            if (string.IsNullOrEmpty(value) || value.IndexOf('\\') < 0)
                return;

            context.ReportDiagnostic(Diagnostic.Create(
                BackslashPathLiteral,
                literal.GetLocation(),
                Truncate(value, 96)));
        }

        private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
        {
            InvocationExpressionSyntax invocation = (InvocationExpressionSyntax)context.Node;
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                return;

            if (!string.Equals(memberAccess.Name.Identifier.ValueText, "Combine", StringComparison.Ordinal))
                return;

            string owner = memberAccess.Expression.ToString();
            if (string.Equals(owner, "Path", StringComparison.Ordinal) ||
                string.Equals(owner, "System.IO.Path", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(PathCombineUsage, memberAccess.Name.GetLocation()));
            }
        }

        private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
        {
            AttributeSyntax attribute = (AttributeSyntax)context.Node;
            string attributeName = attribute.Name.ToString();
            if (!attributeName.EndsWith("DllImport", StringComparison.Ordinal) &&
                !attributeName.EndsWith("DllImportAttribute", StringComparison.Ordinal))
            {
                return;
            }

            if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0)
                return;

            ExpressionSyntax firstArgument = attribute.ArgumentList.Arguments[0].Expression;
            if (firstArgument is not LiteralExpressionSyntax literal)
                return;

            string? libraryName = literal.Token.ValueText;
            if (string.IsNullOrEmpty(libraryName))
                return;

            if (IsWindowsDll(libraryName) || libraryName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Win32DllImport,
                    literal.GetLocation(),
                    libraryName));
            }
        }

        private static void AnalyzeUsing(SyntaxNodeAnalysisContext context)
        {
            UsingDirectiveSyntax usingDirective = (UsingDirectiveSyntax)context.Node;
            string namespaceName = usingDirective.Name?.ToString() ?? string.Empty;
            if (IsWindowsNamespace(namespaceName))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WindowsOnlyApi,
                    usingDirective.GetLocation(),
                    namespaceName));
            }
        }

        private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
        {
            MemberAccessExpressionSyntax memberAccess = (MemberAccessExpressionSyntax)context.Node;
            string text = memberAccess.ToString();
            if (string.Equals(text, "Environment.SpecialFolder.LocalApplicationData", StringComparison.Ordinal) ||
                string.Equals(text, "System.Environment.SpecialFolder.LocalApplicationData", StringComparison.Ordinal))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    WindowsOnlyApi,
                    memberAccess.GetLocation(),
                    text));
            }
        }

        private static bool IsWindowsDll(string libraryName)
        {
            for (int i = 0; i < WindowsDlls.Length; i++)
            {
                if (string.Equals(libraryName, WindowsDlls[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsWindowsNamespace(string namespaceName)
        {
            return string.Equals(namespaceName, "Microsoft.Win32", StringComparison.Ordinal) ||
                   string.Equals(namespaceName, "System.Drawing", StringComparison.Ordinal) ||
                   string.Equals(namespaceName, "System.Windows.Forms", StringComparison.Ordinal);
        }

        private static string Truncate(string value, int max)
        {
            if (value.Length <= max)
                return value;

            return value.Substring(0, max) + "...";
        }
    }
}
