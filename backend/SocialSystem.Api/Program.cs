using Microsoft.OpenApi;
using SocialSystem.Api.OpenApi;
using Microsoft.EntityFrameworkCore;
using SocialSystem.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SocialSystem.Api.Models.Entities;
using SocialSystem.Api.Services;
using SocialSystem.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "SocialSystem API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste the accessToken only. The Bearer prefix is added automatically."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    options.OperationFilter<SwaggerAuthorizationFilter>();
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOptions<JwtSettings>().BindConfiguration("Jwt")
    .Validate(x => x.IsValid(), "Configure Jwt:SigningKey with a Base64 encoded random key of at least 32 bytes; check issuer, audience and expiration.")
    .ValidateOnStart();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, settings) =>
    {
        options.MapInboundClaims = false;
        options.IncludeErrorDetails = false;
        options.TokenValidationParameters = settings.Value.ValidationParameters();
    });
builder.Services.AddAuthorization();
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
    options.IterationCount = 210000;
});
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<FriendService>();
builder.Services.AddScoped<MessageService>();
builder.Services.AddScoped<PostService>();
builder.Services.AddSingleton<UtcConnectionInterceptor>();
builder.Services.AddDbContext<SocialDbContext>((services, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("SocialSystem");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Configure ConnectionStrings:SocialSystem in local User Secrets.");
    options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 46)));
    options.AddInterceptors(services.GetRequiredService<UtcConnectionInterceptor>());
});
var app = builder.Build();
// Never expose exception details or connection strings through HTTP.
app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "SocialSystem API v1"));
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// Deliberately no EnsureCreated, EnsureDeleted, Migrate or seed writes.
app.Run();

public partial class Program { }
