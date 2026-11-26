using System.Security.Claims;
using System.Text;
using LogisticDesk.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace LogisticDesk.Services
{
    /// <summary>JWT settings, bound from the "Jwt" configuration section.</summary>
    public class JwtOptions
    {
        public const string SectionName = "Jwt";

        /// <summary>HMAC-SHA256 needs at least a 256-bit key.</summary>
        public const int MinSigningKeyBytes = 32;

        public string Issuer { get; set; } = "LogisticDesk";
        public string Audience { get; set; } = "LogisticDesk";

        /// <summary>Secret signing key. Never commit it: use user secrets, env vars or a vault.</summary>
        public string SigningKey { get; set; } = string.Empty;

        public int AccessTokenExpirationMinutes { get; set; } = 30;

        public SymmetricSecurityKey GetSecurityKey()
        {
            var bytes = Encoding.UTF8.GetBytes(SigningKey);
            if (bytes.Length < MinSigningKeyBytes)
            {
                throw new InvalidOperationException(
                    $"Jwt:SigningKey must be at least {MinSigningKeyBytes} bytes. Set it with user secrets or the Jwt__SigningKey environment variable.");
            }
            return new SymmetricSecurityKey(bytes);
        }

        public TokenValidationParameters ToValidationParameters() => new()
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = GetSecurityKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    }

    public record AccessToken(string Token, DateTime ExpiresAtUtc);

    /// <summary>Issues signed access tokens for authenticated users.</summary>
    public class JwtTokenService
    {
        private readonly JwtOptions _options;
        private readonly TimeProvider _clock;

        public JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock)
        {
            _options = options.Value;
            _clock = clock;
        }

        public AccessToken CreateToken(ApplicationUser user, IEnumerable<string> roles)
        {
            var now = _clock.GetUtcNow().UtcDateTime;
            var expires = now.AddMinutes(_options.AccessTokenExpirationMinutes);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id),
                new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName ?? string.Empty),
            };
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                IssuedAt = now,
                NotBefore = now,
                Expires = expires,
                SigningCredentials = new SigningCredentials(_options.GetSecurityKey(), SecurityAlgorithms.HmacSha256),
            };

            return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
        }
    }
}
