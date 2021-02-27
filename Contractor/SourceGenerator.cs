using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Contractor
{
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        private const string attributeText = @"
using System;
namespace Contractor
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = false)]
    sealed class AutoImplementAttribute : Attribute
    {
        public AutoImplementAttribute()
        {
        }
    }
}
";

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
                context.AddSource("AutoImplementAttribute", SourceText.From(attributeText, Encoding.UTF8));
                if (!(context.SyntaxReceiver is SyntaxReceiver receiver))
                    return;

                CSharpParseOptions options = (context.Compilation as CSharpCompilation).SyntaxTrees[0].Options as CSharpParseOptions;
                Compilation compilation = context.Compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(SourceText.From(attributeText, Encoding.UTF8), options));

                INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("Contractor.AutoImplementAttribute");

                List<INamedTypeSymbol> interfaceSymbols = new List<INamedTypeSymbol>();
                foreach (InterfaceDeclarationSyntax interfaceDeclarationSyntax in receiver.CandidateInterfaces)
                {
                    SemanticModel model = compilation.GetSemanticModel(interfaceDeclarationSyntax.SyntaxTree);

                    // Get the symbol being decleared by the interface, and keep it if its annotated
                    INamedTypeSymbol interfaceSymbol = model.GetDeclaredSymbol(interfaceDeclarationSyntax);
                    if (interfaceSymbol.GetAttributes().Any(x => x.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    {
                        interfaceSymbols.Add(interfaceSymbol);
                    }
                }

                List<INamedTypeSymbol> classSymbols = new List<INamedTypeSymbol>();
                foreach (ClassDeclarationSyntax classDeclarationSyntax in receiver.CandidateClasses)
                {
                    SemanticModel model = compilation.GetSemanticModel(classDeclarationSyntax.SyntaxTree);

                    // Get the symbol being decleared by the class, and keep it if it implements one of the interfaceSymbols
                    INamedTypeSymbol classSymbol = model.GetDeclaredSymbol(classDeclarationSyntax);
                    if (classSymbol.Interfaces.Any(x => interfaceSymbols.Contains(x)))
                    {
                        classSymbols.Add(classSymbol);
                    }
                }

                foreach (var classSymbol in classSymbols)
                {
                    var allContractableInterfaces = classSymbol.Interfaces.Where(x => x.GetAttributes().Any(a => a.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)));
                    var properties = allContractableInterfaces.SelectMany(x => x.GetMembers().Where(m => m.Kind == SymbolKind.Property).Cast<IPropertySymbol>());
                    var propertiesCodeList = properties.Select(x => $"public {x.Type.Name} {x.Name} {{ get; set; }}");
                    var propertiesCode = string.Join("\n", propertiesCodeList);

                    SourceText sourceText = SourceText.From($@"
using System;
namespace {GetFullNamespaceName(classSymbol)} {{
    public partial class {classSymbol.Name}
    {{
        {propertiesCode}
    }}
}}", Encoding.UTF8);
                    context.AddSource($"{classSymbol.Name}.Generated.cs", sourceText);
                }
            }
            catch (Exception ex)
            {
                if (!Debugger.IsAttached)
                    Debugger.Launch();
            }
        }

        private static string GetFullNamespaceName(INamedTypeSymbol classSymbol)
        {
            List<string> namespaceSegments = new List<string>();

            var namespaceSymbol = classSymbol.ContainingNamespace;
            while (!namespaceSymbol.IsGlobalNamespace)
            {
                namespaceSegments.Insert(0, namespaceSymbol.Name);
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
            }

            return string.Join(".", namespaceSegments);
        }

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }
    }
}

