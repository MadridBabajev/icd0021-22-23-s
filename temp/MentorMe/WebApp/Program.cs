using App.DAL.Contracts;
using App.DAL.EF;
using App.DAL.EF.Seeding;
using Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// TODO Implement language switching
// TODO Seed the data

var builder = WebApplication.CreateBuilder(args);

// ===================== Set up dependency injection for the container =====================
// Postgres DB connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Register our UOW with scoped lifecycle
builder.Services.AddScoped<IAppUOW, AppUOW>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Configure the Identity
builder.Services
    .AddIdentity<AppUser, AppRole>(
        options => options.SignIn.RequireConfirmedAccount = false)
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddEntityFrameworkStores<ApplicationDbContext>();

// MVC Pages
builder.Services.AddControllersWithViews();

// =========================================
var app = builder.Build();
// =========================================

// ===================== Pipeline Setup =====================

// Set up the database configurations and seed the initial data
SetupAppData(app, app.Configuration);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

// ===================== Run the Web server and wait for requests =====================

app.Run();

static void SetupAppData(IApplicationBuilder app, IConfiguration configuration)
{
    using var serviceScope = app.ApplicationServices
        .GetRequiredService<IServiceScopeFactory>()
        .CreateScope();
    using var context = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();

    if (context == null)
    {
        throw new ApplicationException("Problem in services. Can't initialize DB Context");
    }

    using var userManager = serviceScope.ServiceProvider.GetService<UserManager<AppUser>>();
    using var roleManager = serviceScope.ServiceProvider.GetService<RoleManager<AppRole>>();
    
    if (userManager == null || roleManager == null)
    {
        throw new ApplicationException("Problem in services. Can't initialize UserManager or RoleManager");
    }
    
    var logger = serviceScope.ServiceProvider.GetService<ILogger<IApplicationBuilder>>();
    if (logger == null)
    {
        throw new ApplicationException("Problem in services. Can't initialize logger");
    }

    if (context.Database.ProviderName!.Contains("InMemory"))
    {
        return;
    }

    if (configuration.GetValue<bool>("DataInit:DropDatabase"))
    {
        logger.LogWarning("Dropping database");
        AppDataInit.DropDatabase(context);
    }

    if (configuration.GetValue<bool>("DataInit:MigrateDatabase"))
    {
        logger.LogInformation("Migrating database");
        AppDataInit.MigrateDatabase(context);
    }

    if (configuration.GetValue<bool>("DataInit:SeedIdentity"))
    {
        logger.LogInformation("Seeding identity");
        AppDataInit.SeedIdentity(userManager, roleManager);
    }

    if (configuration.GetValue<bool>("DataInit:SeedData"))
    {
        logger.LogInformation("Seed app data");
        AppDataInit.SeedAppData(context);
    }
}

public partial class Program
{
}