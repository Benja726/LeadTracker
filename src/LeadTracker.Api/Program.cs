using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using LeadTracker.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

// ---- Supabase options (from appsettings / user-secrets / env) ----
var supabase = builder.Configuration.GetSection("Supabase").Get<SupabaseOptions>()
               ?? throw new InvalidOperationException("Missing 'Supabase' configuration section.");

// ---- AuthN: validate the JWT that Supabase Auth issues to the client ----
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = supabase.Issuer,
            ValidateAudience = true,
            ValidAudience = SupabaseOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // Supabase puts the user id in "sub" and the user role in "role".
            NameClaimType = "sub",
            RoleClaimType = "role",
        };

        if (!string.IsNullOrWhiteSpace(supabase.JwtSecret))
        {
            // Legacy HS256: symmetric shared secret. Only if your project still uses it.
            options.TokenValidationParameters.IssuerSigningKey =
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(supabase.JwtSecret));
        }
        else
        {
            // Recommended: asymmetric (ES256) keys pulled from the JWKS endpoint.
            // ConfigurationManager caches keys and refreshes them, so rotation just works.
            options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                supabase.JwksUri,
                new SupabaseJwksRetriever(),
                new HttpDocumentRetriever { RequireHttps = true })
            {
                AutomaticRefreshInterval = TimeSpan.FromHours(12),
                RefreshInterval = TimeSpan.FromMinutes(5),
            };
        }
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS for the dashboard frontend (adjust origins as needed)
const string FrontendCors = "frontend";
builder.Services.AddCors(o => o.AddPolicy(FrontendCors, p => p
    .WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(FrontendCors);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
