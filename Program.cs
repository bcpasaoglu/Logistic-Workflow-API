using System.Reflection;
using LogisticDesk.Data;
using LogisticDesk.Infrastructure;
using LogisticDesk.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    // prevent infinite loops when serializing objects with circular references
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    // send enums as names ("OnRoute") instead of numbers
    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
});

// Consistent RFC 7807 error bodies for unhandled exceptions and empty 4xx/5xx responses.
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "LogisticDesk API",
        Version = "v1",
        Description = "Logistics workflow API: vehicles, shipments, warehouse to vehicle assignment with capacity and availability rules.",
    });

    // Surface the XML doc comments of the controllers in Swagger UI.
    // "Authorize" button in Swagger UI: paste the accessToken from /api/auth/login.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT access token from POST /api/auth/login",
    });
    options.OperationFilter<AuthorizeOperationFilter>();

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:3000", "http://localhost:5173", "http://127.0.0.1:5500" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy.WithOrigins(allowedOrigins)
                        .AllowAnyMethod()
                        .AllowAnyHeader());
});

// Behind a TLS-terminating proxy (Azure Container Apps, App Service, nginx) the
// original scheme and client IP arrive in X-Forwarded-* headers.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<LogiDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<LogiDbContext>("database", tags: new[] { "ready" });

builder.Services.AddIdentity<LogisticDesk.Models.ApplicationUser, Microsoft.AspNetCore.Identity.IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;
    options.User.RequireUniqueEmail = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    // No email sender is wired up yet, so confirmation is opt-in via configuration.
    options.SignIn.RequireConfirmedEmail = builder.Configuration.GetValue<bool>("Identity:RequireConfirmedEmail");
}).AddEntityFrameworkStores<LogiDbContext>().AddDefaultTokenProviders();

// Stateless JWT bearer authentication for the API (replaces Identity's cookie scheme).
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => System.Text.Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinSigningKeyBytes,
        $"Jwt:SigningKey must be at least {JwtOptions.MinSigningKeyBytes} bytes. Set it with user secrets or the Jwt__SigningKey environment variable.")
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer();

// Token validation reads JwtOptions lazily, after configuration is fully built.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<Microsoft.Extensions.Options.IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = jwt.Value.ToValidationParameters();
        bearer.TokenValidationParameters.NameClaimType = System.Security.Claims.ClaimTypes.Name;
        bearer.TokenValidationParameters.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
    });


var app = builder.Build();

// Apply pending EF Core migrations on startup when enabled (docker-compose, Azure).
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<LogiDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();

var swaggerEnabled = app.Environment.IsDevelopment()
    || app.Environment.IsEnvironment("Docker")
    || app.Configuration.GetValue<bool>("Swagger:Enabled");
if (swaggerEnabled)
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.DocumentTitle = "LogisticDesk API");
}

// Containers listen on plain HTTP and TLS is terminated in front of them.
var behindTlsProxy = app.Environment.IsEnvironment("Docker")
    || app.Configuration.GetValue<bool>("Hosting:BehindTlsProxy");
if (!behindTlsProxy)
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Liveness: the process is up. Readiness: the database is reachable.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

app.MapControllers();
app.Run();

// Exposed so integration tests can reference the entry point.
public partial class Program { }
