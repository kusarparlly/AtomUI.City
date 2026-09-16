namespace AtomUI.City.Generators.Presentation;

internal sealed class PresentationViewConstructorParameter
{
    public PresentationViewConstructorParameter(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Constructor parameter type name cannot be empty.", nameof(typeName));
        }

        TypeName = typeName;
    }

    public string TypeName { get; }
}
