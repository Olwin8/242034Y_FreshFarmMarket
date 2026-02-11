using _242034Y_FreshFarmMarket.Models;
using _242034Y_FreshFarmMarket.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// ✅ FIX: Persist Data Protection keys to a STABLE location
var keysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "FreshFarmMarket",
    "DataProtectionKeys");

Directory.CreateDirectory(keysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("FreshFarmMarket");

// Db
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AuthConnectionString")));

// Identity
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 3;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(2);

        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders()

    // ✅ NEW: custom Email OTP 2FA provider
    .AddTokenProvider<EmailOtpTokenProvider<ApplicationUser>>("EmailOTP");

// ✅ Cookie paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(2);
    options.SlidingExpiration = true;
});

builder.Services.AddRazorPages();

// Session
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// HttpContext + caching
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();

// reCAPTCHA v3
builder.Services.AddHttpClient();
builder.Services.AddScoped<IRecaptchaService, RecaptchaService>();

// Your services
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IPasswordStrengthService, PasswordStrengthService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IPasswordHistoryService, PasswordHistoryService>();
builder.Services.AddScoped<IInputSanitizationService, InputSanitizationService>();

// Email sender
builder.Services.AddScoped<IEmailSenderService, SmtpEmailSenderService>();

// Anti-forgery (CSRF)
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-CSRF";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

app.UseExceptionHandler("/Error");
app.UseHsts();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();

//
// ✅ Single-session enforcement middleware (ADD-ON ONLY)
//
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? "";

        // ✅ Exclude these to avoid breaking the 2FA flow
        if (!path.StartsWith("/Login", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/Register", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/Enable2fa", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/LoginWith2fa", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/StatusCode", StringComparison.OrdinalIgnoreCase))
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!string.IsNullOrEmpty(userId))
            {
                var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                var sessionService = context.RequestServices.GetRequiredService<ISessionService>();
                var signInManager = context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>();

                var appSessionId = context.Session.GetString("AppSessionId");

                if (string.IsNullOrEmpty(appSessionId))
                {
                    await signInManager.SignOutAsync();
                    context.Session.Clear();
                    context.Response.Redirect("/Login?forced=1");
                    return;
                }

                var user = await userManager.FindByIdAsync(userId);
                var currentSessionId = user?.CurrentSessionId;

                if (string.IsNullOrEmpty(currentSessionId) || !string.Equals(currentSessionId, appSessionId, StringComparison.Ordinal))
                {
                    await signInManager.SignOutAsync();
                    context.Session.Clear();
                    context.Response.Redirect("/Login?forced=1");
                    return;
                }

                var ok = await sessionService.ValidateSessionAsync(appSessionId, userId);
                if (!ok)
                {
                    await signInManager.SignOutAsync();
                    context.Session.Clear();
                    context.Response.Redirect("/Login?timeout=1");
                    return;
                }
            }
        }
    }

    await next();
});

//
// ✅ Max password age enforcement (2 minutes)
//
app.Use(async (context, next) =>
{
    if (context.User?.Identity?.IsAuthenticated == true)
    {
        var path = context.Request.Path.Value ?? "";

        var allow =
            path.StartsWith("/ChangePassword", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Logout", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Error", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/StatusCode", StringComparison.OrdinalIgnoreCase);

        if (!allow)
        {
            var userManager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.GetUserAsync(context.User);

            if (user != null && user.LastPasswordChangeDate.HasValue)
            {
                var cfg = context.RequestServices.GetRequiredService<IConfiguration>();
                var maxMinutes = cfg.GetValue<int>("PasswordAgePolicy:MaxAgeMinutes", 2);

                var age = DateTime.Now - user.LastPasswordChangeDate.Value;
                if (age.TotalMinutes >= maxMinutes)
                {
                    context.Response.Redirect("/ChangePassword?expired=1");
                    return;
                }
            }
        }
    }

    await next();
});

app.UseAuthorization();

app.UseStatusCodePagesWithReExecute("/StatusCode", "?code={0}");

app.MapRazorPages();

//
// ✅ NEW: Seed Admin role + default admin user (from appsettings.json)
//
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var cfg = services.GetRequiredService<IConfiguration>();

    // Ensure Admin role exists
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    // Create / assign admin from config (optional)
    var adminEmail = cfg["AdminSeed:Email"];
    var adminPassword = cfg["AdminSeed:Password"];

    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var email = adminEmail.Trim();

        var adminUser = await userManager.FindByEmailAsync(email);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = "System Admin",
                Gender = "Not specified",
                MobileNo = "+6599999999",
                DeliveryAddress = "Admin",
                CreatedDate = DateTime.Now,
                LastPasswordChangeDate = DateTime.Now
            };

            var created = await userManager.CreateAsync(adminUser, adminPassword);
            if (created.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
        }
    }
}

app.Run();
