using System.Net;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Matchers;
using Users.Consumer;

namespace Users.Consumer.ContractTests;

/// <summary>
/// Consumer side of the contract. Each test describes one request the client
/// makes and the response it needs, runs the real client against a Pact mock
/// server, and records the interaction. When all pass, PactNet writes
/// pacts/users-web-users-api.json for the provider to verify.
///
/// Responses use matchers (Match.Type, Match.Regex, ...) rather than exact
/// values, so the contract pins down the shape the client depends on, not
/// the provider's data.
/// </summary>
[TestFixture]
public class UsersClientContractTests
{
    private IPactBuilderV4 _pact = null!;

    /// <summary>
    /// PactNet merges new interactions into an existing pact file, so start
    /// clean; otherwise a removed test would leave its interaction behind.
    /// </summary>
    [OneTimeSetUp]
    public void DeleteOldPact()
    {
        var pactFile = Path.Combine(RepoPaths.PactDir, "users-web-users-api.json");
        if (File.Exists(pactFile))
        {
            File.Delete(pactFile);
        }
    }

    [SetUp]
    public void SetUp()
    {
        var config = new PactConfig
        {
            PactDir = RepoPaths.PactDir,
            Outputters = new IOutput[] { new NUnitOutput() },
            LogLevel = PactLogLevel.Information,
        };
        _pact = Pact.V4("users-web", "users-api", config).WithHttpInteractions();
    }

    private static object UserShape(int id) => new
    {
        id = Match.Integer(id),
        email = Match.Regex("janet.weaver@example.com", @"^[^@\s]+@[^@\s]+\.[^@\s]+$"),
        first_name = Match.Type("Janet"),
        last_name = Match.Type("Weaver"),
    };

    [Test]
    public async Task GetUser_WhenUserExists_ReturnsUser()
    {
        _pact.UponReceiving("a request for user 2")
                .Given("user 2 exists")
                .WithRequest(HttpMethod.Get, "/api/users/2")
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithJsonBody(new { data = UserShape(2) });

        await _pact.VerifyAsync(async ctx =>
        {
            var user = await Client(ctx).GetUserAsync(2);

            Assert.That(user, Is.Not.Null);
            Assert.That(user!.Id, Is.EqualTo(2));
            Assert.That(user.FirstName, Is.Not.Empty);
        });
    }

    [Test]
    public async Task GetUser_WhenUserMissing_ReturnsNull()
    {
        _pact.UponReceiving("a request for a user that doesn't exist")
                .Given("user 999 does not exist")
                .WithRequest(HttpMethod.Get, "/api/users/999")
            .WillRespond()
                .WithStatus(HttpStatusCode.NotFound);

        await _pact.VerifyAsync(async ctx =>
        {
            Assert.That(await Client(ctx).GetUserAsync(999), Is.Null);
        });
    }

    [Test]
    public async Task GetUsers_ReturnsRequestedPage()
    {
        _pact.UponReceiving("a request for the first page of users")
                .Given("users exist")
                .WithRequest(HttpMethod.Get, "/api/users")
                .WithQuery("page", "1")
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithJsonBody(new
                {
                    page = 1,
                    total_pages = Match.Integer(2),
                    // At least one user, each with the same shape.
                    data = Match.MinType(UserShape(1), 1),
                });

        await _pact.VerifyAsync(async ctx =>
        {
            var page = await Client(ctx).GetUsersAsync(1);

            Assert.That(page.Page, Is.EqualTo(1));
            Assert.That(page.Data, Is.Not.Empty);
        });
    }

    [Test]
    public async Task CreateUser_ReturnsCreatedUser()
    {
        _pact.UponReceiving("a request to create a user")
                .WithRequest(HttpMethod.Post, "/api/users")
                .WithJsonBody(new { name = "Morpheus", job = "leader" })
            .WillRespond()
                .WithStatus(HttpStatusCode.Created)
                .WithJsonBody(new
                {
                    id = Match.Integer(9),
                    name = "Morpheus",
                    job = "leader",
                    // Pact regexes must match the whole value: ISO 8601 with
                    // optional fractional seconds and a Z or +hh:mm offset.
                    created_at = Match.Regex(
                        "2026-10-29T12:00:00+00:00",
                        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$"),
                });

        await _pact.VerifyAsync(async ctx =>
        {
            var created = await Client(ctx).CreateUserAsync(new CreateUserRequest("Morpheus", "leader"));

            Assert.That(created.Id, Is.Positive);
            Assert.That(created.Name, Is.EqualTo("Morpheus"));
        });
    }

    [Test]
    public async Task CreateUser_WithoutName_ThrowsValidationError()
    {
        _pact.UponReceiving("a request to create a user without a name")
                .WithRequest(HttpMethod.Post, "/api/users")
                .WithJsonBody(new { name = "", job = "leader" })
            .WillRespond()
                .WithStatus(HttpStatusCode.BadRequest)
                .WithJsonBody(new { error = Match.Type("name is required") });

        await _pact.VerifyAsync(async ctx =>
        {
            var ex = Assert.ThrowsAsync<UserValidationException>(
                () => Client(ctx).CreateUserAsync(new CreateUserRequest("", "leader")));
            Assert.That(ex!.Message, Is.Not.Empty);
        });
    }

    private static UsersClient Client(IConsumerContext ctx) =>
        new(new HttpClient { BaseAddress = ctx.MockServerUri });
}
