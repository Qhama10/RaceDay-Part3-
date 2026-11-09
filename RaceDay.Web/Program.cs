using System.Globalization;
using Microsoft.AspNetCore.Localization;
using RaceDay.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Every POST must carry the anti-forgery token that the form tag helpers add automatically.
builder.Services.AddControllersWithViews(o => o.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute()));
builder.Services.AddHttpContextAccessor();

// Session: holds the JWT, role and name for the logged-in user (the MVC app has no database of its own).
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromMinutes(120);
    o.Cookie.Name = ".RaceDay.Session";
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

// Every data operation goes through the Part 2 API. The base URL comes from configuration,
// so Docker can override it with:  -e ApiBaseUrl=http://host.docker.internal:5235
var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5235";
builder.Services.AddTransient<BearerTokenHandler>();
builder.Services.AddHttpClient<IRaceDayApi, RaceDayApi>(c =>
{
    c.BaseAddress = new Uri(apiBase.TrimEnd('/') + "/");
    c.Timeout = TimeSpan.FromSeconds(30);
}).AddHttpMessageHandler<BearerTokenHandler>();

var app = builder.Build();

// Friendly error page in every environment: users never see raw exceptions.
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");

// Same culture everywhere (local and Docker) so dates and decimals parse identically.
var culture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = new[] { culture },
    SupportedUICultures = new[] { culture }
});

app.UseStaticFiles();

// Stops the browser showing protected pages from cache after logout (back button).
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers.CacheControl = "no-store";
        return Task.CompletedTask;
    });
    await next();
});

app.UseRouting();
app.UseSession();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
