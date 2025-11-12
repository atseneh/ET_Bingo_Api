using bingooo.data;
using bingooo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class UserCommissionsService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserCommissionsService> _logger;

    public UserCommissionsService(ApplicationDbContext context, ILogger<UserCommissionsService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserCommissionsDto> GetUserCommissionsAsync(string userId)
    {
        try
        {
            var commissions = await _context.UserCommissions
                .Where(uc => uc.UserId == userId)
                .Select(uc => new CommissionRangeDto
                {
                    MinCount = uc.MinCount,
                    MaxCount = uc.MaxCount,
                    Multiplier = uc.Multiplier
                })
                .ToListAsync();
            // Define default ranges if no commissions exist
            var defaultRanges = new List<CommissionRangeDto>
        {
            new CommissionRangeDto { MinCount = 3, MaxCount = 5, Multiplier = 0 },
            new CommissionRangeDto { MinCount = 6, MaxCount = 10, Multiplier = 0.1M },
            new CommissionRangeDto { MinCount = 11, MaxCount = 20, Multiplier = 0.2M },
            new CommissionRangeDto { MinCount = 21, MaxCount = 30, Multiplier = 0.25M },
            new CommissionRangeDto { MinCount = 31, MaxCount = 40, Multiplier = 0.28M },
            new CommissionRangeDto { MinCount = 41, MaxCount = 50, Multiplier = 0.35M },
            new CommissionRangeDto { MinCount = 51, MaxCount = 60, Multiplier = 0.38M },
            new CommissionRangeDto { MinCount = 61, MaxCount = 150, Multiplier = 0.4M } // >60
        };

            return new UserCommissionsDto
            {
                UserId = userId,
                Ranges = commissions.Count == 0? defaultRanges:commissions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user commissions for userId: {UserId}", userId);
            throw new Exception("An error occurred while fetching user commissions.", ex);
        }
    }

    public async Task<bool> SaveUserCommissionsAsync(string userId, List<CommissionRangeRequest> ranges)
    {
        try
        {
            _logger.LogInformation("Starting to save commissions for user: {UserId} with {RangeCount} ranges", userId, ranges.Count);

            // First, ensure all standard commission ranges exist for this user
            await EnsureStandardRangesExistAsync(userId);

            // Get all existing commissions for this user (now including any newly created standard ranges)
            var existingCommissions = await _context.UserCommissions
                .Where(uc => uc.UserId == userId)
                .ToListAsync();

            _logger.LogInformation("Found {ExistingCount} existing commissions for user: {UserId}", existingCommissions.Count, userId);

            // Track what we're doing
            int updatedCount = 0;

            foreach (var range in ranges)
            {
                _logger.LogDebug("Processing range: MinCount={MinCount}, MaxCount={MaxCount}, Multiplier={Multiplier}", 
                    range.MinCount, range.MaxCount, range.Multiplier);

                // Find existing record by UserId + MinCount + MaxCount combination
                var existingRecord = existingCommissions
                    .FirstOrDefault(uc => uc.MinCount == range.MinCount && uc.MaxCount == range.MaxCount);

                if (existingRecord != null)
                {
                    _logger.LogDebug("Updating existing record with ID: {RecordId} from Multiplier: {OldMultiplier} to {NewMultiplier}", 
                        existingRecord.Id, existingRecord.Multiplier, range.Multiplier);
                    
                    // Only update the multiplier, keep everything else the same
                    existingRecord.Multiplier = range.Multiplier;
                    updatedCount++;
                }
                else
                {
                    _logger.LogWarning("No existing record found for range: {MinCount}-{MaxCount}, this should not happen after ensuring standard ranges", 
                        range.MinCount, range.MaxCount);
                }
            }

            _logger.LogInformation("Summary: {UpdatedCount} records updated", updatedCount);

            // Save changes to database
            var saveResult = await _context.SaveChangesAsync();
            _logger.LogInformation("Database save completed. {AffectedRows} rows affected for user: {UserId}", saveResult, userId);

            // Verify the save by reading back the data
            if (saveResult > 0)
            {
                var verificationData = await _context.UserCommissions
                    .Where(uc => uc.UserId == userId)
                    .Select(uc => new { uc.MinCount, uc.MaxCount, uc.Multiplier })
                    .ToListAsync();
                
                _logger.LogInformation("Verification: Found {VerificationCount} records in database for user: {UserId}", 
                    verificationData.Count, userId);
                
                foreach (var record in verificationData)
                {
                    _logger.LogDebug("Verification record: MinCount={MinCount}, MaxCount={MaxCount}, Multiplier={Multiplier}", 
                        record.MinCount, record.MaxCount, record.Multiplier);
                }
            }

            return saveResult > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving user commissions for userId: {UserId}", userId);
            throw new Exception("An error occurred while saving user commissions.", ex);
        }
    }

    private async Task EnsureStandardRangesExistAsync(string userId)
    {
        try
        {
            // Define standard commission ranges
            var standardRanges = new List<(int MinCount, int MaxCount)>
            {
                (3, 5),
                (6, 10),
                (11, 20),
                (21, 30),
                (31, 40),
                (41, 50),
                (51, 60),
                (61, 150) // >60
            };

            // Check which standard ranges don't exist for this user
            var existingRanges = await _context.UserCommissions
                .Where(uc => uc.UserId == userId)
                .Select(uc => new { uc.MinCount, uc.MaxCount })
                .ToListAsync();

            var missingRanges = standardRanges
                .Where(sr => !existingRanges.Any(er => er.MinCount == sr.MinCount && er.MaxCount == sr.MaxCount))
                .ToList();

            if (missingRanges.Any())
            {
                _logger.LogInformation("Creating {MissingCount} missing standard ranges for user: {UserId}", missingRanges.Count, userId);
                
                foreach (var range in missingRanges)
                {
                    var newRecord = new UserCommission
                    {
                        UserId = userId,
                        MinCount = range.MinCount,
                        MaxCount = range.MaxCount,
                        Multiplier = 0, // Default multiplier
                        Index_value = ($"{userId}-{range.MinCount}-{range.MaxCount}").GetHashCode()
                    };
                    _context.UserCommissions.Add(newRecord);
                    _logger.LogDebug("Created standard range: {MinCount}-{MaxCount} for user: {UserId}", range.MinCount, range.MaxCount, userId);
                }

                // Save the new standard ranges
                await _context.SaveChangesAsync();
                _logger.LogInformation("Standard ranges created successfully for user: {UserId}", userId);
            }
            else
            {
                _logger.LogDebug("All standard ranges already exist for user: {UserId}", userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring standard ranges exist for userId: {UserId}", userId);
            throw;
        }
    }

   
    
}