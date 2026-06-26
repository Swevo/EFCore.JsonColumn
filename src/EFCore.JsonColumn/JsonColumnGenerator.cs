// <copyright file="JsonColumnGenerator.cs" company="Canon Europe Limited">
// Copyright (c) Canon Europe Limited. All rights reserved.
// </copyright>

namespace EFCore.JsonColumn;

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

internal sealed class JsonColumnPropertyModel(
    string containingTypeName,
    string propertyName,
    string propertyTypeName,
    bool isValueType,
    Location? location)
{
    public string ContainingTypeName { get; } = containingTypeName;

    public string PropertyName { get; } = propertyName;

    public string PropertyTypeName { get; } = propertyTypeName;

    public bool IsValueType { get; } = isValueType;

    public Location? Location { get; } = location;
}

/// <summary>Generates JSON-column configuration for EF Core owned navigation properties marked with <c>[JsonColumn]</c>.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class JsonColumnGenerator : IIncrementalGenerator
{
    private const string AttributeFqn = "EFCore.JsonColumn.JsonColumnAttribute";

    private static readonly DiagnosticDescriptor Jscol001 = new(
        id: "JSCOL001",
        title: "JsonColumn requires a reference type",
        messageFormat: "'{0}' is decorated with [JsonColumn] but has value type '{1}'. JSON columns must be reference-type owned navigations.",
        category: "EFCore.JsonColumn",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(static initializationContext =>
            initializationContext.AddSource("EFCore.JsonColumn.Attribute.g.cs", SourceText.From(Emitter.AttributeSource, Encoding.UTF8)));

        var properties = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: AttributeFqn,
                predicate: static (node, _) => node is PropertyDeclarationSyntax,
                transform: static (syntaxContext, _) =>
                {
                    var propertySymbol = (IPropertySymbol)syntaxContext.TargetSymbol;
                    return new JsonColumnPropertyModel(
                        propertySymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        propertySymbol.Name,
                        propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                        propertySymbol.Type.IsValueType,
                        propertySymbol.Locations.Length > 0 ? propertySymbol.Locations[0] : null);
                })
            .Collect();

        context.RegisterSourceOutput(properties, static (productionContext, collectedProperties) =>
        {
            foreach (var property in collectedProperties)
            {
                if (!property.IsValueType)
                {
                    continue;
                }

                productionContext.ReportDiagnostic(Diagnostic.Create(
                    Jscol001,
                    property.Location,
                    property.PropertyName,
                    property.PropertyTypeName));
            }

            productionContext.AddSource(
                "EFCore.JsonColumn.Core.g.cs",
                SourceText.From(Emitter.EmitCore(collectedProperties), Encoding.UTF8));
        });
    }
}
