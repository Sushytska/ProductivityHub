using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductivityHub.Database;
using ProductivityHub.Services;
using static ProductivityHub.DTOs.AuthDTOs;

namespace ProductivityHub.Tests;

public class AuthServiceTests
{
    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TestAppDbContext(options);
    }

    private static IConfiguration CreateConfig()
    {
        var settings = new Dictionary<string, string?>
        {
            ["JWT:Key"] = "unit-test-signing-key-that-is-long-enough-for-hmac-sha256",
            ["JWT:Issuer"] = "ProductivityHubAPI.Tests",
            ["JWT:Audience"] = "ProductivityHubAPI.Tests.Users"
        };

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    [Fact]
    public async Task RegisterAsync_NewEmail_CreatesUserAndReturnsToken()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());

        var response = await sut.RegisterAsync(new RegisterRequest("new@example.com", "Password123!"));

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.Token));

        var storedUser = await db.Users.SingleAsync(u => u.Email == "new@example.com");
        Assert.NotEqual(Guid.Empty, storedUser.Id);
        Assert.NotEqual("Password123!", storedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123!", storedUser.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());
        await sut.RegisterAsync(new RegisterRequest("dup@example.com", "Password123!"));

        var response = await sut.RegisterAsync(new RegisterRequest("dup@example.com", "DifferentPass1!"));

        Assert.Null(response);
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == "dup@example.com"));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());
        await sut.RegisterAsync(new RegisterRequest("login@example.com", "Password123!"));

        var response = await sut.LoginAsync(new LoginRequest("login@example.com", "Password123!"));

        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response!.Token));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());
        await sut.RegisterAsync(new RegisterRequest("wrongpass@example.com", "Password123!"));

        var response = await sut.LoginAsync(new LoginRequest("wrongpass@example.com", "NotThePassword!"));

        Assert.Null(response);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());

        var response = await sut.LoginAsync(new LoginRequest("nobody@example.com", "Password123!"));

        Assert.Null(response);
    }

    [Fact]
    public async Task RegisterAsync_Token_ContainsExpectedClaimsIssuerAndAudience()
    {
        using var db = CreateDbContext();
        var sut = new AuthService(db, CreateConfig());

        var response = await sut.RegisterAsync(new RegisterRequest("claims@example.com", "Password123!"));
        var user = await db.Users.SingleAsync(u => u.Email == "claims@example.com");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(response!.Token);

        Assert.Equal("ProductivityHubAPI.Tests", jwt.Issuer);
        Assert.Contains("ProductivityHubAPI.Tests.Users", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value);
        Assert.Equal("claims@example.com", jwt.Claims.Single(c => c.Type == System.Security.Claims.ClaimTypes.Email).Value);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }
}
