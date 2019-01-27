// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.LanguageServices;
using Microsoft.CodeAnalysis.Shared.Extensions;
using Roslyn.Utilities;

namespace MappingGenerator.ExternalMapper
{
        public class MappingClassEditor
        {
            private readonly Document _sourceDocument;
            private readonly ClassDeclarationSyntax _mappingTargetClassDeclaration;
            private string _mappingSource = "";

            public MappingClassEditor(Document sourceDocument, ClassDeclarationSyntax mappingTargetClassDeclaration, string mappingSource, CancellationToken cancellationToken)
            {
                _sourceDocument = sourceDocument;
                _mappingTargetClassDeclaration = mappingTargetClassDeclaration;
                CancellationToken = cancellationToken;
                _mappingSource = mappingSource;
            }

            public string GetOppositeMapperClassName()
            {
                return _mappingTargetClassDeclaration.Identifier.ToString() + "2" + MappingSourceClassWithoutNamespace  + "Mapper";
            }

            public string GetDirectMapperClassName()
            {
                return MappingSourceClassWithoutNamespace + "2" + _mappingTargetClassDeclaration.Identifier.ToString() + "Mapper";
            }

            public string MappingSourceClassWithoutNamespace
            {
                get
                {
                    var classNameWithoutNamespace = _mappingSource;
                    var idx = _mappingSource.LastIndexOf(".");
                    if (idx > -1)
                    {
                        classNameWithoutNamespace = _mappingSource.Substring(idx + 1);
                    }

                    return classNameWithoutNamespace;
                }
            }

            public string MappingTarget
            {
                get
                {
                    var namespaceDeclaration = _mappingTargetClassDeclaration.Ancestors()
                        .FirstOrDefault(x => x.IsKind(SyntaxKind.NamespaceDeclaration)) as NamespaceDeclarationSyntax;
                    if (namespaceDeclaration != null)
                    {
                        return namespaceDeclaration.Name.GetText().ToString().Trim() + "." + _mappingTargetClassDeclaration.Identifier.ToString();
                    }
                    return  _mappingTargetClassDeclaration.Identifier.ToString();
                }
            }

            public CancellationToken CancellationToken { get; set; }

            /// <summary>
            /// Given a document and a type contained in it, moves the type
            /// out to its own document. The new document's name typically
            /// is the type name, or is at least based on the type name.
            /// </summary>
            /// <remarks>
            /// The algorithm for this, is as follows:
            /// 1. Fork the original document that contains the type to be moved.
            /// 2. Keep the type, required namespace containers and using statements.
            ///    remove everything else from the forked document.
            /// 3. Add this forked document to the solution.
            /// 4. Finally, update the original document and remove the type from it.
            /// </remarks>
            internal async Task<ImmutableArray<CodeActionOperation>> GetOperationsAsync()
            {
                var solution = this._sourceDocument.Project.Solution;

                // Fork, update and add as new document.
                var projectToBeUpdated = this._sourceDocument.Project;

                var projectFolder = System.IO.Path.GetDirectoryName(this._sourceDocument.Project.FilePath);
                var fileName = System.IO.Path.Combine(projectFolder, "Mapping\\" + GetDirectMapperClassName() + ".cs");

                var newDocumentId = DocumentId.CreateNewId(projectToBeUpdated.Id, fileName);

                var documentWithMovedType = await AddNewDocumentWithMappingClassAsync(_sourceDocument, newDocumentId, fileName).ConfigureAwait(false);

                var solutionWithNewDocument = documentWithMovedType.Project.Solution;

                // Get the original source document again, from the latest forked solution.
                //var sourceDocument = solutionWithNewDocument.GetDocument(this._sourceDocument.Id);

                return ImmutableArray.Create<CodeActionOperation>(new ApplyChangesOperation(solutionWithNewDocument));
            }

            /// <summary>
            /// Forks the source document, keeps required type, namespace containers
            /// and adds it the solution.
            /// </summary>
            /// <param name="newDocumentId">id for the new document to be added</param>
            /// <returns>the new solution which contains a new document with the type being moved</returns>
            private async Task<Document> AddNewDocumentWithMappingClassAsync(Document source,
                DocumentId newDocumentId, string fileName)
            {
                var document = source;
                Debug.Assert(document.Name != fileName,
                             $"New document name is same as old document name:{fileName}");

                var projectToBeUpdated = document.Project;

                Document editingDocument = null;

                var directMapperClassName = GetDirectMapperClassName();
                var sourceTypeName = GetDirectMapperClassName();

                foreach (var doc in document.Project.Documents)
                {
                    if (doc.FilePath != null && doc.FilePath.EndsWith("\\Mapping\\" + directMapperClassName + ".cs"))
                    {
                        editingDocument = doc;
                        break;
                    }
                }

                if (editingDocument == null)
                {
                    var solutionWithNewDocument = projectToBeUpdated.Solution.AddDocument(
                        newDocumentId, fileName, ClassGenerationExtensions.CreateCompilationUnitWithNamespace("Mapping"), 
                        folders: Enumerable.Repeat("Mapping", 1));

                    editingDocument = solutionWithNewDocument.GetDocument(newDocumentId);
                }

                editingDocument = await GenerateMappingClass(editingDocument, _mappingTargetClassDeclaration, CancellationToken);

                return editingDocument; 
            }


        private async Task<Document> GenerateMappingClass(Document editingDocument, ClassDeclarationSyntax classSyntax, CancellationToken cancellationToken)
        {
            var docId = editingDocument.Id;
            var solution = editingDocument.Project.Solution;

            var  root = await editingDocument.GetSyntaxRootAsync().ConfigureAwait(false) as CompilationUnitSyntax;

            var namespaceDeclaration = root.GetOrCreateNamespaceDeclaration("Mapping");
            root = namespaceDeclaration.GetCompilationUnit();
            var classDeclaration = namespaceDeclaration.GetOrCreateClassDeclaration(GetDirectMapperClassName(), $"Services.IDataMapper<{_mappingSource}, {MappingTarget}>");
            namespaceDeclaration = classDeclaration.GetNamespaceDeclaration();
            var methodDeclaration = classDeclaration.GetMethodDeclaration("Map", 2);
            
            if (methodDeclaration == null)
            {
                // create method:
                methodDeclaration = CreateMethodDeclarationSyntax(returnTypeName: MappingTarget,
                    methodName: "Map",
                    parameterTypes: new[] { _mappingSource, MappingTarget },
                    paramterNames: new[] { "source", "target" });
                methodDeclaration = methodDeclaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
                var newClassDeclaration = classDeclaration.AddMembers(new[] { methodDeclaration });
                var newNamespaceDeclaration = namespaceDeclaration.ReplaceNode(classDeclaration, newClassDeclaration);

                var newRoot = root.ReplaceNode(root.GetNamespaceDeclaration(), newNamespaceDeclaration);   //SyntaxFactory.CompilationUnit();// classDeclaration?.GetNamespaceDeclaration()?.GetCompilationUnit();
                solution = solution.WithDocumentSyntaxRoot(editingDocument.Id, newRoot);
            }

            editingDocument = solution.GetDocument(docId);
            methodDeclaration = await editingDocument.GetMethodDeclarationAsync(GetDirectMapperClassName(), "Map", 2);
            // method exists at this point, regenerate it
            editingDocument = await MappingGeneratorRefactoring.GenerateMappingMethodBody(editingDocument, methodDeclaration, cancellationToken);
            return editingDocument;
        }

        public MethodDeclarationSyntax CreateMethodDeclarationSyntax(string returnTypeName, string methodName, string[] parameterTypes, string[] paramterNames)
        {
            var parameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(GetParametersList(parameterTypes, paramterNames)));
            return SyntaxFactory.MethodDeclaration(attributeLists: SyntaxFactory.List<AttributeListSyntax>(), 
                    modifiers: SyntaxFactory.TokenList(), 
                    returnType: SyntaxFactory.ParseTypeName(returnTypeName), 
                    explicitInterfaceSpecifier: null, 
                    identifier: SyntaxFactory.Identifier(methodName), 
                    typeParameterList: null, 
                    parameterList: parameterList, 
                    constraintClauses: SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(), 
                    body: SyntaxFactory.Block(SyntaxFactory.ParseStatement("throw new NotImplementedException();")), 
                    semicolonToken: SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                // Annotate that this node should be formatted
                .WithAdditionalAnnotations(Formatter.Annotation);
        }

            public ClassDeclarationSyntax CreateClassDeclarationSyntax(string typeName, string baseTypeName)
            {
                return SyntaxFactory.ClassDeclaration(typeName)
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(baseTypeName)))
                    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                    // Annotate that this node should be formatted
                    .WithAdditionalAnnotations(Formatter.Annotation);
            }

        private IEnumerable<ParameterSyntax> GetParametersList(string[] parameterTypes, string[] paramterNames)
        {
            for (int i = 0; i < parameterTypes.Length; i++)
            {
                yield return SyntaxFactory.Parameter(attributeLists: SyntaxFactory.List<AttributeListSyntax>(),
                    modifiers: SyntaxFactory.TokenList(),
                    type: SyntaxFactory.ParseTypeName(parameterTypes[i]),
                    identifier: SyntaxFactory.Identifier(paramterNames[i]),
                    @default: null);
            }
        
        }
    }
}
