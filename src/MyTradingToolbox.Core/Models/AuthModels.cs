namespace MyTradingToolbox.Core.Models;

public class GoogleAuthRequest
{
    public string Credential { get; set; } = string.Empty;
}

public class AuthResponse
{
    public bool Success { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public string? TwoFactorChallengeToken { get; set; }
    public string? Token { get; set; }
    public UserProfileDto? User { get; set; }
    public string? Message { get; set; }
}

public class VerifyTwoFactorRequest
{
    public string? TwoFactorChallengeToken { get; set; }
    public string Code { get; set; } = string.Empty;
}

public class TwoFactorSetupResponse
{
    public string SecretKey { get; set; } = string.Empty;
    public string QrCodeUri { get; set; } = string.Empty;
    public string ManualEntryKey { get; set; } = string.Empty;
}

public class UserProfileDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PictureUrl { get; set; }
    public string Role { get; set; } = "Admin";
    public bool IsTwoFactorEnabled { get; set; }
}
