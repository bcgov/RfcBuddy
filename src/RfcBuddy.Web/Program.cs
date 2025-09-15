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
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
        cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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
        options.NonceCookie.SameSite = SameSiteMode.Unspecified;
        options.CorrelationCookie.SameSite = SameSiteMode.Unspecified;
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
app.UseForwardedHeaders();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
