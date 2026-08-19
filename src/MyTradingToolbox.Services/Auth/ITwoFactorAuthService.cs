namespace MyTradingToolbox.Services.Auth;

public interface ITwoFactorAuthService
{
    (string secretKey, string qrCodeUri, string manualKey) GenerateSecret(string email);
    bool VerifyCode(string secretKey, string code);
}
