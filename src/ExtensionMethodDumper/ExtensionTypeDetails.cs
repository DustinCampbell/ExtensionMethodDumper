using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ExtensionMethodDumper;

internal sealed record class ExtensionTypeDetails(INamedTypeSymbol Symbol)
{
    private string? _displayText;

    private ImmutableArray<ExtensionMethodDetails> _extensionMethods;
    private ImmutableArray<ITypeSymbol> _thisParameterTypes;

    public bool IsPublic => Symbol.DeclaredAccessibility == Accessibility.Public;

    public ImmutableArray<ExtensionMethodDetails> ExtensionMethods
    {
        get
        {
            if (_extensionMethods.IsDefault)
            {
                _extensionMethods = [.. from method in Symbol.GetMembers().OfType<IMethodSymbol>()
                                        where method.IsExtensionMethod
                                        select new ExtensionMethodDetails(method)];
            }

            return _extensionMethods;
        }
    }

    public bool ContainsNonExtensionMembers
        => Symbol.ContainsNonExtensionMembers();

    public ImmutableArray<ITypeSymbol> ThisParameterTypes
    {
        get
        {
            if (_thisParameterTypes.IsDefault)
            {
                _thisParameterTypes = [.. ExtensionMethods.Select(static x => x.ThisParameterType)
                                                          .Distinct<ITypeSymbol>(SymbolEqualityComparer.Default)];
            }

            return _thisParameterTypes;
        }
    }

    public bool AllExtensionsHaveSameThisParameterType
        => ThisParameterTypes.Length == 1;

    public string DisplayText => _displayText ??= Symbol.ToDisplayString();

    public override string ToString() => DisplayText;
}
