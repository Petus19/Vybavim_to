namespace Vybavim.IntegrationTests;

/// <summary>
/// Rovnaké Api ako <see cref="ApiFactory"/>, ale s VLASTNÝM PostgreSQL kontajnerom.
/// Používajú ho testy, ktoré inzeráty pridávajú a menia – keby zapisovali do spoločnej
/// databázy, testy vyhľadávania (ktoré počítajú s presne 12 inzerátmi) by náhodne zlyhávali.
/// </summary>
public sealed class WritableApiFactory : ApiFactory;
