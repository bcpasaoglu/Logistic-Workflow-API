using LogisticDesk.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogisticDesk.Tests;

/// <summary>
/// Boots the real API pipeline (routing, auth, validation, error handling)
/// with the MySQL DbContext swapped for an in-memory database.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string TestSigningKey = "test-signing-key-that-is-long-enough-for-hs256";
    private readonly string _databaseName = $"api-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused");
        builder.UseSetting("Jwt:SigningKey", TestSigningKey);
        builder.UseSetting("Hosting:BehindTlsProxy", "true");
        builder.UseSetting("Swagger:Enabled", "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<LogiDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<LogiDbContext>>();
            services.AddDbContext<LogiDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }
}
