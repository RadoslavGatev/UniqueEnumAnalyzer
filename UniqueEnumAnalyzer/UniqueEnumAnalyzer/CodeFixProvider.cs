using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Recommendations;

namespace UniqueEnumAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(UniqueEnumAnalyzerCodeFixProvider)), Shared]
    public class UniqueEnumAnalyzerCodeFixProvider : CodeFixProvider
    {
        private const string title = "Make enum member unique";

        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(UniqueEnumAnalyzerAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var maxValue = int.Parse(diagnostic.Properties["MaxValue"]);

            var declaration = root.FindNode(diagnosticSpan) as EnumMemberDeclarationSyntax;

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: title,
                    createChangedDocument: c => MakeEnumMemberUnique(context.Document, declaration, maxValue, c),
                    equivalenceKey: title),
                diagnostic);
        }

        private async Task<Document> MakeEnumMemberUnique(Document document, EnumMemberDeclarationSyntax enumMember, int maxValue, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var literal = SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(maxValue + 1));
            var equalsValue = SyntaxFactory.EqualsValueClause(literal);

            var newMember = enumMember.WithEqualsValue(equalsValue);

            var newRoot = root.ReplaceNode(enumMember, newMember);
            var newDocument = document.WithSyntaxRoot(newRoot);
            return newDocument;
        }
    }
}