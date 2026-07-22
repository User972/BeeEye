using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BeeEye.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without a
/// running application or live database. The connection string here is only used for
/// provider selection at design time.
/// </summary>
public sealed class BeeEyeDbContextFactory : IDesignTimeDbContextFactory<BeeEyeDbContext>
{
    public BeeEyeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BeeEyeDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=beeeye;Username=beeeye;Password=design_time_only")
            .Options;
        return new BeeEyeDbContext(options);
    }
}
