using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using bingooo;
using bingooo.Models;
using bingooo.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace bingooo.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UserCommissionsController : ControllerBase
{
    private readonly UserCommissionsService _userCommissionsService;
    private readonly ApiSettings _apiSettings;
        private readonly ILogger<UserCommissionsController> _logger;
        private readonly ApplicationDbContext _context;

        public UserCommissionsController(
            UserCommissionsService userCommissionsService, 
            IOptions<ApiSettings> apiSettings,
            ILogger<UserCommissionsController> logger,
            ApplicationDbContext context)
    {
        _userCommissionsService = userCommissionsService;
        _apiSettings = apiSettings.Value;
            _logger = logger;
            _context = context;
    }

        [HttpGet("api-settings")]
    public IActionResult ApiSetting()
    {
            return Ok(new { success = true, baseUrl = _apiSettings.BaseUrl });
    }

    // GET: Fetch user commissions details
        [HttpGet("edit/{userId}")]
    public async Task<IActionResult> Edit(string userId)
    {
        try
        {
                var commissions = await _userCommissionsService.GetUserCommissionsAsync(userId);

            if (commissions == null)
            {
                    return NotFound(new { success = false, message = "No commissions found for this user." });
                }

                return Ok(new { success = true, data = commissions });
        }
        catch (Exception ex)
        {
                _logger.LogError(ex, "Error fetching user commissions for userId: {UserId}", userId);
                return StatusCode(500, new { success = false, message = "An error occurred while fetching commissions." });
            }
        }

        // POST: Save user commissions details
        [HttpPost("save")]
        public async Task<IActionResult> SaveCommissions([FromBody] SaveCommissionsRequest request)
    {
        try
        {
                if (request == null || string.IsNullOrEmpty(request.UserId) || request.Ranges == null)
                {
                    return BadRequest(new { success = false, message = "Invalid request data. UserId and Ranges are required." });
                }

                _logger.LogInformation("Saving commissions for user: {UserId} with {RangeCount} ranges", 
                    request.UserId, request.Ranges.Count);

                var result = await _userCommissionsService.SaveUserCommissionsAsync(request.UserId, request.Ranges);

            if (result)
            {
                    _logger.LogInformation("Successfully saved commissions for user: {UserId}", request.UserId);
                    return Ok(new { success = true, message = "Commissions saved successfully." });
                }
                else
                {
                    _logger.LogWarning("Failed to save commissions for user: {UserId}", request.UserId);
                    return BadRequest(new { success = false, message = "Failed to save commissions." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving commissions for user: {UserId}", request?.UserId);
                return StatusCode(500, new { success = false, message = "An unexpected error occurred while saving commissions." });
            }
        }

        // GET: Test database connectivity and basic operations
        [HttpGet("test-db")]
        public async Task<IActionResult> TestDatabase()
        {
            try
            {
                // Test reading from database
                var totalCommissions = await _context.UserCommissions.CountAsync();
                _logger.LogInformation("Database test: Found {Count} total commission records", totalCommissions);

                // Test writing to database (create a test record)
                var testRecord = new UserCommission
                {
                    UserId = "test-user-" + DateTime.UtcNow.AddHours(3).Ticks,
                    MinCount = 1,
                    MaxCount = 2,
                    Multiplier = 0.5m,
                    Index_value = 12345
                };

                _context.UserCommissions.Add(testRecord);
                var saveResult = await _context.SaveChangesAsync();
                _logger.LogInformation("Database test: Created test record with {RowsAffected} rows affected", saveResult);

                // Verify the test record was created
                var testRecordFromDb = await _context.UserCommissions
                    .FirstOrDefaultAsync(uc => uc.UserId == testRecord.UserId);
                
                var testRecordExists = testRecordFromDb != null;
                _logger.LogInformation("Database test: Test record exists in database: {Exists}", testRecordExists);

                // Clean up test record
                if (testRecordFromDb != null)
                {
                    _context.UserCommissions.Remove(testRecordFromDb);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Database test: Cleaned up test record");
                }

                return Ok(new { 
                    success = true, 
                    message = "Database test completed successfully",
                    totalCommissions,
                    testRecordCreated = saveResult > 0,
                    testRecordVerified = testRecordExists
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database test failed");
                return StatusCode(500, new { success = false, message = "Database test failed: " + ex.Message });
            }
        }
    }

    // Request model for saving commissions
    public class SaveCommissionsRequest
    {
        public string UserId { get; set; } = string.Empty;
        public List<CommissionRangeRequest> Ranges { get; set; } = new();
    }
}