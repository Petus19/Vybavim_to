using System.Reflection;

namespace Vybavim.ArchitectureTests;

/// <summary>Pravidlá vrstiev vnútri modulu: Domain nezávisí od databázy ani od webu.</summary>
public sealed class LayerTests
{
    /// <summary>Menné priestory, ktoré doména nesmie používať.</summary>
    private static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore",   // databáza patrí do Infrastructure
        "Microsoft.AspNetCore",            // HTTP patrí do Endpoints
        ".Infrastructure",
        ".Endpoints",
        ".Application",
    ];

    private const BindingFlags AllMembers =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Domain_DoesNotDependOnOtherLayers()
    {
        // Doména (napr. Listing.cs) obsahuje iba biznis pravidlá. Pozrieme sa na všetky typy,
        // ktoré doménové triedy používajú (vlastnosti, polia, parametre, návratové typy, predkovia).
        var violations = Assemblies.ModuleImplementations
            .SelectMany(module => module.GetTypes())
            .Where(type => type.Namespace?.EndsWith(".Domain", StringComparison.Ordinal) == true)
            .SelectMany(type => UsedTypes(type)
                .Where(IsForbiddenInDomain)
                .Select(used => $"{type.FullName} používa {used.FullName}"))
            .Distinct()
            .ToList();

        Assert.Empty(violations);
    }

    private static bool IsForbiddenInDomain(Type type) =>
        type.Namespace is { } ns
        && ForbiddenInDomain.Any(forbidden => forbidden.StartsWith('.')
            ? ns.Contains(forbidden, StringComparison.Ordinal)
            : ns.StartsWith(forbidden, StringComparison.Ordinal));

    private static IEnumerable<Type> UsedTypes(Type type)
    {
        var used = new List<Type>();
        if (type.BaseType is not null)
        {
            used.Add(type.BaseType);
        }

        used.AddRange(type.GetInterfaces());
        used.AddRange(type.GetFields(AllMembers).Select(f => f.FieldType));
        used.AddRange(type.GetProperties(AllMembers).Select(p => p.PropertyType));
        used.AddRange(type.GetConstructors(AllMembers).SelectMany(c => c.GetParameters()).Select(p => p.ParameterType));

        foreach (var method in type.GetMethods(AllMembers))
        {
            used.Add(method.ReturnType);
            used.AddRange(method.GetParameters().Select(p => p.ParameterType));
        }

        // Rozbalí generiká a polia: List<Listing> → List + Listing, string[] → string
        return used.SelectMany(Unwrap);
    }

    private static IEnumerable<Type> Unwrap(Type type)
    {
        if (type.HasElementType)
        {
            return Unwrap(type.GetElementType()!);
        }

        return type.IsGenericType
            ? type.GetGenericArguments().SelectMany(Unwrap).Prepend(type.GetGenericTypeDefinition())
            : [type];
    }
}
