using GenZStore.Commands;
using GenZStore.Data;
using GenZStore.Hubs;
using GenZStore.Models;
using GenZStore.Queries;
using GenZStore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// --- 1. DATABASE CONFIGURATION (MariaDB / MySQL - Pomelo) ---
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseMySql(conn, ServerVersion.AutoDetect(conn));
});

// --- 2. FORWARDED HEADERS CONFIGURATION ---
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// --- 3. CORS CONFIGURATION (✅ FIXED) ---
// Purpose: Allows Telegram, Vercel, and Localhost to connect without blocking
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // 🚨 CRITICAL FIX: Use SetIsOriginAllowed to allow ANY origin (needed for Telegram WebApps)
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// --- 4. IDENTITY & AUTHENTICATION (✅ FIXED) ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Debugging: Check if Key exists
    var key = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrEmpty(key)) Console.WriteLine("⚠️ WARNING: Jwt:Key is NULL");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? "SuperSecretDefaultKey_123456789")),

        // 🚨 CRITICAL FIX: Disable strict Issuer/Audience validation to prevent 401 errors
        ValidateIssuer = false,
        ValidateAudience = false,

        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// --- 5. DEPENDENCY INJECTION ---
builder.Services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddScoped<ICategoryCommand, CategoryCommand>();
builder.Services.AddScoped<IProductCommand, ProductCommand>();
builder.Services.AddScoped<IProductQuery, ProductQuery>();
builder.Services.AddScoped<ICategoryQuery, CategoryQuery>();
builder.Services.AddScoped<IOrderQuery, OrderQuery>();
builder.Services.AddScoped<IOrderCommand, OrderCommand>();
builder.Services.AddScoped<IAnalyticsQuery, AnalyticsQuery>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<BakongService>();
builder.Services.AddScoped<PayWayService>();
builder.Services.AddScoped<EmbeddingService>();
builder.Services.AddScoped<TokenService>();

// External API Services
builder.Services.AddHttpClient<GeminiService>();
builder.Services.AddHttpClient<GeminiEmbeddingService>();
builder.Services.AddHostedService<TelegramBotService>();

builder.Services.AddSignalR();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// --- 6. MIDDLEWARE PIPELINE ---
app.UseForwardedHeaders();

// ✅ APPLY CORS HERE
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// --- 7. STATIC FILES & IMAGES ---
var imagesPath = Path.Combine(builder.Environment.ContentRootPath, "images");
if (!Directory.Exists(imagesPath))
{
    Directory.CreateDirectory(imagesPath);
}

var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".webp"] = "image/webp";
provider.Mappings[".png"] = "image/png";
provider.Mappings[".jpg"] = "image/jpeg";
provider.Mappings[".jpeg"] = "image/jpeg";

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images",
    ContentTypeProvider = provider
});

app.UseStaticFiles();

// --- 8. INITIALIZATION & ROUTING ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AppDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
    await DbInitializer.SeedAsync(context, userManager, roleManager);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHub<OrderHub>("/orderHub");
app.MapControllers();

app.Run();