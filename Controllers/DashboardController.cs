using bingooo.data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;
using bingooo.Models;
using bingooo.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using static System.Runtime.InteropServices.JavaScript.JSType;
using YourNamespace.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace bingooo.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DashboardController : ControllerBase
    {
        ApplicationDbContext _context;
        private readonly BalanceService _balanceService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApiSettings _apiSettings;


        public DashboardController(ApplicationDbContext context, BalanceService balanceService, UserManager<ApplicationUser> userManager, IOptions<ApiSettings> apiSettings) {
            _context = context;
            _balanceService = balanceService;
            _userManager = userManager;
            _apiSettings = apiSettings.Value;

        }

        [HttpGet("api-settings")]
        public IActionResult ApiSetting()
        {
            return Ok(new { success = true, baseUrl = _apiSettings.BaseUrl });
        }

        // GET: Fetch dashboard data
        [HttpGet("index")]
        public async Task<IActionResult> Index()
        {
            try
            {
                // Fetch the currently logged-in user
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

                if (loggedInUser == null)
                {
                    return NotFound(new { success = false, message = "Logged-in user not found." });
                }

                // Fetch total games played today
                var today = DateTime.Today;
                int totalGamesPlayedToday;

                if (loggedInUser.isAdmin)
                {
                    // Admin: Fetch total games played by all non-admin users today
                    totalGamesPlayedToday = await _context.Balance
                        .Where(b => b.IsTopUp == false && b.Date.Date == today && !b.User.isAdmin)
                        .CountAsync();
                }
                else
                {
                    // Non-admin: Fetch total games played by the logged-in user today
                    totalGamesPlayedToday = await _context.Balance
                        .Where(b => b.UserId == loggedInUser.Id && b.IsTopUp == false && b.Date.Date == today)
                        .CountAsync();
                }

                // Fetch total cartelas (fixed value)
                const int totalCartelas = 150;

                // Fetch total users (excluding admins)
                var totalUsers = await _context.Users
                    .Where(u => !u.isAdmin)
                    .CountAsync();

                // Fetch total users (excluding admins)
                var totalShops = await _context.Users
                    .Where(u => !u.isAdmin)
                    .CountAsync();

                return Ok(new { 
                    success = true, 
                    data = new {
                        isAdmin = loggedInUser.isAdmin,
                        totalGamesPlayedToday,
                        totalCartelas,
                        totalUsers,
                        totalShops
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching dashboard data." });
            }
        }



        [HttpGet("sales")]
        public async Task<IActionResult> Sales(string userId, DateTime? date = null)
        {
            try
            {
                // Fetch the currently logged-in user's ID if not provided
                if (string.IsNullOrEmpty(userId))
                {
                    var userName = User.Identity.Name; // Get the username of the logged-in user
                    var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

                    if (loggedInUser == null)
                    {
                        return NotFound(new { success = false, message = "Logged-in user not found." });
                    }

                    userId = loggedInUser.Id; // Use the logged-in user's ID
                }

                // Validate input
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "User ID is missing." });
                }

                // Check if the user exists in the database
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Set the date to the current date if it is null or equals DateTime.MinValue (01/01/0001)
                if (date == null || date.Value == DateTime.MinValue)
                {
                    date = DateTime.Today; // Default to today's date
                }

                // Build the query to fetch sales actions, including the User navigation property
                var query = _context.Balance
                    .Where(b => b.UserId == userId && b.Date.Date == date.Value.Date && !b.IsTopUp)
                    .Include(b => b.User);

                // Fetch detailed sales actions for the user on the specified date
                var details = await query
                    .Select(b => new SalesActionDto
                    {
                        UserId = b.UserId,
                        UserName = b.User.UserName,
                        Commission = b.BalanceAmount,
                        DateTime = b.Date,
                        StartedTime = b.StartedTime,
                        EndedTime = b.EndedTime,
                        ShopName = b.ShopName,
                        OnCall = b.OnCall ?? 0,
                        NoCards = b.NoCards ?? 0,
                        Price = b.Price ?? 0,
                        Collected = b.Collected ?? 0,
                        WinningAmount = b.WinningAmount ?? 0
                    })
                    .ToListAsync();

                // Calculate the total commission balance (sum of all deductions)
                var totalCommissionBalance = await _context.Balance
                    .Where(b => b.UserId == userId && b.Date.Date == date.Value.Date && !b.IsTopUp) // Only include deductions
                    .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

                return Ok(new { 
                    success = true, 
                    data = new {
                        details,
                        totalCommissionBalance,
                        userId,
                        date
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching sales details." });
            }
        }

        [HttpGet("sales/summary")]
        public async Task<IActionResult> SalesSummary(DateTime? date = null)
        {
            try
            {
                if (date == null || date.Value == DateTime.MinValue)
                    date = DateTime.Today;

                var summaries = await _context.Balance
                    .Where(b => b.Date.Date == date.Value.Date && !b.IsTopUp)
                    .GroupBy(b => new { b.UserId, b.User.UserName, b.User.FullName, b.User.ShopName })
                    .Select(g => new
                    {
                        UserId = g.Key.UserId,
                        UserName = g.Key.UserName,
                        FullName = g.Key.FullName,
                        ShopName = g.Key.ShopName,
                        Games = g.Count(),
                        TotalCommission = g.Sum(x => x.BalanceAmount),
                        TotalPercent = g.Sum(x => x.WinningAmount ?? 0)
                    })
                    .OrderByDescending(x => x.TotalCommission)
                    .ToListAsync();

                return Ok(new { success = true, data = summaries });
            }
            catch
            {
                return StatusCode(500, new { success = false, message = "Error loading sales summary" });
            }
        }


        // GET: Fetch aggregated sales actions for all users
        [HttpGet("admin-sales")]
        public async Task<IActionResult> AdminSales(DateTime? dateFilter)
        {
            try
            {
                // Apply date filter if provided
                var query = _context.Balance.AsQueryable();
                if (dateFilter.HasValue)
                {
                    query = query.Where(b => b.Date.Date == dateFilter.Value.Date);
                }
                var date = dateFilter==null ? DateTime.UtcNow.AddHours(3).Date: dateFilter.Value.Date;
                // Aggregate data by user and date
                var adminSalesData = await query
                    .GroupBy(b => new { b.UserId})
                    .Select(g => new
                    {
                        g.Key.UserId,
                        date,
                        g.First().User.UserName, // Assuming User navigation property exists
                        g.First().User.FullName, // Assuming FullName is stored in the User table
                        g.First().User.ShopName, // Assuming FullName is stored in the User table
                        NumberOfGames = g.Count(b => !b.IsTopUp), // Count deductions (IsTopUp = false)
                        TotalDeductionAmount = g.Where(b => !b.IsTopUp).Sum(b => b.BalanceAmount) // Total deduction amount
                    }).Where(x => x.NumberOfGames > 0)
                    .ToListAsync();

                // Prepare the final list of AdminSalesDto
                var adminSalesList = new List<AdminSalesDto>();

                foreach (var sale in adminSalesData)
                {
                    // Default betting amount per game (you can adjust this logic)
                    decimal bettingAmount = 1; // Example: 1 unit per game

                    // Add the aggregated data to the list
                    adminSalesList.Add(new AdminSalesDto
                    {
                        UserId = sale.UserId,
                        Date = sale.date,
                        UserName = sale.UserName,
                        FullName = sale.FullName,
                        ShopName = sale.ShopName,
                        TotalCommission = sale.TotalDeductionAmount, // Use the calculated commission
                        NumberOfGames = sale.NumberOfGames
                    });
                }

                return Ok(new { 
                    success = true, 
                    data = new {
                        adminSalesList,
                        dateFilter
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching admin sales data." });
            }
        }

        //details for individual user
        [HttpGet("details/{userId}")]
        public async Task<IActionResult> Details(string userId, DateTime date)
        {
            try
            {
                // Validate input
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "User ID is missing." });
                }

                // Check if the user exists in the database
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Build the query to fetch sales actions for the specific user and date
                var query = _context.Balance
                    .Where(b => b.UserId == userId && b.Date.Date == date.Date);

                // Fetch detailed sales actions for the user on the specified date
                var details = await query
                    .Select(b => new SalesActionDto
                    {
                        UserId = b.UserId,
                        UserName = b.User.UserName,
                        Commission = b.BalanceAmount,
                        DateTime = b.Date,
                        StartedTime = b.StartedTime,
                        EndedTime = b.EndedTime,
                        ShopName = b.ShopName,
                        OnCall = b.OnCall ?? 0,
                        NoCards = b.NoCards ?? 0,
                        Price = b.Price ?? 0,
                        Collected = b.Collected ?? 0,
                        WinningAmount = b.WinningAmount ?? 0
                    })
                    .ToListAsync();

                // Calculate the total commission balance (sum of all deductions)
                var totalCommissionBalance = await _context.Balance
                    .Where(b => b.UserId == userId && b.Date.Date == date.Date && !b.IsTopUp) // Only include deductions
                    .SumAsync(b => (decimal?)b.BalanceAmount) ?? 0;

                return Ok(new { 
                    success = true, 
                    data = new {
                        details,
                        totalCommissionBalance,
                        userId,
                        date
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching user details." });
            }
        }



        // GET: Fetch and generate the sales report
        [HttpGet("admin-sales-report")]
        public async Task<IActionResult> AdminSalesReport(DateTime? startDate, DateTime? endDate, string username)
        {
            try
            {
                // Fetch all users except admins
                var users = await _context.Users
                    .Where(u => !u.isAdmin) // Assuming there's an `isAdmin` property in your User model
                    .Select(u => new { u.Id, u.UserName })
                    .ToListAsync();

                // Validate input if form is submitted
                if (!startDate.HasValue || !endDate.HasValue || string.IsNullOrEmpty(username))
                {
                    return BadRequest(new { success = false, message = "Please provide valid Start Date, End Date, and Username." });
                }

                // Fetch user by username
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Query the balance table for the specified date range and user
                var query = _context.Balance
                    .Where(b => b.UserId == user.Id && b.Date.Date >= startDate.Value.Date && b.Date.Date <= endDate.Value.Date);

                // Aggregate data
                var reportData = await query
                    .GroupBy(b => b.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.First().User.UserName,
                        NumberOfGames = g.Count(b => !b.IsTopUp), // Count deductions (IsTopUp = false)
                        TotalDeductionAmount = g.Where(b => !b.IsTopUp).Sum(b => b.BalanceAmount), // Total deduction amount
                        TotalBalanceTransaction = g.Sum(b => b.Collected) // Sum of all transactions
                    })
                    .FirstOrDefaultAsync();

                if (reportData == null)
                {
                    return NotFound(new { success = false, message = "No sales data found for the specified criteria." });
                }

                // Prepare the final report
                var salesReport = new SalesReportDto
                {
                    UserName = reportData.UserName,
                    NumberOfGames = reportData.NumberOfGames,
                    TotalCommission = reportData.TotalDeductionAmount,
                    TotalBalanceTransaction = reportData.TotalBalanceTransaction
                };

                return Ok(new { 
                    success = true, 
                    data = new {
                        salesReport,
                        startDate,
                        endDate,
                        username,
                        users
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while generating the sales report." });
            }
        }



        [HttpGet("sales-report")]
        public async Task<IActionResult> SalesReport(DateTime? startDate, DateTime? endDate)
        {
            try
            {
                // Fetch the currently logged-in user's ID
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

                if (loggedInUser == null)
                {
                    return Unauthorized(new { success = false, message = "Logged-in user not found." });
                }

                var userId = loggedInUser.Id; // Use the logged-in user's ID

                // Validate input dates
                // Query the balance table for the specified date range and user
                var query = _context.Balance
                    .Where(b => b.UserId == userId && b.Date.Date >= startDate.Value.Date && b.Date.Date <= endDate.Value.Date);

                // Aggregate data
                var reportData = await query
                    .GroupBy(b => b.UserId)
                    .Select(g => new
                    {
                        UserId = g.Key,
                        UserName = g.First().User.UserName,
                        NumberOfGames = g.Count(b => !b.IsTopUp), // Count deductions (IsTopUp = false)
                        TotalDeductionAmount = g.Where(b => !b.IsTopUp).Sum(b => b.BalanceAmount), // Total deduction amount
                        TotalBalanceTransaction = g.Sum(b => b.Collected) // Sum of all transactions
                    })
                    .FirstOrDefaultAsync();

                if (reportData == null)
                {
                    return NotFound(new { success = false, message = "No sales data found for the specified date range." });
                }

                // Prepare the final report
                var salesReport = new SalesReportDto
                {
                    UserName = reportData.UserName,
                    NumberOfGames = reportData.NumberOfGames,
                    TotalCommission = reportData.TotalDeductionAmount,
                    TotalBalanceTransaction = reportData.TotalBalanceTransaction
                };

                return Ok(new { 
                    success = true, 
                    data = new {
                        salesReport,
                        startDate,
                        endDate
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching sales details." });
            }
        }



        public async Task<dynamic> CutCommission(string userId, decimal selectedCartelas, decimal betAmount)
        {
            try
            {
                var query = @$"
SELECT TOP(1) 
    ([multiplier] * {selectedCartelas} * {betAmount}) AS Commission, 
    (({selectedCartelas} * {betAmount}) - ([multiplier] * {selectedCartelas} * {betAmount})) AS WinningAmount 
FROM [bingooo].[dbo].[UserCommissions]  
WHERE userid = @userId
AND maxcount >= @selectedCartelas 
ORDER BY [maxcount]";

                var result = await _context.Database
                    .SqlQueryRaw<CommissionResultDto>(query,
                        new[] { new SqlParameter("@userId", userId), new SqlParameter("@selectedCartelas", selectedCartelas) })
                    .FirstOrDefaultAsync();

                return result; // Ensure a valid return value
            }
            catch (Exception ex)
            {
                // Log the exception (optional)
                Console.WriteLine($"Error in CutCommission: {ex.Message}");

                return new { Error = "An error occurred while processing the commission.", Details = ex.Message };
            }
        }
        // Commissions Dashboard
        [HttpGet("commissions")]
        public async Task<IActionResult> Commissions()
        {
            try
            {
                // Fetch all non-admin users
                var users = await _context.Users
                    .Where(u => u.isAdmin == false) // Exclude admins
                    .ToListAsync();

                // Create a list to hold the commissions data
                var commissionsDto = new List<CommissionsDto>();

                foreach (var user in users)
                {
                    // Use the CalculateAndSaveBalance service to get the calculated balance
                    var calculatedBalance = await _balanceService.CalculateAndSaveBalance(user.Id);

                    // Get the latest top-up amount and date
                    var latestTopUp = await _context.Balance
                        .Where(b => b.UserId == user.Id && b.IsTopUp)
                        .OrderByDescending(b => b.Date)
                        .FirstOrDefaultAsync();

                    // Calculate the status percentage
                    var credit = latestTopUp?.BalanceAmount ?? 0;
                    var status = credit > 0 ? (calculatedBalance.CalculatedBalance / credit) * 100 : 0;

                    // Map the user data and calculated values to the DTO
                    commissionsDto.Add(new CommissionsDto
                    {
                        Id = user.Id,
                        UserName = user.UserName,
                        FullName = user.FullName,
                        PhoneNumber = user.PhoneNumber,
                        Address = user.Address, // Include the user's address
                        Credit = credit, // Latest top-up amount
                        CalculatedBalance = calculatedBalance.CalculatedBalance, // Current balance
                        Status = (double)Math.Round(status, 2) // Balance as a percentage of credit
                    });
                }

                return Ok(new { success = true, data = commissionsDto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching commissions data." });
            }
        }


        //settings sidebar
        [HttpGet("settings")]
        public async Task<IActionResult> Settings()
        {
            var userName = User.Identity.Name; // Get the username of the logged-in user
            var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

            if (loggedInUser == null)
            {
                return Unauthorized(new { success = false, message = "User not found." });
            }
            
            var settings = new SettingsModel()
            {
                SoundSpeed = loggedInUser.SoundSpeed,
                VoiceType = loggedInUser.VoiceType,
                checkRows = loggedInUser.checkRows??false,
                checkColumns = loggedInUser.checkColumns??false,
                checkDiagonals = loggedInUser.checkDiagonals ?? false,
                checkCorners = loggedInUser.checkCorners ?? false,
                checkMiddle = loggedInUser.checkMiddle ?? false,
                Firework = loggedInUser.Firework ?? true,
            };
            
            return Ok(new { success = true, data = settings });
        }
        
        [HttpPost("settings")]
        public async Task<IActionResult> Settings([FromBody] SettingsModel model)
        {
            try
            {
                // Fetch the user by ID
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

                if (loggedInUser == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }
                
                loggedInUser.SoundSpeed = model.SoundSpeed;
                loggedInUser.VoiceType = model.VoiceType;
                loggedInUser.checkRows = model.checkRows;
                loggedInUser.checkColumns = model.checkColumns;
                loggedInUser.checkDiagonals = model.checkDiagonals;
                loggedInUser.checkCorners = model.checkCorners;
                loggedInUser.checkMiddle = model.checkMiddle;
                loggedInUser.Firework = model.Firework;
                
                _context.Users.Update(loggedInUser);
                await _context.SaveChangesAsync();
                
                return Ok(new { success = true, message = "Settings updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while updating settings." });
            }
        }
        
        // Save User Preferences (POST Action)
        [HttpPost("save-settings")]
        public IActionResult SaveSettings(string soundPreference, string colorPreference)
        {
            return Ok(new { 
                success = true, 
                message = "Settings saved successfully!",
                data = new {
                    selectedSound = soundPreference,
                    selectedColor = colorPreference
                }
            });
        }
        [HttpGet("male-or-female")]
        public async Task<IActionResult> MaleOrFemale()
        {
            var userName = User.Identity.Name; // Get the username of the logged-in user
            var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

            if (loggedInUser == null)
            {
                return Unauthorized(new { success = false, message = "User not found." });
            }

            return Ok(new { success = true, voiceType = loggedInUser.VoiceType });
        } 
        
        [HttpGet("audio-files")]
        public async Task<IActionResult> GetAudioFiles(string soundPreference, string colorPreference)
        {
            var userName = User.Identity.Name; // Get the username of the logged-in user
            var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

            if (loggedInUser == null)
            {
                return Unauthorized(new { success = false, message = "User not found." });
            }
            
            var voiceType = loggedInUser.VoiceType;
            var audioFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", $"audio/{voiceType}");
            
            if (!Directory.Exists(audioFolder))
            {
                return NotFound(new { success = false, message = "Audio folder not found." });
            }
            
            var files = Directory.GetFiles(audioFolder)
                                    .Select(f => Path.GetFileName(f))
                                    .ToList();

            return Ok(new { success = true, data = files });
        }
        //        [HttpGet]
        //        public async Task<IActionResult> GetUsers()
        //        {
        //            //// Simulate saving user preferences (replace with database logic later)
        //            //ViewBag.Message = "Settings saved successfully!";
        //            //ViewBag.SelectedSound = soundPreference;
        //            //ViewBag.SelectedColor = colorPreference;

        //                var query = @$"
        //SELECT TOP(1) 
        //    ([UserName],[PhoneNumber],[Address],[ShopName],[isActive]
        //FROM [bingooo].[dbo].[AspNetUsers]  
        //WHERE isAdmin == 0";

        //                var users = await _context.Database
        //                    .SqlQueryRaw<GetUserDto>(query)
        //                    .FirstOrDefaultAsync();

        //            return View(users);
        //        }



        // GET: Fetch all non-admin users
        [HttpGet("users")]
        public async Task<IActionResult> Users()
        {
            try
            {
                // Define a raw SQL query to fetch non-admin users
                var sqlQuery = @"
            SELECT Id, UserName, FullName, PhoneNumber, Address, ShopName, IsActive 
            FROM AspNetUsers 
            WHERE isAdmin = 0"; // Exclude admins

                // Execute the raw SQL query and map the results to a list of Users DTOs
                var users = await _context.ApplicationUser
                    .FromSqlRaw(sqlQuery)
                    .Select(u => new ApplicationUser
                    {
                        Id = u.Id,
                        UserName = u.UserName,
                        FullName = u.FullName,
                        PhoneNumber = u.PhoneNumber,
                        Address = u.Address,
                        ShopName = u.ShopName,
                        isActive = u.isActive
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching users." });
            }
        }
        // PUT: Toggle user status (active/inactive)
        [HttpPut("ToggleUserStatus/{userId}")]
        public async Task<IActionResult> ToggleUserStatus(string userId)
        {
            try
            {
                var user = await _context.ApplicationUser.FindAsync(userId);
                if (user == null)
                {
                    return Ok(new { success = false, message = "User not found." });
                }

                user.isActive = !user.isActive;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, isActive = user.isActive });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = "An error occurred." });
            }
        }


        // GET: Render the form for adding a new user
        [HttpGet("add-user")]
        public IActionResult AddUser()
        {
            return Ok(new { success = true, message = "Add user form endpoint" });
        }

        // POST: Handle the form submission and save the new user
        [HttpPost("add-user")]
        public async Task<IActionResult> AddUser([FromBody] AddUserDto model)
        {
            try
            {
                // Validate input
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Invalid input." });
                }

                // Check if the username already exists
                var existingUser = await _context.ApplicationUser.FirstOrDefaultAsync(u => u.UserName == model.UserName);
                if (existingUser != null)
                {
                    return BadRequest(new { success = false, message = "Username already exists." });
                }

                // Create a new user
                var newUser = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(), // Unique ID
                    UserName = model.UserName,
                    FullName = model.FullName,
                    PhoneNumber = model.PhoneNumber,
                    Address = model.Address,
                    ShopName = model.ShopName,
                    isActive = true, // Default status is active
                    isAdmin = false // Default role is non-admin
                };

                // Save the new user to the database
                _context.ApplicationUser.Add(newUser);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User added successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while adding the user." });
            }
        }


        // GET: Render the form for editing an existing user
        [HttpGet("edit-user")]
        public async Task<IActionResult> EditUser([FromQuery] string userId)
        {
            try
            {
                // Fetch the user by ID
                var user = await _context.ApplicationUser.FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                return Ok(new { success = true, data = user });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching user details." });
            }
        }
        [HttpGet("check/winning-pattern")]
        public async Task<IActionResult> GetWinningPattern()
        {
            try
            {
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                return Ok(new {
                    success = true, 
                    data = new {
                        checkColumns = user.checkColumns,
                        checkCorners = user.checkCorners,
                        checkRows = user.checkRows,
                        checkMiddle = user.checkMiddle,
                        checkDiagonals = user.checkDiagonals
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching winning pattern." });
            }
        }
        [HttpGet("check/firework")]
        public async Task<IActionResult> CheckFirework()
        {
            try
            {
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                return Ok(new { success = true, data = new { firework = user.Firework } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while checking firework setting." });
            }
        }

        [HttpGet("set/voice-speed")]
        public async Task<IActionResult> setVoiceSpeed()
        {
            try
            {
                var userName = User.Identity.Name; // Get the username of the logged-in user
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);
                if (user == null)
                {
                    return Unauthorized(new { success = false, message = "User not found." });
                }

                return Ok(new { success = true, data = new { soundSpeed = user.SoundSpeed } });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching voice speed." });
            }
        }

        // POST: Handle the form submission and update the user's details
        [HttpPost("edit-user")]
        public async Task<IActionResult> EditUser([FromBody] ApplicationUser model)
        {
            try
            {
                // Validate input
                if (!ModelState.IsValid)
                {
                    return BadRequest(new { success = false, message = "Invalid input." });
                }

                // Fetch the existing user by ID
                var existingUser = await _context.ApplicationUser.FirstOrDefaultAsync(u => u.Id == model.Id);

                if (existingUser == null)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Update the user's properties
                existingUser.UserName = model.UserName;
                existingUser.NormalizedUserName = model.UserName?.ToUpperInvariant();
                existingUser.FullName = model.FullName;
                existingUser.PhoneNumber = model.PhoneNumber;
                existingUser.Address = model.Address;
                existingUser.ShopName = model.ShopName;
                existingUser.isActive = model.isActive;
                existingUser.GameRule = model.GameRule;

                // Save the changes to the database
                _context.ApplicationUser.Update(existingUser);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "User updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while updating the user." });
            }
        }

        // POST: Update only the user's password
        [HttpPost("update-user-password")]
        public async Task<IActionResult> UpdateUserPassword([FromBody] UpdateUserPasswordDto model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.UserId) || string.IsNullOrEmpty(model.NewPassword))
                {
                    return BadRequest(new { success = false, message = "UserId and NewPassword are required." });
                }

                var existingUser = await _userManager.FindByIdAsync(model.UserId);
                if (existingUser == null)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Remove old password if exists (for non-external logins)
                var hasPassword = await _userManager.HasPasswordAsync(existingUser);
                IdentityResult result;
                if (hasPassword)
                {
                    result = await _userManager.RemovePasswordAsync(existingUser);
                    if (!result.Succeeded)
                    {
                        return StatusCode(500, new { success = false, message = "Failed to remove old password." });
                    }
                }
                result = await _userManager.AddPasswordAsync(existingUser, model.NewPassword);
                if (!result.Succeeded)
                {
                    return StatusCode(500, new { success = false, message = "Failed to set new password.", errors = result.Errors });
                }

                return Ok(new { success = true, message = "Password updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while updating the password." });
            }
        }
        [HttpGet("bonus")]
        public async Task<IActionResult> Bonus(string userId, DateTime? date = null)
        {
            try
            {
                // Fetch the currently logged-in user's ID if not provided
                if (string.IsNullOrEmpty(userId))
                {
                    var userName = User.Identity.Name; // Get the username of the logged-in user
                    var loggedInUser = await _context.Users.FirstOrDefaultAsync(u => u.UserName == userName);

                    if (loggedInUser == null)
                    {
                        return NotFound(new { success = false, message = "Logged-in user not found." });
                    }

                    userId = loggedInUser.Id; // Use the logged-in user's ID
                }

                // Validate input
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { success = false, message = "User ID is missing." });
                }

                // Check if the user exists in the database
                var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
                if (!userExists)
                {
                    return NotFound(new { success = false, message = "User not found." });
                }

                // Set the date to the current date if it is null or equals DateTime.MinValue (01/01/0001)
                if (date == null || date.Value == DateTime.MinValue)
                {
                    date = DateTime.Today; // Default to today's date
                }

                // Build the query to fetch sales actions, including the User navigation property
                var query = _context.Sales
                    .Where(b => b.UserId == userId && b.Date.Date == date.Value.Date)
                    .Include(b => b.User);

                // Fetch detailed sales actions for the user on the specified date
                var details = await query
                    .Select(b => new Sales
                    {
                        ShopName = b.User.ShopName,
                        TransactionAmount = b.TransactionAmount,
                        TransactionType = b.TransactionType,
                        Date = b.Date
                    })
                    .ToListAsync();

                // Calculate the total bonus collected
                // Find the latest withdrawal date for the user
                var latestWithdrawDate = await _context.Sales
                    .Where(s => s.UserId == userId && s.TransactionType == "Withdraw")
                    .OrderByDescending(s => s.Date)
                    .Select(s => (DateTime?)s.Date)
                    .FirstOrDefaultAsync();

                // Sum deposits after the latest withdrawal (or all if no withdrawal exists)
                var totalBonus = await _context.Sales
                    .Where(s => s.UserId == userId
                        && s.TransactionType == "Deposit"
                        && (latestWithdrawDate == null || s.Date > latestWithdrawDate))
                    .SumAsync(s => (decimal?)s.TransactionAmount) ?? 0;


                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        userId,
                        details,
                        totalBonus,
                        date
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "An error occurred while fetching sales details." });
            }
        }
    }
}
