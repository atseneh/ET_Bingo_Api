using bingooo.data;
using bingooo.Models;
using bingooo.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace bingooo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly JwtService _jwtService;
        private readonly ApiSettings _apiSettings;
        private readonly ILogger<AccountController> _logger;
        ApplicationDbContext _context;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            JwtService jwtService,
            IOptions<ApiSettings> apiSettings,
            ILogger<AccountController> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _apiSettings = apiSettings.Value;
            _logger = logger;
            _context = context;
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                var user = await _userManager.FindByNameAsync(model.Username);
                if (user == null)
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    });
                }

                if (!user.isActive)
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "you are not eligible to login please contact admin"
                    });
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);
                if (!result.Succeeded)
                {
                    return Unauthorized(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    });
                }

                // Generate JWT token
                var token = await _jwtService.GenerateJwtTokenAsync(user);
                var refreshToken = await _jwtService.GenerateRefreshTokenAsync();

                // Get user roles
                var userRoles = await _userManager.GetRolesAsync(user);

                var userInfo = new UserInfoDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? "",
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    Address = user.Address ?? "",
                    ShopName = user.ShopName ?? "",
                    GameRule = user.GameRule ?? "",
                    AdminRole = user.isAdmin ? 1 : 0,
                    Roles = userRoles.ToList()
                };

                

                _logger.LogInformation("User {Username} logged in successfully", model.Username);

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(3).AddMinutes(Convert.ToDouble(1440)), // 1440 minutes
                    User = userInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user: {Username}", model.Username);
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during login"
                });
            }
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Invalid request data"
                    });
                }

                // Check if user already exists
                var existingUser = await _userManager.FindByNameAsync(model.Username);
                if (existingUser != null)
                {
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = "Username already exists"
                    });
                }

                // Check if email exists (only if email is provided)
                if (!string.IsNullOrEmpty(model.Email))
                {
                    var existingEmail = await _userManager.FindByEmailAsync(model.Email);
                    if (existingEmail != null)
                    {
                        return BadRequest(new AuthResponseDto
                        {
                            Success = false,
                            Message = "Email already exists"
                        });
                    }
                }

                // Create new user
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    Email = model.Email ?? "", // Use empty string if email is null
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address ?? "", // Use empty string if address is null
                    ShopName = model.ShopName,
                    isAdmin = false, // Default to regular user
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (!result.Succeeded)
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    return BadRequest(new AuthResponseDto
                    {
                        Success = false,
                        Message = $"Registration failed: {errors}"
                    });
                }

                // Generate JWT token for the new user
                var token = await _jwtService.GenerateJwtTokenAsync(user);
                var refreshToken = await _jwtService.GenerateRefreshTokenAsync();

                var userInfo = new UserInfoDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? "",
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    Address = user.Address ?? "",
                    ShopName = user.ShopName ?? "",
                    AdminRole = user.isAdmin ? 1 : 0,
                    Roles = new List<string>()
                };

                _logger.LogInformation("User {Username} registered successfully", model.Username);

                return Ok(new AuthResponseDto
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(3).AddMinutes(Convert.ToDouble(60)), // 60 minutes
                    User = userInfo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user: {Username}", model.Username);
                return StatusCode(500, new AuthResponseDto
                {
                    Success = false,
                    Message = "An error occurred during registration"
                });
            }
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto model)
        {
            try
            {
                var principal = _jwtService.GetPrincipalFromExpiredToken(model.Token);
                if (principal == null)
                {
                    return BadRequest(new RefreshTokenResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new RefreshTokenResponseDto
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return BadRequest(new RefreshTokenResponseDto
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Generate new token
                var newToken = await _jwtService.GenerateJwtTokenAsync(user);
                var newRefreshToken = await _jwtService.GenerateRefreshTokenAsync();

                return Ok(new RefreshTokenResponseDto
                {
                    Success = true,
                    Message = "Token refreshed successfully",
                    Token = newToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddHours(3).AddMinutes(Convert.ToDouble(60)) // 60 minutes
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return StatusCode(500, new RefreshTokenResponseDto
                {
                    Success = false,
                    Message = "An error occurred while refreshing token"
                });
            }
        }

        [Authorize]
        [HttpGet("profile")]
        public async Task<IActionResult> GetProfile()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var userRoles = await _userManager.GetRolesAsync(user);

                var userInfo = new UserInfoDto
                {
                    Id = user.Id,
                    Username = user.UserName ?? "",
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    PhoneNumber = user.PhoneNumber ?? "",
                    Address = user.Address ?? "",
                    ShopName = user.ShopName ?? "",
                    AdminRole = user.isAdmin ? 1 : 0,
                    Roles = userRoles.ToList()
                };

                return Ok(new { success = true, data = userInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user profile");
                return StatusCode(500, new { success = false, message = "An error occurred while getting profile" });
            }
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                // In JWT authentication, logout is typically handled client-side by removing the token
                // But we can log the logout event
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var username = User.FindFirst(ClaimTypes.Name)?.Value;

                _logger.LogInformation("User {Username} logged out", username);

                return Ok(new { success = true, message = "Logout successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during logout");
                return StatusCode(500, new { success = false, message = "An error occurred during logout" });
            }
        }

        [Authorize]
        [HttpGet("is-admin")]
        public async Task<IActionResult> IsAdmin()
        {
            try
            {
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { success = false, message = "User not authenticated" });
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found" });
                }

                var isAdmin = user.isAdmin || User.IsInRole("Admin");

                return Ok(new { success = true, isAdmin = isAdmin });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking admin status");
                return StatusCode(500, new { success = false, message = "An error occurred while checking admin status" });
            }
        }

        [HttpGet("api-settings")]
        public IActionResult ApiSetting()
        {
            return Ok(new { success = true, baseUrl = _apiSettings.BaseUrl });
        }
    }
}