using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pgvector.EntityFrameworkCore;
using ProductivityHub.Database;
using ProductivityHub.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), o => o.UseVector()));

// Add services to the container.

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NoteService>();

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = ConfigurationOptions.Parse(builder.Configuration["Redis:ConnectionString"]!);
    // BLPOP in NoteEmbeddingQueue blocks server-side for up to 5s; give the client's own
    // response timeout enough headroom above that so it doesn't race a legitimate timeout.
    configuration.SyncTimeout = 10000;
    configuration.AsyncTimeout = 10000;
    return ConnectionMultiplexer.Connect(configuration);
});

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<INoteChunker, NoteChunker>();
builder.Services.AddSingleton<INoteEmbeddingQueue, NoteEmbeddingQueue>();
builder.Services.AddScoped<NoteEmbeddingProcessor>();
builder.Services.AddScoped<StrandedNoteReconciler>();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHostedService<NoteEmbeddingBackgroundService>();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var jwtKey = builder.Configuration["JWT:Key"]
    ?? throw new InvalidOperationException("JWT:Key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["JWT:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["JWT:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
