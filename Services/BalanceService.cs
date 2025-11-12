using bingooo.data;
using bingooo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Threading.Tasks;
using YourNamespace.Models;

namespace bingooo.Services
{
    public class BalanceService
    {
        private readonly ApplicationDbContext _context;

        public BalanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TopUpResult> TopUpUserBalance(string userId, decimal balance)
        {
            try
            {
                if (string.IsNullOrEmpty(userId) || balance <= 0)
                {
                    return new TopUpResult
                    {
                        Success = false,
                        Message = "Invalid user or amount."
                    };
                }
                var amount = new Balance
                {
                    UserId = userId,
                    BalanceAmount = balance,
                    Date = DateTime.UtcNow.AddHours(3),
                    IsTopUp = true
                };
                _context.Balance.Add(amount);
                // Save changes to the database
                await _context.SaveChangesAsync();

                return new TopUpResult
                {
                    Success = true,
                    Message = $"Top-Up of {amount:C} applied successfully to user {userId}."
                };

            }
            catch (Exception ex)
            {
                // Log the exception (optional)
                // Log.Error(ex, "An error occurred while topping up user balance.");

                return new TopUpResult
                {
                    Success = false,
                    Message = "An error occurred while applying the top-up."
                };
            }

        }



        /// <summary>
        /// Calculates the balance for a specific user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A tuple containing the UserId and the calculated balance.</returns>
        public async Task<(string UserId, decimal CalculatedBalance, string Message)> Savetopup(string userId)
        {
            // Validate input
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
            }

            // Check if the user exists in the AspNetUsers table
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
            }

            // Calculate the balance
            var topUps = await _context.Balance
                .Where(b => b.UserId == userId && b.IsTopUp)
                .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

            var message = "balance topup successfull!";
            // Return the result
            return (userId, topUps, message);
        }

        /// <summary>
        /// Checks if a user is eligible based on their calculated balance.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>True if the user is eligible (balance > 0), otherwise false.</returns>
        public async Task<bool> CheckEligibility(string userId)
        {
            // Calculate the balance
            var (_, calculatedBalance) = await CalculateAndSaveBalance(userId);

            // Check eligibility
            return calculatedBalance > 0;
        }
        /// <summary>
        /// Automatically calculates and saves the user's balance by deducting deductions from top-ups.
        /// Also saves each deduction individually for reporting purposes.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <returns>A tuple containing the UserId and the calculated balance.</returns>
        public async Task<(string UserId, decimal CalculatedBalance)> CalculateAndSaveBalance(string userId)
        {
            // Validate input
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(userId));
            }

            // Check if the user exists in the AspNetUsers table
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
            {
                throw new InvalidOperationException($"User with ID '{userId}' does not exist.");
            }

            // Calculate the total top-ups and deductions
            var topUps = await _context.Balance
                .Where(b => b.UserId == userId && b.IsTopUp) // Only include top-ups
                .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

            var deductions = await _context.Balance
                .Where(b => b.UserId == userId && !b.IsTopUp) // Only include deductions
                .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

            var calculatedBalance = topUps - deductions;


            // Return the result
            return (userId, calculatedBalance);
        }

        /// <summary>
        /// Calculates the commission and winning amount for a user.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="selectedCartelas">The number of selected cartelas.</param>
        /// <param name="bettingAmount">The betting amount per cartela.</param>
        /// <returns>A tuple containing the Commission and WinningAmount.</returns>
        public async Task<Balance> CalculateCommissionAndWinningAmount(
            ApplicationUser? loggedInUser, int selectedCartelas, decimal bettingAmount)
        {
            // Validate input
            if (loggedInUser == null)
            {
                throw new ArgumentException("User cannot be null.", nameof(loggedInUser));
            }

            if (string.IsNullOrEmpty(loggedInUser.Id))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(loggedInUser.Id));
            }

            if (selectedCartelas <= 0)
            {
                throw new ArgumentException("Selected cartelas must be greater than zero.", nameof(selectedCartelas));
            }

            if (bettingAmount <= 0)
            {
                throw new ArgumentException("Betting amount must be greater than zero.", nameof(bettingAmount));
            }

            // Check if the user has sufficient balance
            bool hasSufficientBalance = await CheckAvailabilityBalance(loggedInUser, bettingAmount, selectedCartelas);
            if (!hasSufficientBalance)
            {
                throw new InvalidOperationException("Insufficient balance to proceed with the transaction.");
            }

            // Retrieve the relevant UserCommission record
            var userCommission = await _context.UserCommissions
                .Where(uc => uc.UserId == loggedInUser.Id && uc.MaxCount >= selectedCartelas)
                .OrderBy(uc => uc.MaxCount)
                .FirstOrDefaultAsync();

            if (userCommission == null)
            {
                throw new InvalidOperationException($"No commission configuration found for user ID '{loggedInUser.Id}' with selected cartelas {selectedCartelas}.");
            }

            // Calculate the Commission and WinningAmount
            decimal commission;
            if(selectedCartelas <= 2)
            {
                commission = 0;
            }
            else
            {
                commission = userCommission.Multiplier * selectedCartelas * bettingAmount;
            }
            decimal winningAmount = (selectedCartelas * bettingAmount) - commission;

            // Deduct the usage from the user's balance
            var newRecord = await SaveUsage(loggedInUser, commission, selectedCartelas, bettingAmount, winningAmount);
            // Return the result
            return newRecord;
        }

        /// <summary>
        /// Checks if the user's remaining balance is sufficient for the betting amount.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="bettingAmount">The betting amount to check against the balance.</param>
        /// <returns>True if the calculated balance is greater than or equal to the betting amount; otherwise, false.</returns>
        public async Task<bool> CheckAvailabilityBalance(ApplicationUser? loggedInUser, decimal bettingAmount, int selectedCartelas)
        {
            // Validate input
            if (loggedInUser == null)
            {
                throw new ArgumentException("User cannot be null.", nameof(loggedInUser));
            }

            if (string.IsNullOrEmpty(loggedInUser.Id))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(loggedInUser.Id));
            }

            if (bettingAmount <= 0)
            {
                throw new ArgumentException("Betting amount must be greater than zero.", nameof(bettingAmount));
            }

            // Retrieve the relevant UserCommission record
            var userCommission = await _context.UserCommissions
                .Where(uc => uc.UserId == loggedInUser.Id && uc.MaxCount >= selectedCartelas)
                .OrderBy(uc => uc.MaxCount)
                .FirstOrDefaultAsync();

            // Check if commission configuration exists
            if (userCommission == null)
            {
                throw new InvalidOperationException($"No commission configuration found for user ID '{loggedInUser.Id}' with selected cartelas {selectedCartelas}.");
            }

            // Calculate the total top-ups and deductions
            var topUps = await _context.Balance
                .Where(b => b.UserId == loggedInUser.Id && b.IsTopUp)
                .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

            var deductions = await _context.Balance
                .Where(b => b.UserId == loggedInUser.Id && !b.IsTopUp)
                .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

            var calculatedBalance = topUps - deductions;
            decimal commission = userCommission.Multiplier * selectedCartelas * bettingAmount;

            // Check if the calculated balance is sufficient
            return calculatedBalance >= commission;
        }
        /// <summary>
        /// Deducts the betting amount multiplied by the number of selected cartelas and saves the deduction as a new record.
        /// </summary>
        /// <param name="userId">The ID of the user.</param>
        /// <param name="bettingAmount">The betting amount per cartela.</param>
        /// <param name="selectedCartelas">The number of selected cartelas.</param>
        /// <returns>A tuple containing the UserId and the deduction amount.</returns>
        public async Task<Balance> SaveUsage(ApplicationUser? loggedInUser, decimal commission, int selectedCartelas, decimal bettingAmount, decimal winningAmount)
        {
            // Validate input
            if (loggedInUser == null)
            {
                throw new ArgumentException("User cannot be null.", nameof(loggedInUser));
            }

            if (string.IsNullOrEmpty(loggedInUser.Id))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(loggedInUser.Id));
            }

            if (commission < 0)
            {
                throw new ArgumentException("Betting amount must be greater than zero.", nameof(commission));
            }

            if (selectedCartelas <= 0)
            {
                throw new ArgumentException("Selected cartelas must be greater than zero.", nameof(selectedCartelas));
            }

            // Calculate the total deduction
            var totalDeduction = commission;

            // Save the deduction as a new record in the Balance table
            var deductionRecord = new Balance
            {
                UserId = loggedInUser.Id,
                BalanceAmount = totalDeduction, // Negative value indicates a deduction
                Date = DateTime.UtcNow.AddHours(3),
                StartedTime = DateTime.UtcNow.AddHours(3),
                NoCards = selectedCartelas,
                Price = bettingAmount,
                Collected = bettingAmount * selectedCartelas,
                Comission = commission,
                ShopName = loggedInUser.ShopName ?? "Unknown Shop",
                IsTopUp = false,
                WinningAmount = winningAmount
            };

            _context.Balance.Add(deductionRecord);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving the deduction.", ex);
            }

            // Return the result
            return (deductionRecord);
        }
        public async Task<Balance> UpdateEndGame(ApplicationUser? loggedInUser, int id, int winningCartela, int onCall)
        {
            // Validate input
            if (loggedInUser == null)
            {
                throw new ArgumentException("User cannot be null.", nameof(loggedInUser));
            }

            if (string.IsNullOrEmpty(loggedInUser.Id))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(loggedInUser.Id));
            }
            try
            {
                var endRecord = await _context.Balance.FindAsync(id);
                if (endRecord == null)
                {
                    throw new InvalidOperationException($"No record found with ID '{id}'.");
                }
                endRecord.EndedTime = DateTime.UtcNow.AddHours(3);
                endRecord.OnCall = onCall;
                //TODO: Save the winning cartela
                _context.Update(endRecord);
                await _context.SaveChangesAsync();
                return (endRecord);
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving the deduction.", ex);
            }

            // Return the result

        }

        public async Task<Sales> SaveBonus(ApplicationUser? loggedInUser, decimal transactionAmount, string transactionType)
        {
            // Validate input
            if (loggedInUser == null)
            {
                throw new ArgumentException("User cannot be null.", nameof(loggedInUser));
            }

            if (string.IsNullOrEmpty(loggedInUser.Id))
            {
                throw new ArgumentException("User ID cannot be null or empty.", nameof(loggedInUser.Id));
            }

            if (transactionAmount < 0)
            {
                throw new ArgumentException("bonus amount must be greater than zero.", nameof(transactionAmount));
            }

            // Calculate totalBonus: sum of all deposits for this user
            // Find the latest withdrawal date for the user
            var latestWithdrawDate = await _context.Sales
                .Where(s => s.UserId == loggedInUser.Id && s.TransactionType == "Withdraw")
                .OrderByDescending(s => s.Date)
                .Select(s => (DateTime?)s.Date)
                .FirstOrDefaultAsync();

            // Sum deposits after the latest withdrawal (or all if no withdrawal exists)
            var totalBonus = await _context.Sales
                .Where(s => s.UserId == loggedInUser.Id
                    && s.TransactionType == "Deposit"
                    && (latestWithdrawDate == null || s.Date > latestWithdrawDate))
                .SumAsync(s => (decimal?)s.TransactionAmount) ?? 0;

            decimal amountToSave = transactionAmount;

            if (transactionType == "Withdraw")
            {
                // Save the totalBonus as a negative value for withdrawal
                amountToSave = -totalBonus;

                // Optionally, reset all previous deposits for this user (mark as withdrawn)
                // This can be done by updating a flag or removing them, but here we just reset totalBonus
                totalBonus = 0;
            }

            // Save the bonus as a new record in the sales table
            var BonusRecord = new Sales
            {
                UserId = loggedInUser.Id,
                ShopName = loggedInUser.ShopName,
                Date = DateTime.UtcNow.AddHours(3),
                TransactionAmount = amountToSave,
                TransactionType = transactionType
            };

            _context.Sales.Add(BonusRecord);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred while saving the deduction.", ex);
            }

            // Return the result
            return BonusRecord;
        }
    }
}






