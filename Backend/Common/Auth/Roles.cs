namespace Vybavim.Common.Auth;

/// <summary>
/// Názvy rolí. Sú v Common, lebo oprávnenia kontrolujú všetky moduly
/// (napr. moderovanie inzerátov v Listings smie iba Admin).
/// </summary>
public static class Roles
{
    /// <summary>Správca platformy: vidí a spravuje používateľov, moderuje obsah.</summary>
    public const string Admin = "Admin";
}
