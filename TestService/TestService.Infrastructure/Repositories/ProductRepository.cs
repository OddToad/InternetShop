using Microsoft.EntityFrameworkCore;
using TestService.Application.Interfaces;
using TestService.Domain.Entities;

namespace TestService.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ApplicationDbContext _context;

    public ProductRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id)
        => await _context.Products.FindAsync(id);

    public async Task<IEnumerable<Product>> GetAllAsync()
        => await _context.Products.ToListAsync();

    public async Task AddAsync(Product product)
        => await _context.Products.AddAsync(product);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();
}