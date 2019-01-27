using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace MappingGenerator
{
    public static class ClassGenerationExtensions
    {
        public static CompilationUnitSyntax GetCompilationUnit(this NamespaceDeclarationSyntax namespaceDeclaration)
        {
            return namespaceDeclaration.Ancestors().OfType<CompilationUnitSyntax>().FirstOrDefault();
        }

        public static NamespaceDeclarationSyntax GetNamespaceDeclaration(this ClassDeclarationSyntax classDeclaration)
        {
            return classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        }

        public static NamespaceDeclarationSyntax GetNamespaceDeclaration(this CompilationUnitSyntax unit)
        {
            return unit.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.NamespaceDeclaration)) as NamespaceDeclarationSyntax;
        }

        public static NamespaceDeclarationSyntax GetOrCreateNamespaceDeclaration(this CompilationUnitSyntax node, string classNamespace)
        {
            if (!(node.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.NamespaceDeclaration)) is NamespaceDeclarationSyntax namespaceDescaration))
            {
                namespaceDescaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(classNamespace)).NormalizeWhitespace();
                node = node.AddMembers(namespaceDescaration);
                namespaceDescaration = node.ChildNodes().FirstOrDefault(x => x.IsKind(SyntaxKind.NamespaceDeclaration)) as NamespaceDeclarationSyntax;
            }

            return namespaceDescaration;
        }

        public static ClassDeclarationSyntax GetClassDeclaration(this NamespaceDeclarationSyntax namespaceDeclaration,
            string className)
        {
            var classes = namespaceDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.ClassDeclaration));

            foreach (var item in classes)
            {
                var asClass = item as ClassDeclarationSyntax;
                if (asClass.Identifier.ValueText == className)
                {
                    return asClass;
                }
            }

            return null;
        }

        public static ClassDeclarationSyntax GetOrCreateClassDeclaration(this NamespaceDeclarationSyntax namespaceDeclaration,
            string className, string baseType)
        {
            var classDeclaration = namespaceDeclaration.GetClassDeclaration(className);

            if (classDeclaration == null)
            {
                // create it
                var item = SyntaxFactory.ClassDeclaration(className)
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseType)))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    .NormalizeWhitespace()
                    // Annotate that this node should be formatted
                    .WithAdditionalAnnotations(Formatter.Annotation);
                namespaceDeclaration = namespaceDeclaration.AddMembers(new[] {item});
                classDeclaration = namespaceDeclaration.GetClassDeclaration(className);
            }

            return classDeclaration;
        }

        public static MethodDeclarationSyntax GetMethodDeclaration(this ClassDeclarationSyntax classDeclaration, string methodName,
            int paramCount)
        {
            var methods = classDeclaration.ChildNodes().Where(x => x.IsKind(SyntaxKind.MethodDeclaration)).Cast<MethodDeclarationSyntax>();
            var mapMethod = methods.FirstOrDefault(x => x.Identifier.ValueText == methodName && x.ParameterList.Parameters.Count == paramCount);
            return mapMethod;
        }

        public static async Task<MethodDeclarationSyntax> GetMethodDeclarationAsync(this Document document, string className,
            string methodName, int paramCount)
        {
            var root = await document.GetSyntaxRootAsync() as CompilationUnitSyntax;
            return root?
                   .GetNamespaceDeclaration()?
                   .GetClassDeclaration(className)?
                   .GetMethodDeclaration(methodName, paramCount);
        }

        public static CompilationUnitSyntax CreateCompilationUnitWithNamespace(string namespaceName)
        {
            var unit = SyntaxFactory.CompilationUnit();

            unit = unit.AddUsings(
                SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")));

            return unit;
        }

        public static string GetMappingSourceFromComment(this MemberDeclarationSyntax classDeclaration)
        {
            if (classDeclaration.HasLeadingTrivia)
            {
                var trivia = classDeclaration.GetLeadingTrivia();
                var singleLines = trivia.Where(x => x.Kind() == SyntaxKind.SingleLineCommentTrivia);
                foreach (var line in singleLines)
                {
                    var item = line.ToString();
                    if (item.StartsWith("// MappingSource:"))
                    {
                        return item.Substring(("// MappingSource:").Length).Trim();
                    }
                }
            }

            return "";
        }
    }
}
