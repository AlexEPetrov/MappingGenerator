using Microsoft.CodeAnalysis.CodeActions;
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Editing;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MappingGenerator.ExternalMapper
{
    public class MappingClassPropertiesCodeAction : CodeAction
    {
        private readonly ClassDeclarationSyntax _classDeclaration;
        private readonly string _title;
        private readonly string _fileName;
        private readonly Document _document;
        private readonly string _mappingSourceClass;

        public MappingClassPropertiesCodeAction(Document document, ClassDeclarationSyntax classDeclaration, string mappingSourceClass)
        {
            _classDeclaration = classDeclaration;
            _fileName = document.FilePath;
            _title = CreateDisplayText();
            _document = document;
            _mappingSourceClass = mappingSourceClass;
        }

        private string CreateDisplayText()
        {
            return "Mapping: Create mapping properties";
        }

        public override string Title => _title;

        private MappingClassEditor GetEditor(CancellationToken cancellationToken, string mappingSource)
        {
            return new MappingClassEditor(_document, _classDeclaration, mappingSource, cancellationToken);
        }

        protected override async Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            var editor = GetEditor(cancellationToken, _mappingSourceClass);
            return await editor.GetOperationsAsync().ConfigureAwait(false);
        }
    }
}
