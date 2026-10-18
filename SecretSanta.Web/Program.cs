using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Http.Resilience;
using SecretSanta.Web;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// Razor + Blazor + Auth + Google Oauth
builder.Services
    .AddRazorPages()
    .Services
    .AddServerSideBlazor()
    .Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
        })
        .AddGoogle(options =>
        {
            options.ClientId = builder.Configuration.GetExpectedValue<string>("Google:ClientId");
            options.ClientSecret = builder.Configuration.GetExpectedValue<string>("Google:ClientSecret");
            options.ClaimActions.MapJsonKey("urn:google:profile", "link");
            options.ClaimActions.MapJsonKey("urn:google:image", "picture");
            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "sub"); // Google unique ID
            options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");         // email
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");           // full name

        })
    .Services
    .AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Localization (SK default, CZ, EN)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RazorPagesOptions>(opt =>
{
    opt.RootDirectory = "/Components";
});

// API client
builder.Services.AddHttpClient("apiservice", client =>
{
    var baseUrl = builder.Configuration["ApiService:BaseUrl"]
        ?? (builder.Environment.IsDevelopment() ? "https://localhost:7471" : "https+http://apiservice");
    client.BaseAddress = new Uri(baseUrl);
    var internalApiKey = builder.Configuration["ApiSecurity:InternalApiKey"];
    if (!string.IsNullOrEmpty(internalApiKey))
    {
        client.DefaultRequestHeaders.Add("X-Internal-Api-Key", internalApiKey);
    }
});

// The API may be asleep on free hosting (Render spins it down after inactivity, wake-up takes ~1 min),
// so allow longer timeouts than the 30 s standard resilience default.
// ConfigureAll: ServiceDefaults registers the handler via ConfigureHttpClientDefaults, whose options name isn't the client name.
builder.Services.ConfigureAll<HttpStandardResilienceOptions>(options =>
{
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
});

var app = builder.Build();

// Wake the API up in parallel with the web start, so the first page load doesn't wait for it
app.Lifetime.ApplicationStarted.Register(() =>
{
    var client = app.Services.GetRequiredService<IHttpClientFactory>().CreateClient("apiservice");
    _ = client.GetAsync("/").ContinueWith(
        t => app.Logger.LogInformation("API warm-up ping finished: {Status}", t.IsCompletedSuccessfully ? t.Result.StatusCode.ToString() : t.Exception?.GetBaseException().Message),
        TaskScheduler.Default);
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(SharedResource.DefaultCulture)
    .AddSupportedCultures(SharedResource.SupportedCultures)
    .AddSupportedUICultures(SharedResource.SupportedCultures));

app.UseRouting();

app.UseCookiePolicy();
app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Stores the selected language in a cookie and reloads the page it was called from
app.MapGet("/Culture/Set", (string culture, string? redirectUri, HttpContext context) =>
{
    if (SharedResource.SupportedCultures.Contains(culture))
    {
        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true, SameSite = SameSiteMode.Lax });
    }

    var isLocal = !string.IsNullOrEmpty(redirectUri)
        && redirectUri.StartsWith('/')
        && !redirectUri.StartsWith("//")
        && !redirectUri.StartsWith("/\\");
    return Results.LocalRedirect(isLocal ? redirectUri! : "/");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.MapDefaultEndpoints();

app.Run();
