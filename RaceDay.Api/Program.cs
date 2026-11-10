using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Raceday_API__prog_part_2_.Data;
using Raceday_API__prog_part_2_.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<RaceDayDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()!;
builder.Services.AddSingleton(jwt);
builder.Services.AddSingleton<ITokenService, TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwt.Issuer,
        ValidAudience = jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key))
    });
builder.Services.AddAuthorization();

// Azure Blob Storage (image uploads). Set BlobStorage:ConnectionString with user-secrets or an environment variable.
var blob = builder.Configuration.GetSection("BlobStorage").Get<BlobSettings>() ?? new BlobSettings();
builder.Services.AddSingleton(blob);
builder.Services.AddSingleton<IBlobService, BlobService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RaceDay API", Version = "v1" });
    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token from /api/auth/login"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme { Reference = new OpenApiReference
            { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
    });
});
builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>();
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();   // must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }