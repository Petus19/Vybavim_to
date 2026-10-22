namespace Vybavim.ArchitectureTests;

/// <summary>
/// Pravidlá modulárneho monolitu z README („Pravidlá architektúry“).
/// Každý test zbiera všetky porušenia naraz, aby bolo v chybe vidieť celý zoznam.
/// </summary>
public sealed class ModuleBoundaryTests
{
    [Fact]
    public void Modules_AreFoundInOutput()
    {
        // Poistka: keby sa .dll modulov nenačítali, ostatné testy by „prešli“ naprázdno.
        Assert.Contains(Assemblies.ModuleImplementations, a => a.GetName().Name == "Vybavim.Modules.Listings");
        Assert.Contains(Assemblies.ModuleContracts, a => a.GetName().Name == "Vybavim.Modules.Listings.Contracts");
    }

    [Fact]
    public void Common_DoesNotReferenceAnyModule()
    {
        // Common je spoločný základ – keby poznal modul, vznikol by cyklus.
        var violations = Assemblies.OurReferences(Assemblies.Common)
            .Where(Assemblies.IsModule)
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Contracts_DoNotReferenceAnyOtherProject()
    {
        // Contracts obsahujú iba rozhrania, DTO a udalosti – nesmú závisieť od ničoho z nášho kódu.
        var violations = Assemblies.ModuleContracts
            .SelectMany(contracts => Assemblies.OurReferences(contracts)
                .Select(reference => $"{contracts.GetName().Name} → {reference}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Modules_ReferenceOtherModulesOnlyThroughContracts()
    {
        // Modul smie odkazovať na Common, na svoje Contracts a na Contracts iných modulov.
        // Na implementáciu iného modulu (napr. Projects → Listings) NIKDY.
        var violations = Assemblies.ModuleImplementations
            .SelectMany(module => Assemblies.OurReferences(module)
                .Where(reference => Assemblies.IsModule(reference) && !Assemblies.IsContracts(reference))
                .Select(reference => $"{module.GetName().Name} → {reference}"))
            .ToList();

        Assert.Empty(violations);
    }

    [Fact]
    public void Modules_ExposeOnlyTheirModuleClass()
    {
        // Vnútro modulu je internal. Verejná je iba trieda {Modul}Module (napr. ListingsModule),
        // cez ktorú modul zapája Api. Výnimkou sú migrácie – tie generuje EF Core ako public.
        var violations = Assemblies.ModuleImplementations
            .SelectMany(module => module.GetExportedTypes()
                .Where(type => type.Name != Assemblies.ModuleName(module.GetName().Name!) + "Module")
                .Where(type => type.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) != true)
                .Select(type => type.FullName!))
            .ToList();

        Assert.Empty(violations);
    }
}
