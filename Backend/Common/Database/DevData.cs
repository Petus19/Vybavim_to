using Microsoft.Extensions.Configuration;

namespace Vybavim.Common.Database;

/// <summary>
/// Prepínače vývojových dát. Demo účty (client.demo, specialist.demo …) a ďalšie inzeráty
/// potrebuje AI agent a jeho vyhodnotenie; zapína ich appsettings.Development.json.
/// Integračné testy ich vypínajú – počítajú s presne 12 inzerátmi.
/// </summary>
public static class DevData
{
    public const string DemoAccountsKey = "DevData:DemoAccounts";

    public static bool DemoAccountsEnabled(IConfiguration configuration) =>
        configuration.GetValue<bool>(DemoAccountsKey);
}
