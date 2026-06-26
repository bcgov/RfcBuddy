using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.HttpOverrides;
using RfcBuddy.App.Services;
using System.Security.Claims;
using System.Security.Principal;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IPrincipal>(provider => provider.GetService<IHttpContextAccessor>()!.HttpContext!.User);
builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRfcService, ExcelService>();
builder.Services.AddScoped<IWordService, WordService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

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
        // The OnRedirectToIdentityProvider event below runs before the PAR back-channel
        // push, so the redirect_uri is corrected to https before it is sent to Keycloak.
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

        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = context =>
            {
                // Force HTTPS redirect URI when running behind a TLS-terminating reverse proxy (like OpenShift route)
                if (context.Request.Headers.ContainsKey("X-Forwarded-Proto") &&
                    context.Request.Headers["X-Forwarded-Proto"].ToString().Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    context.ProtocolMessage.RedirectUri = context.ProtocolMessage.RedirectUri.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
                }
                return Task.CompletedTask;
            }
        };
    });

var app = builder.Build();

app.UseForwardedHeaders();

// TEMPORARY: verifies that ForwardedHeaders promotes the request to https behind the
// OpenShift route. Hit https://<route-host>/debug/forwarded and confirm "scheme":"https".
// Remove this endpoint once proxy header handling is confirmed.
app.MapGet("/debug/forwarded", (HttpContext ctx) => Results.Json(new
{
    scheme = ctx.Request.Scheme,
    host = ctx.Request.Host.Value,
    isHttps = ctx.Request.IsHttps,
    xForwardedProto = ctx.Request.Headers["X-Forwarded-Proto"].ToString(),
    xForwardedFor = ctx.Request.Headers["X-Forwarded-For"].ToString(),
    xForwardedHost = ctx.Request.Headers["X-Forwarded-Host"].ToString(),
}));

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
