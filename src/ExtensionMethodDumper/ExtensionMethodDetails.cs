using Microsoft.CodeAnalysis;

namespace ExtensionMethodDumper;

internal sealed record class ExtensionMethodDetails(IMethodSymbol Symbol)
{
    private static readonly SymbolDisplayFormat s_symbolDisplayFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance,
        memberOptions: SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef,
        extensionMethodStyle: SymbolDisplayExtensionMethodStyle.StaticMethod,
        parameterOptions: SymbolDisplayParameterOptions.IncludeExtensionThis | SymbolDisplayParameterOptions.IncludeModifiers | SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeOptionalBrackets,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseErrorTypeSymbolName | SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.AllowDefaultLiteral);

    private string? _displayText;

    public bool IsPublic
        => Symbol.DeclaredAccessibility == Accessibility.Public &&
           Symbol.ContainingSymbol.DeclaredAccessibility == Accessibility.Public;

    public bool IsGeneric => Symbol.IsGenericMethod;
    public int ReducedFormParameterCount => Symbol.Parameters.Length - 1;

    public IParameterSymbol ThisParameter => Symbol.Parameters[0];
    public ITypeSymbol ThisParameterType => ThisParameter.Type;

    public bool ThisParameterUsesTypeParameter
        => UsesTypeParameter(ThisParameterType);

    public bool ThisParameterIsErrorType
        => ThisParameterType is IErrorTypeSymbol errorType;

    public bool ThisParameterIsGenericType
        => ThisParameterType is INamedTypeSymbol namedType &&
           namedType.IsGenericType;

    public bool ThisParameterIsValueType => ThisParameterType.IsValueType;
    public RefKind ThisParameterRefKind => ThisParameter.RefKind;

    private bool UsesTypeParameter(ITypeSymbol type)
    {
        switch (type)
        {
            case INamedTypeSymbol namedType:
                if (namedType.IsGenericType)
                {
                    foreach (var typeArgument in namedType.TypeArguments)
                    {
                        if (UsesTypeParameter(typeArgument))
                        {
                            return true;
                        }
                    }
                }

                return false;

            case ITypeParameterSymbol typeParameter:
                return Symbol.TypeParameters.Contains(typeParameter, SymbolEqualityComparer.Default);

            case IArrayTypeSymbol arrayType:
                return UsesTypeParameter(arrayType.ElementType);

            case IPointerTypeSymbol pointerType:
                return UsesTypeParameter(pointerType.PointedAtType);

            default:
                throw new InvalidOperationException($"Surprise! Didn't handle {type.Name}!");
        }
    }

    public string DisplayText => _displayText ??= Symbol.ToDisplayString(s_symbolDisplayFormat);

    public override string ToString() => DisplayText;
}
