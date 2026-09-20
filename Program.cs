using EduSathi.Data;
using EduSathi.Hubs;
using EduSathi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EduSathi.Services;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Entity Framework Core with SQL Server connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddScoped<QuestionService>();

// 2. Configure ASP.NET Core Identity with your custom ApplicationUser model
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Configure cookie login path to redirect unauthenticated users to /Account/Login
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
});

// 2.1 Configure JWT Bearer for mobile API endpoints (Identity manages cookies automatically)
builder.Services.AddAuthentication()
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
        ValidAudience = builder.Configuration["JwtSettings:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:Secret"]!))
    };
});

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddHttpClient<EduSathi.Services.SummaryService>();
builder.Services.AddHttpClient<EduSathi.Services.McqService>();

// Register Swagger generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var builderApp = builder.Build();

// Configure the HTTP request pipeline.
// 0. Support ngrok and reverse proxies by forwarding headers FIRST
var forwardedHeaderOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                       ForwardedHeaders.XForwardedProto
};
forwardedHeaderOptions.KnownNetworks.Clear();
forwardedHeaderOptions.KnownProxies.Clear();
builderApp.UseForwardedHeaders(forwardedHeaderOptions);

if (!builderApp.Environment.IsDevelopment())
{
    builderApp.UseExceptionHandler("/Home/Error");
    builderApp.UseHsts();
    // Only enforce HTTPS redirection in non-development environments
    builderApp.UseHttpsRedirection();
}

builderApp.UseSwagger();
builderApp.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "EduSathi API V1");
});

builderApp.UseRouting();

// 3. Authentication MUST come before Authorization
builderApp.UseAuthentication();
builderApp.UseAuthorization();

builderApp.MapStaticAssets();

// 4. Route default traffic straight to your Home page
builderApp.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// 5. Map Razor Pages (Required for Identity Login/Register UI pages)
builderApp.MapRazorPages();

// 6. Realtime hub backing the Questionnaires live room lobby.
builderApp.MapHub<RoomHub>("/hubs/room");

builderApp.Run();