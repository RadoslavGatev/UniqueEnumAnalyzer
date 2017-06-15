using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace UniqueEnumAnalyzer
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class UniqueEnumAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "UniqueEnumAnalyzer";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Naming";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.EnumDeclaration);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext obj)
        {
            var enumDeclaration = obj.Node as EnumDeclarationSyntax;
            var semanticModel = obj.SemanticModel;

            var attributes = enumDeclaration.AttributeLists.SelectMany(x => x.Attributes);

            var isUniqueAttributeApplied = attributes.Any(x => (x.Name as IdentifierNameSyntax)?.Identifier.ValueText == "Description" &&
                  x.ArgumentList.Arguments.Any(a => (a.Expression as LiteralExpressionSyntax)?.Token.ValueText == "UniqueEnum"));
            if (!isUniqueAttributeApplied)
            {
                return;
            }

            var membersDict = new HashSet<int>();
            var maxValue = 0;
            var currentValue = 0;

            EnumMemberDeclarationSyntax wrongMember = null;
            foreach (var member in enumDeclaration.Members)
            {
                if (member.EqualsValue != null)
                {
                    currentValue = int.Parse(semanticModel.GetConstantValue(member.EqualsValue.Value).Value.ToString());
                }
                else
                {
                    currentValue++;
                }

                if (maxValue < currentValue)
                {
                    maxValue = currentValue;
                }
                if (wrongMember == null && !membersDict.Add(currentValue))
                {
                    wrongMember = member;
                }
            }

            if (wrongMember != null)
            {
                var dictionary = new Dictionary<string, string>
                {
                    { "MaxValue", maxValue.ToString() }
                };

                var diagnostic = Diagnostic.Create(Rule, wrongMember.GetLocation(), dictionary.ToImmutableDictionary(), wrongMember.Identifier.ValueText);
                obj.ReportDiagnostic(diagnostic);
            }
        }
    }
}
