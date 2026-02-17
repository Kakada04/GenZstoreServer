using GenZStore.Data;
using GenZStore.Models;
using GenZStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GenZStore.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly TokenService _tokenService;
        private readonly AppDbContext _context;
        public AuthController(UserManager<ApplicationUser> userManager,
                              SignInManager<ApplicationUser> signInManager,
                              TokenService tokenService,
                              AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _tokenService = tokenService;
            _context = context;
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null || !await _userManager.CheckPasswordAsync(user, dto.Password))
            {
                return Unauthorized(new { Message = "Invalid credentials" });
            }

            // Only allow Admins to login here
            if (user.Role != "Owner") return Unauthorized(new { Message = "Admins only" });

            // Generate Tokens
            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();

            // Save Refresh Token to DB
            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }
        // GET: api/auth/profile
        [HttpGet("profile")]
        [Authorize] // ?? Requires Access Token
        public async Task<IActionResult> GetProfile()
        {
            // Get User ID from the Token Claims
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound("User not found");

            return Ok(new
            {
                Id = user.Id,
                Username = user.UserName,
                Role = user.Role,
                // You can add an Avatar URL field to ApplicationUser if you want
                AvatarUrl = $"https://ui-avatars.com/api/?name={user.UserName}&background=000&color=fff"
            });
        }

        // POST: api/auth/logout
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (userId == null) return Unauthorized();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // ?? Destroy the Refresh Token in DB
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            await _userManager.UpdateAsync(user);

            return Ok(new { Message = "Logged out successfully" });
        }
        // POST: api/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
        {
            if (dto is null) return BadRequest("Invalid client request");

            // Find user by Refresh Token (simplified)
            // ideally you validate access token principal too
            var user = _userManager.Users.FirstOrDefault(u => u.RefreshToken == dto.RefreshToken);

            if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
            {
                return BadRequest("Invalid or expired refresh token");
            }

            var newAccessToken = _tokenService.GenerateAccessToken(user);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            await _userManager.UpdateAsync(user);

            return Ok(new
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken
            });
        }
        // ... inside AuthController class

        // 1️⃣ MOVE THIS CLASS HERE (Inside Controller is fine, just keep it simple)
        public class AutoRegisterRequest
        {
            public string TelegramId { get; set; }
        }

        [AllowAnonymous]
        [HttpPost("auto-register")]
        public async Task<IActionResult> AutoRegister([FromBody] AutoRegisterRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TelegramId))
            {
                return BadRequest(new { Message = "Invalid request. TelegramId is required." });
            }

            var telegramId = request.TelegramId;

            try
            {
                // 1. Find or Create User (Identity)
                var user = await _userManager.FindByNameAsync(telegramId);

                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = telegramId,
                        Email = $"{telegramId}@telegram.local",
                        Role = "Customer",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    var result = await _userManager.CreateAsync(user, "GenZStore@2026!");
                    if (!result.Succeeded)
                    {
                        return BadRequest(new { Message = "User creation failed", Errors = result.Errors });
                    }
                }

                // 2. Find or Create Customer (Business Logic)
                var customer = await _context.Customers.FirstOrDefaultAsync(c => c.TelegramId == telegramId);

                if (customer == null)
                {
                    customer = new Customer
                    {
                        Id = Guid.NewGuid(),

                        // ✅ FIX: Link Customer to the User (This caused the 500 error!)
                        UserId = user.Id,

                        TelegramId = telegramId,
                        Name = telegramId, // Default name is ID, user can update later
                        CreatedAt = DateTime.UtcNow
                    };

                    await _context.Customers.AddAsync(customer);
                    await _context.SaveChangesAsync();
                }

                // 3. Generate Tokens
                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                user.RefreshToken = refreshToken;
                user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
                await _userManager.UpdateAsync(user);

                return Ok(new
                {
                    Message = "Auth successful",
                    UserId = user.Id,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken
                });
            }
            catch (Exception ex)
            {
                // Log the real error to console so you can see it in Vercel/Server logs
                Console.WriteLine($"Auth Error: {ex.Message} \n {ex.InnerException?.Message}");
                return StatusCode(500, new { Message = "Internal Server Error", Error = ex.Message });
            }
        }
        public class LoginDto
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class RefreshTokenDto
        {
            public string AccessToken { get; set; }
            public string RefreshToken { get; set; }
        }
    }

}