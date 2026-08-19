using System.Text.Encodings.Web;
using OtpNet;

namespace MyTradingToolbox.Services.Auth;

public class TwoFactorAuthService : ITwoFactorAuthService
{
    private const string Issuer = "MyTradingToolbox";

    public (string secretKey, string qrCodeUri, string manualKey) GenerateSecret(string email)
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);
        
        var encodedIssuer = UrlEncoder.Default.Encode(Issuer);
        var encodedEmail = UrlEncoder.Default.Encode(email);
        var qrCodeUri = $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={base32Secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period=30";

        return (base32Secret, qrCodeUri, base32Secret);
    }

    public bool VerifyCode(string secretKey, string code)
    {
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(code))
            return false;

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secretKey.Trim());
            var totp = new Totp(secretBytes, mode: OtpHashMode.Sha1, step: 30, totpSize: 6);
            return totp.VerifyTotp(code.Trim(), out _, new VerificationWindow(previous: 1, future: 1));
        }
        catch
        {
            return false;
        }
    }
}
