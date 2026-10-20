using System.Reflection;

namespace Vybavim.ArchitectureTests;

/// <summary>
/// Načíta skompilované projekty backendu (súbory .dll) z výstupného priečinka testov,
/// aby sme mohli skúmať, čo na čo odkazuje.
/// </summary>
internal static class Assemblies
{
    public const string CommonName = "Vybavim.Common";
    private const string ModulePrefix = "Vybavim.Modules.";
    private const string ContractsSuffix = ".Contracts";

    /// <summary>Všetky projekty modulov (implementácie aj Contracts).</summary>
    public static IReadOnlyList<Assembly> AllModules { get; } = LoadModules();

    /// <summary>Implementácie modulov, napr. Vybavim.Modules.Listings.</summary>
    public static IEnumerable<Assembly> ModuleImplementations => AllModules.Where(a => !IsContracts(a.GetName().Name!));

    /// <summary>Verejné kontrakty modulov, napr. Vybavim.Modules.Listings.Contracts.</summary>
    public static IEnumerable<Assembly> ModuleContracts => AllModules.Where(a => IsContracts(a.GetName().Name!));

    public static Assembly Common { get; } = Assembly.Load(CommonName);

    public static bool IsModule(string assemblyName) => assemblyName.StartsWith(ModulePrefix, StringComparison.Ordinal);

    public static bool IsContracts(string assemblyName) => assemblyName.EndsWith(ContractsSuffix, StringComparison.Ordinal);

    /// <summary>Krátky názov modulu: "Vybavim.Modules.Listings.Contracts" → "Listings".</summary>
    public static string ModuleName(string assemblyName) =>
        assemblyName[ModulePrefix.Length..].Replace(ContractsSuffix, string.Empty, StringComparison.Ordinal);

    /// <summary>Názvy projektov nášho kódu, na ktoré assembly skutočne odkazuje.</summary>
    public static IEnumerable<string> OurReferences(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(name => name == CommonName || IsModule(name));

    private static List<Assembly> LoadModules() =>
        Directory.GetFiles(AppContext.BaseDirectory, ModulePrefix + "*.dll")
            .Select(path => Assembly.Load(Path.GetFileNameWithoutExtension(path)))
            .OrderBy(a => a.GetName().Name, StringComparer.Ordinal)
            .ToList();
}
