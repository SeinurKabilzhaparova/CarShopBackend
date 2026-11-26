using System.Security.Claims;
using System.Text;
using CarShopFinal.Persistance;
using CarShopFinal.Application.Dependency;
using CarShopFinal.Persistance.Context;
using CarShopFinal.Persistance.Dependency;
using CarShopFinal.Persistance.Redis;
using CarShopFinal.WebApi.Hubs;
using CarShopFinal.WebApi.Middlewares;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using IRedis = CarShopFinal.Persistance.Redis.IRedis;

var builder = WebApplication.CreateBuilder(args);

var jwtKey = builder.Configuration["jwt:key"];
if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Configure jwt__key with a signing key of at least 32 bytes.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["jwt:issuer"],
            ValidAudience = builder.Configuration["jwt:audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey)
            )
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });



//Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379"));
    
builder.Services.AddScoped<IRedis>(sp =>
{
    var mux = sp.GetRequiredService<IConnectionMultiplexer>();
    return new RedisDb(mux);
});

var moderatorIds = builder.Configuration.GetSection("Moderation:UserIds")
    .GetChildren()
    .Select(entry => Guid.TryParse(entry.Value, out var id) ? id : Guid.Empty)
    .Where(id => id != Guid.Empty)
    .ToHashSet();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ModerateListings", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context =>
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && moderatorIds.Contains(userId)));
});
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi();
builder.Services.AddApplication();
builder.Services.AddPersistance(builder.Configuration);
builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddPolicy("dev", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
                return uri.Host == "localhost" || uri.Host == "127.0.0.1";
            })
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("dev");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<ChatHub>("/hub/chat");

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<CarShopFinal.Persistance.Seeding.DataSeeder>();
    await seeder.SeedAsync();
}

app.Run();
