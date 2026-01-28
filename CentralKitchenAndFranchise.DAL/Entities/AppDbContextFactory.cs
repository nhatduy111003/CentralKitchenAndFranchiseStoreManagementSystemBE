using CentralKitchenAndFranchise.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CentralKitchenAndFranchise.DAL
{
    public class AppDbContextFactory
        : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

            optionsBuilder.UseNpgsql(
                "Host=dpg-d5sbm9c9c44c73ertha0-a.singapore-postgres.render.com;" +
                "Port=5432;" +
                "Database=centralkitchenandfranchise;" +
                "Username=centralkitchenandfranchise_user;" +
                "Password=PNg4bALugnoxJiJWm7WA0jqc5RYqgA2C;" +
                "SSL Mode=Require;" +
                "Trust Server Certificate=true"
            );

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
