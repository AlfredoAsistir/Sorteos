using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using eLotto.Core.Data;
using eLotto.Core.Repository;
using eLotto.Core.Services;
using eLotto.Services;
using eLotto.Options;
using QuestPDF.Infrastructure;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var applicationName = builder.Configuration["Branding:ApplicationName"]?.Trim();
if (string.IsNullOrWhiteSpace(applicationName))
    throw new InvalidOperationException("Branding:ApplicationName is not configured.");

QuestPDF.Settings.License = LicenseType.Community;
QuestPDF.Settings.UseEnvironmentFonts = false;

const string frontendCorsPolicy = "Frontend";
var allowedFrontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

if (allowedFrontendOrigins.Length == 0)
    throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one frontend origin.");

builder.Services.AddCors(options =>
{
    options.AddPolicy(frontendCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedFrontendOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtKey = jwtSettings["Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
    throw new InvalidOperationException("JwtSettings:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = true;
        options.EventsType = typeof(ActiveSessionJwtBearerEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = $"{applicationName} API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT bearer token.",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<eLottoContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("eLottoConnectionString"),
        sql => sql.MigrationsAssembly("eLotto")));

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<ActiveSessionJwtBearerEvents>();
builder.Services.AddScoped<IAccountServices, AccountServices>();
builder.Services.AddSingleton<IReferralCodeGenerator, ReferralCodeGenerator>();
builder.Services.AddScoped<ISorteoTimeService, SorteoTimeService>();
builder.Services.AddScoped<IWalletPaymentService, WalletPaymentService>();
builder.Services.AddScoped<IScratchcardAssignmentService, ScratchcardAssignmentService>();
builder.Services.AddScoped<IScratchcardRevealService, ScratchcardRevealService>();
builder.Services.AddScoped<IUserPrizeHistoryService, UserPrizeHistoryService>();
builder.Services.AddScoped<IMyReferralsService, MyReferralsService>();
builder.Services.AddHostedService<ExpiredDepositCleanupService>();
var transparencyDirectory = SorteoTransparencyFileStore.ResolveDirectory(
    builder.Environment.ContentRootPath,
    builder.Environment.WebRootPath,
    builder.Configuration["Transparency:StoragePath"]);
builder.Services.AddSingleton<ISorteoTransparencyFileStore>(
    new SorteoTransparencyFileStore(transparencyDirectory));
builder.Services.AddScoped<ISorteoTransparencyService, SorteoTransparencyService>();
builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection(StripeOptions.SectionName));
builder.Services.Configure<DepositOptions>(builder.Configuration.GetSection(DepositOptions.SectionName));
builder.Services.Configure<LotteryRulesOptions>(builder.Configuration.GetSection(LotteryRulesOptions.SectionName));
builder.Services.Configure<AuthenticationOptions>(builder.Configuration.GetSection(AuthenticationOptions.SectionName));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ISorteosRepository, SorteosRepository>();
builder.Services.AddScoped<IReferralProgramSettingsRepository, ReferralProgramSettingsRepository>();
builder.Services.AddScoped<IUserLotteryRepository, UserLotteryRepository>();
builder.Services.AddScoped<IConfirmedTicketPurchaseRepository, ConfirmedTicketPurchaseRepository>();
builder.Services.AddScoped<IUserLotteryService, UserLotteryService>();
builder.Services.AddScoped<IConfirmedTicketDeliveryService, ConfirmedTicketDeliveryService>();
builder.Services.AddSingleton<IWhatsAppResendThrottle, WhatsAppResendThrottle>();
builder.Services.AddSingleton<IConfirmedTicketsPdfService, ConfirmedTicketsPdfService>();
builder.Services.AddScoped<ISorteoImageStorageService, SorteoImageStorageService>();
builder.Services.AddHttpClient<IWhatsAppService, UltraMsgWhatsAppService>()
    .ConfigureHttpClient((provider, client) =>
    {
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<WhatsAppOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.HttpTimeoutSeconds));
    });

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

#region Aplica Migracion

var shouldMigrate = false;

if (app.Environment.IsDevelopment())
{
    shouldMigrate = true;
}
else
{
    var updateFile = Path.Combine(
        app.Environment.ContentRootPath,
        "update.json");

    shouldMigrate = File.Exists(updateFile);
}

if (shouldMigrate)
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider
        .GetRequiredService<eLottoContext>();

    var logger = scope.ServiceProvider
        .GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Checking database migrations...");

        await db.Database.MigrateAsync();

        logger.LogInformation("Database migrations completed.");

        if (!app.Environment.IsDevelopment())
        {
            var updateFile = Path.Combine(
                app.Environment.ContentRootPath,
                "update.json");

            if (File.Exists(updateFile))
            {
                File.Delete(updateFile);

                logger.LogInformation(
                    "update.json deleted after successful migration.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            "Error applying database migrations.");

        throw;
    }
}

#endregion




app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", $"{applicationName} API v1");
});

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors(frontendCorsPolicy);
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();


