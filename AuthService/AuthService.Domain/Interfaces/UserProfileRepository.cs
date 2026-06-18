using AuthService.Domain.Entities;

namespace AuthService.Domain.Interfaces;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByUserIdAsync(string identityUserId);
    Task AddAsync(UserProfile profile);
    Task SaveChangesAsync();
}
