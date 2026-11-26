using LogisticDesk.Models;
using LogisticDesk.Services;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Xunit;

namespace LogisticDesk.Tests;

public class JwtTokenServiceTests
{
    private static JwtOptions Options(string key = ApiFactory.TestSigningKey) => new()
    {
        Issuer = "issuer",
        Audience = "audience",
        SigningKey = key,
        AccessTokenExpirationMinutes = 15,
    };

    [Fact]
    public async Task Token_validates_and_carries_user_and_roles()
    {
        var options = Options();
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(options), TimeProvider.System);
        var user = new ApplicationUser { Id = "user-1", UserName = "a@b.com", Email = "a@b.com" };

        var token = service.CreateToken(user, new[] { "Manager" });
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, options.ToValidationParameters());

        Assert.True(result.IsValid);
        Assert.Equal("user-1", result.Claims[JwtRegisteredClaimNames.Sub]);
        Assert.InRange(token.ExpiresAtUtc, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public async Task Token_signed_with_another_key_is_rejected()
    {
        var service = new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options()), TimeProvider.System);
        var token = service.CreateToken(new ApplicationUser { Id = "u" }, Array.Empty<string>());

        var other = Options("a-completely-different-signing-key-of-32+-bytes");
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Token, other.ToValidationParameters());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Short_signing_key_is_refused()
    {
        Assert.Throws<InvalidOperationException>(() => Options("too-short").GetSecurityKey());
    }
}
