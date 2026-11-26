using System.Security.Claims;
using LogisticDesk.DTOs.Auth;
using LogisticDesk.Models;
using LogisticDesk.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LogisticDesk.Controllers
{
    /// <summary>Registration, login (JWT) and the current user.</summary>
    [Route("api/auth")]
    [ApiController]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly JwtTokenService _tokens;

        public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager, JwtTokenService tokens)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokens = tokens;
        }

        /// <summary>Creates a new user account.</summary>
        [AllowAnonymous]
        [HttpPost("register")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var email = request.Email.Trim();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = request.FullName.Trim(),
                LastName = request.LastName.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    message = "User creation failed",
                    errors = result.Errors
                });
            }
            return Ok(new
            {
                message = "User Registered Successfully"
            });
        }

        /// <summary>Exchanges email and password for a JWT access token.</summary>
        /// <remarks>Send the token as <c>Authorization: Bearer &lt;token&gt;</c>. Five failed attempts lock the account for 15 minutes.</remarks>
        [AllowAnonymous]
        [HttpPost("login")]
        [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user == null)
            {
                return InvalidCredentials();
            }

            // Uses Identity's lockout and email confirmation rules without issuing a cookie.
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return result.IsLockedOut
                    ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account is temporarily locked. Try again later.")
                    : InvalidCredentials();
            }

            var token = _tokens.CreateToken(user, await _userManager.GetRolesAsync(user));
            return Ok(new LoginResponse { AccessToken = token.Token, ExpiresAtUtc = token.ExpiresAtUtc });
        }

        /// <summary>Returns the user the access token belongs to.</summary>
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType(typeof(CurrentUserResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> Me()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = userId == null ? null : await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Unauthorized();
            }

            return Ok(new CurrentUserResponse
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                Roles = await _userManager.GetRolesAsync(user),
            });
        }

        // Same message for unknown email and wrong password, so accounts cannot be enumerated.
        private IActionResult InvalidCredentials() =>
            Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
    }
}
