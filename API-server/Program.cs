using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
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
builder.Services.AddScoped<TaskService>();
builder.Services.AddScoped<HabitService>();

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

builder.Services.AddSingleton<ITaskEmbeddingQueue, TaskEmbeddingQueue>();
builder.Services.AddScoped<TaskEmbeddingProcessor>();
builder.Services.AddScoped<StrandedTaskReconciler>();

builder.Services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHostedService<NoteEmbeddingBackgroundService>();
builder.Services.AddHostedService<TaskEmbeddingBackgroundService>();

builder.Services.Configure<AnthropicOptions>(builder.Configuration.GetSection("Anthropic"));
builder.Services.AddScoped<IRagService, RagService>();
builder.Services.AddHttpClient<IChatService, AnthropicChatService>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<AnthropicOptions>>().Value;
    // IsNullOrEmpty (not `?? throw`/null-check): in the Docker environment, Anthropic__ApiKey
    // is always defined as a container env var (defaulting to "" when ANTHROPIC_API_KEY isn't
    // set — see docker-compose.yml), so Configuration[...] returns "" rather than null and a
    // plain null-check would silently let a blank key through to Anthropic instead of failing
    // fast here.
    var apiKey = builder.Configuration["Anthropic:ApiKey"];
    if (string.IsNullOrEmpty(apiKey))
    {
        throw new InvalidOperationException(
            "Anthropic:ApiKey is not configured (set via dotnet user-secrets, or ANTHROPIC_API_KEY in API-server/.env for Docker).");
    }
    client.BaseAddress = new Uri(options.BaseUrl);
    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
    client.DefaultRequestHeaders.Add("anthropic-version", options.ApiVersion);
    // 120s covers both the non-streaming call and the (typically longer-running) streamed
    // call — HttpClient.Timeout bounds the whole operation including reading a streamed
    // body, not just headers.
    client.Timeout = TimeSpan.FromSeconds(120);
});
builder.Services.AddScoped<ChatOrchestrationService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Bounds cost exposure on the paid Anthropic API: 10 chat requests per minute per user.
    options.AddPolicy("chat", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    // Register/Login run before there's an authenticated user to key on, so this
    // partitions by remote IP instead — bounds both password-brute-force attempts and
    // the CPU cost of repeated BCrypt.Verify calls per caller.
    options.AddPolicy("auth", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

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

// Skip in the Docker environment: the api compose service runs HTTP-only
// (ASPNETCORE_URLS=http://+:8080, no HTTPS port), so redirection has nowhere
// to redirect to and would just log a warning on every request.
if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Unauthenticated liveness check — used by the Dockerfile's HEALTHCHECK so
// docker-compose's `depends_on: condition: service_healthy` can gate nginx/
// realtime-service on Kestrel actually accepting connections, not just the
// api container having started.
app.MapGet("/healthz", () => Results.Ok());

app.MapControllers();

app.Run();
