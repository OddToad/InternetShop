using AuthService.Domain.Entities;
using AuthService.Domain.Interfaces;
using AuthService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly ApplicationDbContext _context;

    public UserProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserProfile?> GetByUserIdAsync(string identityUserId)
    {
        return await _context.UserProfiles.FirstOrDefaultAsync(p => p.IdentityUserId == identityUserId);
    }

    public async Task AddAsync(UserProfile profile)
    {
        await _context.UserProfiles.AddAsync(profile);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}