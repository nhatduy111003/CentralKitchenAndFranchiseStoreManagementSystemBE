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
                "Host=dpg-d5u9cssr85hc73a1ictg-a.singapore-postgres.render.com;" +
                "Port=5432;" +
                "Database=centralkitchenandfranchise_4tn4;" +
                "Username=centralkitchenandfranchise_user;" +
                "Password=dk7WbsEhymEjLFfGs39yzLViRiZwhy2r;" +
                "SSL Mode=Require;" +
                "Trust Server Certificate=true"
            );

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
