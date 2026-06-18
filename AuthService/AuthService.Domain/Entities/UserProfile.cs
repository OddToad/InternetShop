namespace AuthService.Domain.Entities;

public class UserProfile
{
    public Guid Id { get; set; }
    public string IdentityUserId { get; set; } = null!; // Связь с таблицей AspNetUsers
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string? PhoneNumber { get; set; }
}