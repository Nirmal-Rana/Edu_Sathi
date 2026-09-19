using EduSathi.Data;
using EduSathi.Hubs;
using EduSathi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Entity Framework Core with SQL Server connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 2. Configure ASP.NET Core Identity with your custom ApplicationUser model
builder.Services.AddDefaultIdentity<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

builder.Services.AddHttpClient<EduSathi.Services.SummaryService>();
builder.Services.AddHttpClient<EduSathi.Services.McqService>();

// Register Swagger generator
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

// ---> ADDED SWAGGER MIDDLEWARE HERE <---
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "EduSathi API V1");
});

app.UseHttpsRedirection();
app.UseRouting();

// 3. Authentication MUST come before Authorization
app.UseAuthentication();
app.UseAuthorization();
app.MapStaticAssets();

// 4. Route default traffic straight to your Home landing page first
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

// 5. Map Razor Pages (Required for Identity Login/Register UI pages)
app.MapRazorPages();

// 6. Realtime hub backing the Questionnaires live room lobby.
app.MapHub<RoomHub>("/hubs/room");

app.Run();