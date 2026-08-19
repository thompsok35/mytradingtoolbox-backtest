using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Auth;
using OtpNet;
using Xunit;

namespace MyTradingToolbox.Tests;

public class AuthAndSecurityTests
{
    [Fact]
    public void TwoFactorAuthService_GenerateAndVerify_SucceedsWithValidCode()
    {
        var service = new TwoFactorAuthService();
        var (secret, qrCodeUri, manualKey) = service.GenerateSecret("trader@mytradingtoolbox.com");

        secret.Should().NotBeNullOrWhiteSpace();
        manualKey.Should().Be(secret);
        qrCodeUri.Should().Contain("otpauth://totp/");
        qrCodeUri.Should().Contain("MyTradingToolbox");

        // Generate current TOTP code with OtpNet directly
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes);
        var currentCode = totp.ComputeTotp();

        // Verify with service
        var isValid = service.VerifyCode(secret, currentCode);
        isValid.Should().BeTrue();

        // Verify invalid code fails
        service.VerifyCode(secret, "000000").Should().BeFalse();
        service.VerifyCode(secret, "").Should().BeFalse();
    }

    [Fact]
    public void JwtTokenService_GenerateAndValidate_ExtractsCorrectClaims()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            { "Jwt:Secret", "TestSecretKey_For_UnitTesting_MyTradingToolbox_2026!" }
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        var service = new JwtTokenService(config);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@mytradingtoolbox.com",
            Name = "Head Trader",
            Role = "Admin"
        };

        // 1. Session Token
        var token = service.GenerateUserToken(user);
        token.Should().NotBeNullOrWhiteSpace();

        var principal = service.ValidateToken(token, isTwoFactorChallenge: false);
        principal.Should().NotBeNull();
        principal!.FindFirst(ClaimTypes.Email)?.Value.Should().Be("admin@mytradingtoolbox.com");
        principal.FindFirst(ClaimTypes.Name)?.Value.Should().Be("Head Trader");
        principal.FindFirst(ClaimTypes.Role)?.Value.Should().Be("Admin");

        // 2. 2FA Challenge Token
        var challengeToken = service.GenerateTwoFactorChallengeToken(user);
        challengeToken.Should().NotBeNullOrWhiteSpace();

        // Challenge token should not be accepted as session token
        service.ValidateToken(challengeToken, isTwoFactorChallenge: false).Should().BeNull();

        // Challenge token should be accepted as 2FA challenge
        var challengePrincipal = service.ValidateToken(challengeToken, isTwoFactorChallenge: true);
        challengePrincipal.Should().NotBeNull();
        challengePrincipal!.FindFirst(ClaimTypes.Email)?.Value.Should().Be("admin@mytradingtoolbox.com");
    }

    [Fact]
    public async Task UserRepository_CreateOrUpdateGoogleUser_PersistsAndUpdates()
    {
        var options = new DbContextOptionsBuilder<MarketDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MarketDataContext(options);
        var repo = new UserRepository(db);

        var user = await repo.CreateOrUpdateGoogleUserAsync("lead@mytradingtoolbox.com", "Alex Trader", "https://img.com/avatar.png");
        user.Should().NotBeNull();
        user.Email.Should().Be("lead@mytradingtoolbox.com");
        user.Name.Should().Be("Alex Trader");
        user.IsTwoFactorEnabled.Should().BeFalse();

        // Update name
        var updated = await repo.CreateOrUpdateGoogleUserAsync("lead@mytradingtoolbox.com", "Alex T.", "https://img.com/avatar2.png");
        updated.Id.Should().Be(user.Id);
        updated.Name.Should().Be("Alex T.");

        // Enable 2FA
        updated.IsTwoFactorEnabled = true;
        updated.TwoFactorSecret = "JBSWY3DPEHPK3PXP";
        await repo.UpdateUserAsync(updated);

        var fetched = await repo.GetByEmailAsync("lead@mytradingtoolbox.com");
        fetched.Should().NotBeNull();
        fetched!.IsTwoFactorEnabled.Should().BeTrue();
        fetched.TwoFactorSecret.Should().Be("JBSWY3DPEHPK3PXP");
    }
}
