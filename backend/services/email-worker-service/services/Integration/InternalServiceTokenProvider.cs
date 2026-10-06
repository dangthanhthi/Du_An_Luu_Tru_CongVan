using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace EmailWorkerService.Services.Integration;

public sealed class InternalServiceTokenProvider
{
    private readonly IConfiguration _configuration;

    public InternalServiceTokenProvider(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GetToken()
    {
        var configuredToken = _configuration["InternalAuth:ServiceToken"];
        if (LooksLikeUsableJwt(configuredToken))
        {
            return configuredToken!;
        }

        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Secret must be configured with at least 32 bytes so EmailWorker can call protected internal services.");
        }

        var serviceUserId = _configuration["InternalAuth:ServiceUserId"];
        if (!Guid.TryParse(serviceUserId, out var parsedServiceUserId))
        {
            parsedServiceUserId = Guid.Parse("00000000-0000-0000-0000-000000000008");
        }

        var now = DateTime.UtcNow;
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256Signature);

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, parsedServiceUserId.ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim("service", "email-worker-service")
            ],
            notBefore: now.AddMinutes(-1),
            expires: now.AddHours(12),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool LooksLikeUsableJwt(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Contains("...", StringComparison.Ordinal))
        {
            return false;
        }

        return token.Split('.').Length == 3;
    }
}
