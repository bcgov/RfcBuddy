using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using RfcBuddy.App.Services;
using RfcBuddy.Web.Services;
using System.Security.Claims;
using System.Security.Principal;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IPrincipal>(provider => provider.GetService<IHttpContextAccessor>()!.HttpContext!.User);
builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRfcService, ExcelService>();
builder.Services.AddScoped<IWordService, WordService>();
builder.Services.AddSingleton<IRfcArchiveService, RfcArchiveService>();
builder.Services.AddHostedService<ArchiveUpdateService>();

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHealthChecks();

// Persist Data Protection keys to the shared data volume so every replica uses
// the same key ring. Without a shared key ring each pod generates its own keys
// and cannot decrypt the auth cookie, antiforgery token, or OIDC correlation/
// nonce cookies issued by another pod. That mismatch is what forces a second
// OIDC login on the first cross-pod POST ("Apply filters and download RFCs").
// SetApplicationName must be identical across replicas so purpose strings match.
string dataProtectionKeysPath = Path.Combine(builder.Configuration["DataFolder"] ?? "./data", "keys");
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
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = $"{builder.Configuration.GetSection(keycloakSection)["auth-server-url"]}/realms/{builder.Configuration.GetSection(keycloakSection)["realm"]}";
        options.ClientId = builder.Configuration.GetSection(keycloakSection)["resource"];
        options.ClientSecret = builder.Configuration.GetSection(keycloakSection).GetSection("credentials")["secret"];
        options.MetadataAddress = $"{builder.Configuration.GetSection(keycloakSection)["auth-server-url"]}/realms/{builder.Configuration.GetSection(keycloakSection)["realm"]}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.ResponseType = OpenIdConnectResponseType.Code;
        // Use Pushed Authorization Requests (PAR) when Keycloak advertises the endpoint.
        // Forwarded headers (UseForwardedHeaders) promote proxied requests to https, so the
        // redirect_uri is generated correctly for the PAR back-channel push to Keycloak.
        options.PushedAuthorizationBehavior = PushedAuthorizationBehavior.UseIfAvailable;
        options.NonceCookie.SameSite = SameSiteMode.Unspecified;
        options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;

        // Ensure secure cookies for OIDC in production
        options.NonceCookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.CorrelationCookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = ClaimTypes.Role,
            ValidateIssuer = true,
        };
    });

var app = builder.Build();

app.UseForwardedHeaders();

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
