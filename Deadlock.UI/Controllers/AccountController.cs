using Deadlock.Core.Domain.Entities;
using Deadlock.Core.DTO;
using Deadlock.Core.Helpers;
using Deadlock.Core.ServiceContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Deadlock.UI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly IEmailSender _emailSender;

        public AccountController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IJwtService jwtService,
            IEmailSender emailSender
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _emailSender = emailSender;
        }

        #region register

        [HttpPost("register")]
        public async Task<ActionResult<AppUser>> PostRegister(RegisterDto registerDto)
        {
            //Validation
            if (!ModelState.IsValid)
            {
                string errorMessage = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Problem(errorMessage);
            }

            if (await _userManager.FindByEmailAsync(registerDto.Email) != null)
                ModelState.AddModelError(nameof(registerDto.Email), "This email is already in use.");

            if (await _userManager.FindByNameAsync(registerDto.UserName) != null)
                ModelState.AddModelError(nameof(registerDto.UserName), "This username is already taken.");

            AppUser appUser = new AppUser()
            {
                Email = registerDto.Email,
                UserName = registerDto.UserName,
                EmailConfirmed = true
            };

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUser = await _userManager.FindByIdAsync(currentUserId!);

            string role;

            if (currentUser != null && await _userManager.IsInRoleAsync(currentUser, SD.ManagerRole))
            {
                role = SD.ObserverRole;
                appUser.ManagerId = currentUserId;
            }
            else
            {
                role = SD.ManagerRole;
            }

            IdentityResult result = await _userManager.CreateAsync(appUser, registerDto.Password);
            if (result.Succeeded)
            {
                var roleResult = await _userManager.AddToRoleAsync(appUser, role);
                if (!roleResult.Succeeded)
                    return BadRequest("Failed to assign role to user.");

                //sign-in
                await _signInManager.SignInAsync(appUser, isPersistent: false);

                var authenticationResponse = await _jwtService.CreateJwtToken(appUser);
                appUser.RefreshToken = authenticationResponse.RefreshToken;

                appUser.RefreshTokenExpirationDateTime = authenticationResponse.RefreshTokenExpirationDateTime;
                await _userManager.UpdateAsync(appUser);

                return Ok(new
                {
                    message = role == SD.ManagerRole ? "Manager registered successfully" : "Observer registered successfully",
                    data = authenticationResponse,
                    role = role
                });
            }
            else
            {
                string errorMessage = string.Join(" | ", result.Errors.Select(e => e.Description)); //error1 | error2
                return Problem(errorMessage);
            }
        }


        #endregion

        #region IsEmailAlreadyRegistered

        [HttpGet("exists/{email}")]
        public async Task<IActionResult> IsEmailAlreadyRegistered(string email)
        {
            AppUser? user = await _userManager.FindByEmailAsync(email);

            if (user == null)
                return Ok(true);
            else
                return Ok(false);
        }

        #endregion

        #region login

        [HttpPost("login")]
        public async Task<IActionResult> PostLogin(LoginDto loginDto)
        {
            //Validation
            if (!ModelState.IsValid)
            {
                string errorMessage = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Problem(errorMessage);
            }

            var result = await _signInManager.PasswordSignInAsync(loginDto.UserName, loginDto.Password, isPersistent: false, lockoutOnFailure: false);

            if (result.Succeeded)
            {
                AppUser? UserFromDB = await _userManager.FindByNameAsync(loginDto.UserName);
                if (UserFromDB == null)
                {
                    return NoContent();
                }

                //sign-in
                await _signInManager.SignInAsync(UserFromDB, isPersistent: false);

                var authenticationResponse = await _jwtService.CreateJwtToken(UserFromDB);
                UserFromDB.RefreshToken = authenticationResponse.RefreshToken;
                UserFromDB.RefreshTokenExpirationDateTime = authenticationResponse.RefreshTokenExpirationDateTime;
                await _userManager.UpdateAsync(UserFromDB);

                return Ok(authenticationResponse);
            }
            else
            {
                return Problem("Invalid email or password");
            }
        }

        #endregion

        #region Logout

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> PostLogout()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found");

            user.RefreshToken = null;
            user.RefreshTokenExpirationDateTime = DateTime.UtcNow.AddMinutes(-1);
            await _userManager.UpdateAsync(user);

            await _signInManager.SignOutAsync();

            return Ok(new
            {
                message = "Logged out successfully",
                timestamp = DateTime.UtcNow
            });
        }

        #endregion

        #region generate-new-jwt-token

        [HttpPost("generate-new-jwt-token")]
        public async Task<IActionResult> GenerateNewAccessToken(TokenModel tokenModel)
        {
            if (tokenModel == null)
            {
                return BadRequest("Invalid client request");
            }

            ClaimsPrincipal? principal = _jwtService.GetPrincipalFromJwtToken(tokenModel.Token);
            if (principal == null)
            {
                return BadRequest("Invalid jwt access token");
            }

            string? email = principal.FindFirstValue(ClaimTypes.Email);
            if (email == null)
                return BadRequest("Email claim missing from token");
            AppUser? user = await _userManager.FindByEmailAsync(email);

            if (user == null || user.RefreshToken != tokenModel.RefreshToken || user.RefreshTokenExpirationDateTime <= DateTime.Now)
            {
                return BadRequest("Invalid refresh token");
            }

            AuthenticationResponse authenticationResponse = await _jwtService.CreateJwtToken(user);

            user.RefreshToken = authenticationResponse.RefreshToken;
            user.RefreshTokenExpirationDateTime = authenticationResponse.RefreshTokenExpirationDateTime;

            await _userManager.UpdateAsync(user);

            return Ok(authenticationResponse);
        }

        #endregion

        #region RequestPasswordReset

        [HttpPost("forgot-password/request")]
        public async Task<IActionResult> RequestPasswordReset([FromBody] ForgotPasswordRequestDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return Ok(new { message = "If email exists, OTP sent successfully" });

            // Generate 4-digit OTP
            string otp = new Random().Next(1000, 9999).ToString();
            user.OtpCode = otp;
            user.OtpExpiration = DateTime.UtcNow.AddMinutes(5);
            await _userManager.UpdateAsync(user);

            // Send Email
            await _emailSender.SendEmailAsync(
                request.Email,
                "Your OTP Code",
                $"<h2>Your OTP: <strong>{otp}</strong></h2><p>Valid for 5 minutes</p>");

            return Ok(new { message = "OTP sent successfully" });
        }

        #endregion

        #region ResendOtp

        [HttpPost("forgot-password/resendOtp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || user.OtpExpiration < DateTime.UtcNow)
                return BadRequest("OTP expired or invalid email");

            // Generate new OTP
            string newOtp = new Random().Next(1000, 9999).ToString();
            user.OtpCode = newOtp;
            user.OtpExpiration = DateTime.UtcNow.AddMinutes(5);

            await _userManager.UpdateAsync(user);

            await _emailSender.SendEmailAsync(request.Email, "New OTP Code",
                $"<h2>New OTP: <strong>{newOtp}</strong></h2>");

            return Ok(new { message = "New OTP sent successfully" });
        }

        #endregion

        #region VerifyOtp

        [HttpPost("forgot-password/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || user.OtpCode != request.Otp || user.OtpExpiration < DateTime.UtcNow)
                return BadRequest("Invalid or expired OTP");

            user.OtpCode = null; // Clear OTP
            user.OtpExpiration = DateTime.UtcNow.AddMinutes(-1); // Mark as used
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "OTP verified successfully", email = request.Email });
        }

        #endregion

        #region ResetPassword

        [HttpPost("forgot-password/reset")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
        {
            if (!ModelState.IsValid || request.Password != request.ConfirmPassword)
                return BadRequest("Passwords do not match");

            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
                return BadRequest("User not found");

            if (user.OtpExpiration < DateTime.UtcNow.AddMinutes(-10))
                return BadRequest("OTP verification expired. Request new OTP");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, request.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return BadRequest(errors);
            }

            // Clear OTP data
            user.OtpCode = null;
            user.OtpExpiration = DateTime.MinValue;
            await _userManager.UpdateAsync(user);

            return Ok(new { message = "Password reset successfully. You can now login with new password" });
        }

        #endregion

    }
}
