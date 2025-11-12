using bingooo.data;
using bingooo.Models;
using bingooo.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace bingooo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BalanceController : ControllerBase
    {
        private readonly BalanceService _balanceService;
        private readonly ApplicationDbContext _context;
        private readonly ApiSettings _apiSettings;

        public BalanceController(BalanceService balanceService, ApplicationDbContext context, IOptions<ApiSettings> apiSettings)
        {
            _balanceService = balanceService;
            _context = context;
            _apiSettings = apiSettings.Value;

        }

        [HttpGet("api-settings")]
        public IActionResult ApiSetting()
        {
            return Ok(new { success = true, baseUrl = _apiSettings.BaseUrl });
        }

        [HttpPost("topup")]
        public async Task<IActionResult> TopUpBalance(string userId, decimal balance)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(userId) || balance <= 0)
                {
                    return BadRequest(new { success = false, message = "Invalid input parameters" });
                }

                // Call the TopUpUserBalance service to handle the top-up logic
                var result = await _balanceService.TopUpUserBalance(userId, balance);

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while applying the top-up." });
            }
        }

        /// <summary>
        /// Checks the balance for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>The UserId and the calculated balance.</returns>
        [HttpGet("{userId}/balance")]
        public async Task<IActionResult> GetBalance(string userId)
        {
            try
            {
                var (UserId, CalculatedBalance, message) = await _balanceService.Savetopup(userId);
                return Ok(new { message, CalculatedBalance });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks the eligibility of a specific user based on their balance.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is eligible, otherwise false.</returns>
        [HttpGet("{userId}/eligibility")]
        public async Task<IActionResult> CheckEligibility(string userId)
        {
            try
            {
                var isEligible = await _balanceService.CheckEligibility(userId);
                return Ok(new { IsEligible = isEligible });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        [HttpGet("{userId}/calculate-balance")]
        public async Task<IActionResult> CalculateAndSaveBalance(string userId)
        {
            try
            {
                var (UserId, CalculatedBalance) = await _balanceService.CalculateAndSaveBalance(userId);
                return Ok(new { UserId, CalculatedBalance});
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        /// <summary>
        /// Calculates the commission and winning amount for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="selectedCartelas">The number of selected cartelas.</param>
        /// <param name="bettingAmount">The betting amount per cartela.</param>
        /// <returns>The Commission and WinningAmount.</returns>
        [HttpGet("calculate/calculate-commission")]
        public async Task<IActionResult> CalculateCommissionAndWinningAmount(
       int selectedCartelas, decimal bettingAmount)
        {
            try
            {
                
                var userName = User.Identity.Name;
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (loggedInUser == null)
                {
                    return Unauthorized();
                }
                var newRecord = await _balanceService.CalculateCommissionAndWinningAmount(loggedInUser, selectedCartelas, bettingAmount);
                return Ok(newRecord);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        [HttpGet("save/end")]
        public async Task<IActionResult> UpdateEndGame(
       int id, int winningCartela,int onCall)
        {
            try
            {

                var userName = User.Identity.Name;
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (loggedInUser == null)
                {
                    return Unauthorized();
                }
                var newRecord = await _balanceService.UpdateEndGame(loggedInUser,id,winningCartela,onCall);
                return Ok(newRecord);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
        /// <summary>
        /// Checks if the user's remaining balance is sufficient for the betting amount.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="bettingAmount">The betting amount to check against the balance.</param>
        /// <returns>True if the calculated balance is greater than or equal to the betting amount; otherwise, false.</returns>
        [HttpGet("balance/check-balance")]
        public async Task<IActionResult> CheckAvailabilityBalance( decimal bettingAmount, int selectedCartelas)
        {
            try
            {
                var userName = User.Identity.Name;
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (loggedInUser == null)
                {
                    return Unauthorized();
                }
                // Call the service method to check balance availability
                bool hasSufficientBalance = await _balanceService.CheckAvailabilityBalance(loggedInUser, bettingAmount, selectedCartelas);

                // Return the result as JSON
                return Ok(new { HasSufficientBalance = hasSufficientBalance });
            }
            catch (ArgumentException ex)
            {
                // Handle invalid input errors
                return BadRequest(new { Message = ex.Message });
            }
            catch (Exception ex)
            {
                // Handle unexpected errors
                return StatusCode(500, new { Message = $"An error occurred: {ex.Message}" });
            }
        }

        // GET: Fetch all users' balance details


        // GET: Fetch the balance details for the currently logged-in user
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Get the UserId of the currently logged-in user
                var userName = User.Identity.Name;

                if (string.IsNullOrEmpty(userName))
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                // Fetch the user's details from the database using the username/email
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName || u.Email == userName);

                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                var userId = user.Id; // Use the retrieved UserId

                // Fetch the user's details from the database
                user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                // Calculate the user's balance
                var (UserId, CalculatedBalance) = await _balanceService.CalculateAndSaveBalance(userId); 

                // Get the latest top-up amount and date
                var latestTopUp = await _context.Balance
                    .Where(b => b.UserId == userId && b.IsTopUp)
                    .OrderByDescending(b => b.Date)
                    .FirstOrDefaultAsync();

                // Calculate the status percentage
                var credit = latestTopUp?.BalanceAmount ?? 0;
                var status = credit > 0 ? (CalculatedBalance / credit) * 100 : 0;

                // Define the percentage (default to 15%, 20%, or 25% based on your logic)
                var percentage = DeterminePercentage((double)status); // Helper method to determine percentage

                // Prepare the user's balance details
                var userBalance = new UserBalanceDto
                {
                    UserId = user.Id,
                    UserName = user.UserName,
                    FullName = user.FullName,
                    ShopName = user.ShopName,
                    Address = user.Address,
                    Credit = credit,
                    Balance = CalculatedBalance,
                    Status = Math.Round((double)status, 2),
                    Percentage = percentage,
                    LastTopUpDate = latestTopUp?.Date
                };

                return Ok(new { success = true, data = userBalance });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching balance details." });
            }
        }

        [HttpGet("saveBonus")]
        public async Task<IActionResult> SaveBonus(
       int id, decimal transactionAmount, string transactionType)
        {
            try
            {

                var userName = User.Identity.Name;
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (loggedInUser == null)
                {
                    return Unauthorized();
                }
                var newRecord = await _balanceService.SaveBonus(loggedInUser, transactionAmount, transactionType);
                return Ok(newRecord);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private int DeterminePercentage(double status)
        {
            if (status >= 25)
                return 25;
            else if (status >= 20)
                return 20;
            else
                return 15;
        }


    }
    }


