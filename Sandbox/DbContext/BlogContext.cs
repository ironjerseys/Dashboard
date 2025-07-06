using Microsoft.EntityFrameworkCore;
using Sandbox.Models;

namespace Sandbox.Data;

public class BlogContext : DbContext
{
    public BlogContext(DbContextOptions<BlogContext> opts) : base(opts) { }

    public DbSet<Article> Articles => Set<Article>();
}