namespace GarageFlow.Api.Security;

/// <summary>
/// Role names as used in [Authorize(Roles = ...)]. They match the UserRole enum names, which is what the
/// token generator writes into the "role" claim. A comma means "any of these".
/// </summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Advisor = "Advisor";
    public const string Technician = "Technician";

    public const string AdminOrAdvisor = "Admin,Advisor";
    public const string AdminOrTechnician = "Admin,Technician";
}
