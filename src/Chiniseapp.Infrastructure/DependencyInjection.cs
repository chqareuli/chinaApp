using Chiniseapp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Chiniseapp.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found. " +
                "Set it via 'dotnet user-secrets set ConnectionStrings:DefaultConnection \"...\"' in Chiniseapp.Api.");

        services.AddDbContext<ChiniseDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
