using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Authentication;
using RfcBuddy.Web.Authorization;
using RfcBuddy.Web.Services;
using RfcBuddy.Web.Support;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IPrincipal>(provider => provider.GetService<IHttpContextAccessor>()!.HttpContext!.User);
builder.Services.AddSingleton<IAppSettingsService, AppSettingsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRfcService, ExcelService>();
builder.Services.AddScoped<IWordService, WordService>();
builder.Services.AddSingleton<IRfcArchiveService, RfcArchiveService>();
builder.Services.AddSingleton<IRfcChangeTracker, RfcChangeTracker>();
builder.Services.AddSingleton<IApiTokenService>(provider => new ApiTokenService(builder.Configuration["DataFolder"] ?? "./data", provider.GetRequiredService<ILogger<ApiTokenService>>()));
builder.Services.AddSingleton<IUserRegistryService>(provider => new UserRegistryService(builder.Configuration["DataFolder"] ?? "./data", provider.GetRequiredService<ILogger<UserRegistryService>>(), provider.GetService<IApiTokenService>()));
builder.Services.AddScoped<UserRegistrationFilter>();
builder.Services.AddTransient<IAuthorizationHandler, AdminAuthorizationHandler>();
builder.Services.AddHostedService<ArchiveUpdateService>();
builder.Services.AddHostedService<UserMaintenanceService>();

// Add services to the container.
builder.Services.AddControllersWithViews(options => options.Filters.AddService<UserRegistrationFilter>());
builder.Services.AddHealthChecks();

// Persist Data Protection keys to the shared data volume so every replica uses
// the same key ring. Without a shared key ring each pod generates its own keys
// and cannot decrypt the auth cookie, antiforgery token, or OIDC correlation/
// nonce cookies issued by another pod. That mismatch is what forces a second
// OIDC login on the first cross-pod POST ("Apply filters and download RFCs").
// SetApplicationName must be identical across replicas so purpose strings match.
string dataProtectionKeysPath = Path.Join(builder.Configuration["DataFolder"] ?? "./data", "keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("RfcBuddy");

// Trust proxy headers (for OpenShift HTTPS)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = null; // Trust all proxy hops in OpenShift
});

//Authentication
const string keycloakSection = "Keycloak";
builder.Services.AddAuthentication(options =>
{
    //Sets cookie authentication scheme
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
    .AddScheme<AuthenticationSchemeOptions, ApiTokenAuthenticationHandler>("ApiToken", _ => { })
    .AddCookie(cookie =>
    {
        cookie.AccessDeniedPath = "/";
        cookie.LogoutPath = "/";
        //Sets the cookie name and maxage, so the cookie is invalidated.
        cookie.Cookie.Name = "keycloak.cookie";
        cookie.Cookie.MaxAge = TimeSpan.FromMinutes(600);
        cookie.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        cookie.SlidingExpiration = true;
        // Use standard chunking cookie manager to split cookies exceeding 4K limits.
        // This avoids exceeding Load Balancer single-header limits.
        cookie.CookieManager = new Microsoft.AspNetCore.Authentication.Cookies.ChunkingCookieManager();
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = $"{builder.Configuration.GetSection(keycloakSection)["auth-server-url"]}/realms/{builder.Configuration.GetSection(keycloakSection)["realm"]}";
        options.ClientId = builder.Configuration.GetSection(keycloakSection)["resource"];
        options.ClientSecret = builder.Configuration.GetSection(keycloakSection).GetSection("credentials")["secret"];
        options.MetadataAddress = $"{builder.Configuration.GetSection(keycloakSection)["auth-server-url"]}/realms/{builder.Configuration.GetSection(keycloakSection)["realm"]}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.GetClaimsFromUserInfoEndpoint = true;
        options.ResponseType = OpenIdConnectResponseType.Code;
        // Use Pushed Authorization Requests (PAR) when Keycloak advertises the endpoint.
        // Forwarded headers (UseForwardedHeaders) promote proxied requests to https, so the
        // redirect_uri is generated correctly for the PAR back-channel push to Keycloak.
        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.UseIfAvailable;
        options.NonceCookie.SameSite = builder.Environment.IsDevelopment()
            ? SameSiteMode.Unspecified
            : SameSiteMode.None;
        options.CorrelationCookie.SameSite = builder.Environment.IsDevelopment()
            ? SameSiteMode.Unspecified
            : SameSiteMode.None;

        // Ensure secure cookies for OIDC in production
        options.NonceCookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        // Do not store keycloak OIDC JWT tokens (access, identity, refresh tokens) inside 
        // the session cookie, as the app is server-side and does not use them for API requests.
        // This keeps cookie size below 2KB and prevents Load Balancer header size overflows.
        options.SaveTokens = false;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role,
            ValidateIssuer = true,
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", policy => policy.Requirements.Add(new AdminRequirement()));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("ApiPolicy", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; frame-ancestors 'none'; form-action 'self'; img-src 'self' data:;");
    await next();
});

// Anonymous liveness/readiness endpoint for OpenShift probes. Must NOT require auth,
// otherwise the probe triggers the OIDC/PAR challenge and the pod never goes Ready.
app.MapHealthChecks("/healthz").AllowAnonymous();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

await app.RunAsync();
