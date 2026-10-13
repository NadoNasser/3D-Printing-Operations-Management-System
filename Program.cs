using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using PrintShop3D.Data;
using PrintShop3D.Models;

var builder = WebApplication.CreateBuilder(args);

const long MaxMultipartRequestSize = 320L * 1024 * 1024;

// Kestrel must allow the full multipart request before ASP.NET Core's form
// limit can validate the 300 MB multi-file upload.
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = MaxMultipartRequestSize;
});

// EF Core - existing SQL Server database (Database-First). No migrations are run
// against the core schema; it is created/maintained via create_database.sql.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddRazorPages();
builder.Services.Configure<FormOptions>(options =>
{
    // The Files page accepts multiple 3D design files. The page itself enforces
    // 100 MB per file and 300 MB total, so the multipart request limit must be
    // at least 300 MB as well.
    options.MultipartBodyLengthLimit = MaxMultipartRequestSize;
});
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// Simple cookie authentication with two roles: Admin, Staff.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(10);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Events.OnValidatePrincipal = async context =>
        {
            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var userIdValue = context.Principal?.FindFirst("UserId")?.Value;
            if (!int.TryParse(userIdValue, out var userId) || !await db.Users.AnyAsync(u => u.UserId == userId && u.IsActive))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Default policy: any authenticated user (Admin or Staff) unless a page opts out ([AllowAnonymous]).
    options.FallbackPolicy = options.DefaultPolicy;
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

await SeedDefaultAdminAsync(app);

app.Run();

// Creates the first Admin account on an empty USERS table only. Requires
// db/002_add_users_table.sql to have been run first. Safe to call every
// startup - it is a no-op once any user exists.
static async Task SeedDefaultAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        if (!await db.Users.AnyAsync())
        {
            var username = Environment.GetEnvironmentVariable("PRINTSHOP_DEFAULT_ADMIN_USERNAME") ?? "admin";
            var password = Environment.GetEnvironmentVariable("PRINTSHOP_DEFAULT_ADMIN_PASSWORD");
            var generated = false;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
                generated = true;
            }
            var admin = new AppUser { Username = username, Role = "Admin", IsActive = true, CreatedAt = DateTime.Now };
            admin.PasswordHash = hasher.HashPassword(admin, password);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
            if (generated)
                logger.LogWarning("Created first admin user '{Username}' with a generated password. Set PRINTSHOP_DEFAULT_ADMIN_PASSWORD before deployment if you need a known bootstrap password.", username);
            else
                logger.LogWarning("Created first admin user '{Username}' from PRINTSHOP_DEFAULT_ADMIN_PASSWORD. Change it after first login.", username);
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not seed default admin user. Has db/002_add_users_table.sql been run yet?");
    }
}
