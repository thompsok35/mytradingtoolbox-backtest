using System.Security.Claims;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Services.Auth;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepo;
    private readonly IJwtTokenService _jwtService;
    private readonly ITwoFactorAuthService _twoFactorService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepo,
        IJwtTokenService jwtService,
        ITwoFactorAuthService twoFactorService,
        IConfiguration config,
        ILogger<AuthController> logger)
    {
        _userRepo = userRepo;
        _jwtService = jwtService;
        _twoFactorService = twoFactorService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Returns public auth configuration (e.g. Google Client ID for frontend)
    /// </summary>
    [HttpGet("config")]
    public ActionResult GetAuthConfig()
    {
        var clientId = _config["Google:ClientId"] 
            ?? _config["GOOGLE_CLIENT_ID"] 
            ?? _config["VITE_GOOGLE_CLIENT_ID"] 
            ?? string.Empty;

        return Ok(new { googleClientId = clientId });
    }

    /// <summary>
    /// Authenticates with Google ID Token / Credential
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleAuthRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Credential))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Google credential token is required." });
        }

        string email = string.Empty;
        string name = "Trader";
        string? picture = null;

        try
        {
            var googleClientId = _config["Google:ClientId"] 
                ?? _config["GOOGLE_CLIENT_ID"] 
                ?? _config["VITE_GOOGLE_CLIENT_ID"];
                
            GoogleJsonWebSignature.Payload? payload = null;

            if (!string.IsNullOrWhiteSpace(googleClientId))
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential, new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = new[] { googleClientId }
                });
            }
            else
            {
                payload = await GoogleJsonWebSignature.ValidateAsync(request.Credential);
            }

            email = payload.Email;
            name = payload.Name ?? payload.Email;
            picture = payload.Picture;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google token validation failed");
            return Unauthorized(new AuthResponse { Success = false, Message = $"Invalid Google token: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Email address could not be verified from Google." });
        }

        var user = await _userRepo.CreateOrUpdateGoogleUserAsync(email, name, picture, ct);

        if (user.IsTwoFactorEnabled && !string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            var challengeToken = _jwtService.GenerateTwoFactorChallengeToken(user);
            return Ok(new AuthResponse
            {
                Success = true,
                RequiresTwoFactor = true,
                TwoFactorChallengeToken = challengeToken,
                Message = "Two-factor authentication code required."
            });
        }

        var token = _jwtService.GenerateUserToken(user);
        return Ok(new AuthResponse
        {
            Success = true,
            RequiresTwoFactor = false,
            Token = token,
            User = ToDto(user)
        });
    }

    /// <summary>
    /// Verifies 2FA TOTP code for Login or Setup confirmation
    /// </summary>
    [HttpPost("2fa/verify")]
    public async Task<ActionResult<AuthResponse>> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Verification code is required." });
        }

        if (!string.IsNullOrWhiteSpace(request.TwoFactorChallengeToken))
        {
            var principal = _jwtService.ValidateToken(request.TwoFactorChallengeToken, isTwoFactorChallenge: true);
            if (principal == null)
            {
                return Unauthorized(new AuthResponse { Success = false, Message = "2FA challenge token expired or invalid." });
            }

            var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(new AuthResponse { Success = false, Message = "Invalid user identification in challenge token." });
            }

            var user = await _userRepo.GetByIdAsync(userId, ct);
            if (user == null || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            {
                return Unauthorized(new AuthResponse { Success = false, Message = "User not found or 2FA not configured." });
            }

            var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.Code);
            if (!isValid)
            {
                return BadRequest(new AuthResponse { Success = false, Message = "Invalid 6-digit authenticator code." });
            }

            var token = _jwtService.GenerateUserToken(user);
            return Ok(new AuthResponse
            {
                Success = true,
                Token = token,
                User = ToDto(user)
            });
        }

        var authUser = await GetCurrentAuthenticatedUserAsync(ct);
        if (authUser == null)
        {
            return Unauthorized(new AuthResponse { Success = false, Message = "Authentication required." });
        }

        if (string.IsNullOrWhiteSpace(authUser.TwoFactorSecret))
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Please request 2FA setup first." });
        }

        var isSetupValid = _twoFactorService.VerifyCode(authUser.TwoFactorSecret, request.Code);
        if (!isSetupValid)
        {
            return BadRequest(new AuthResponse { Success = false, Message = "Invalid 6-digit code. Please verify time and try again." });
        }

        authUser.IsTwoFactorEnabled = true;
        await _userRepo.UpdateUserAsync(authUser, ct);

        return Ok(new AuthResponse
        {
            Success = true,
            User = ToDto(authUser),
            Message = "Two-Factor Authentication successfully activated!"
        });
    }

    /// <summary>
    /// Generates TOTP secret and QR code for setting up 2FA
    /// </summary>
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupResponse>> SetupTwoFactor(CancellationToken ct)
    {
        var user = await GetCurrentAuthenticatedUserAsync(ct);
        if (user == null) return Unauthorized("Authentication required.");

        var (secret, qrCodeUri, manualKey) = _twoFactorService.GenerateSecret(user.Email);
        user.TwoFactorSecret = secret;
        await _userRepo.UpdateUserAsync(user, ct);

        return Ok(new TwoFactorSetupResponse
        {
            SecretKey = secret,
            QrCodeUri = qrCodeUri,
            ManualEntryKey = manualKey
        });
    }

    /// <summary>
    /// Disables 2FA for the current user
    /// </summary>
    [HttpPost("2fa/disable")]
    public async Task<ActionResult> DisableTwoFactor(CancellationToken ct)
    {
        var user = await GetCurrentAuthenticatedUserAsync(ct);
        if (user == null) return Unauthorized("Authentication required.");

        user.IsTwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        await _userRepo.UpdateUserAsync(user, ct);

        return Ok(new { success = true, message = "2FA disabled successfully." });
    }

    /// <summary>
    /// Returns current user profile
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<UserProfileDto>> GetCurrentUser(CancellationToken ct)
    {
        var user = await GetCurrentAuthenticatedUserAsync(ct);
        if (user == null) return Unauthorized();
        return Ok(ToDto(user));
    }

    private async Task<AppUser?> GetCurrentAuthenticatedUserAsync(CancellationToken ct)
    {
        if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var tokenStr = authHeader.ToString();
            if (tokenStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var jwt = tokenStr.Substring("Bearer ".Length).Trim();
                var principal = _jwtService.ValidateToken(jwt);
                if (principal != null)
                {
                    var userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if (Guid.TryParse(userIdStr, out var userId))
                    {
                        return await _userRepo.GetByIdAsync(userId, ct);
                    }
                }
            }
        }
        return null;
    }

    private static UserProfileDto ToDto(AppUser user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        Name = user.Name,
        PictureUrl = user.PictureUrl,
        Role = user.Role,
        IsTwoFactorEnabled = user.IsTwoFactorEnabled
    };
}
