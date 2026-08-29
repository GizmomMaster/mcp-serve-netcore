namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Имена схем аутентификации (FR-40…FR-43).
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>Схема-диспетчер, выбирающая между API-ключом и JWT.</summary>
    public const string Composite = "Composite";
}
