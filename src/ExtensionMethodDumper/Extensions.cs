using Microsoft.CodeAnalysis;

namespace ExtensionMethodDumper;

internal static class Extensions
{
    public static void WriteCommaSeparatedLine(this TextWriter writer, params ReadOnlySpan<object?> parts)
    {
        while (parts is [var part, .. var rest])
        {
            if (part is string text)
            {
                var span = text.AsSpan();
                var quoteValue = span.ContainsAny(',', ' ');

                if (quoteValue)
                {
                    writer.Write('"');
                }

                foreach (var ch in span)
                {
                    // Escape any double quotes.
                    if (ch is '"')
                    {
                        writer.Write('"');
                        writer.Write('"');
                    }
                    else
                    {
                        writer.Write(ch);
                    }
                }

                if (quoteValue)
                {
                    writer.Write('"');
                }
            }
            else
            {
                writer.Write(part);
            }

            if (!rest.IsEmpty)
            {
                writer.Write(",");
            }
            else
            {
                writer.WriteLine();
            }

            parts = rest;
        }
    }

    public static IEnumerable<IMethodSymbol> GetExtensionMethods(this INamedTypeSymbol namedType)
        => namedType.GetMembers().OfType<IMethodSymbol>().Where(static m => m.IsExtensionMethod);

    public static bool ContainsExtensionMethods(this INamedTypeSymbol namedType)
        => namedType.MightContainExtensionMethods && namedType.GetExtensionMethods().Any();

    public static bool ContainsNonExtensionMembers(this INamedTypeSymbol namedType)
        => namedType.GetMembers().Where(static s => s is not IMethodSymbol m || !m.IsExtensionMethod).Any();

    public static List<ExtensionTypeDetails> GetExtensionTypes(this IAssemblySymbol assemblySymbol)
    {
        var extensionTypes = new List<ExtensionTypeDetails>();
        var collector = new ExtensionTypeCollector(extensionTypes);
        assemblySymbol.Accept(collector);

        return extensionTypes;
    }

    private sealed class ExtensionTypeCollector(List<ExtensionTypeDetails> extensionTypes) : SymbolVisitor
    {
        private readonly List<ExtensionTypeDetails> _extensionTypes = extensionTypes;

        public override void VisitAssembly(IAssemblySymbol symbol)
        {
            symbol.GlobalNamespace.Accept(this);
        }

        public override void VisitNamespace(INamespaceSymbol symbol)
        {
            foreach (var member in symbol.GetMembers())
            {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol)
        {
            if (symbol.ContainsExtensionMethods())
            {
                _extensionTypes.Add(new ExtensionTypeDetails(symbol));
            }
        }
    }
}
