using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using PactNet;
using PactNet.Exceptions;
using PactNet.Infrastructure.Outputters;
using PactNet.Verifier;
using Users.Provider;

namespace Users.Provider.ContractTests;

/// <summary>
/// Provider side of the contract. Starts the real Users API on a free port,
/// replays every interaction in the consumer's pact file against it, and
/// fails if any response doesn't satisfy what the consumer declared.
/// </summary>
[TestFixture]
public class UsersApiContractTests
{
    private static readonly string PactFile = Path.Combine(RepoPaths.PactDir, "users-web-users-api.json");

    [OneTimeSetUp]
    public void RequirePactFile()
    {
        if (!File.Exists(PactFile))
        {
            Assert.Fail($"No pact at {PactFile}. Run the consumer contract tests first.");
        }
    }

    [Test]
    public async Task UsersApi_HonoursUsersWebContract()
    {
        await using var app = await StartProviderAsync();

        Verify(app);
    }

    /// <summary>
    /// Proves the check has teeth: a provider that renames a field the
    /// consumer reads must fail verification.
    /// </summary>
    [Test]
    public async Task RenamingAFieldTheConsumerReads_FailsVerification()
    {
        await using var app = await StartProviderAsync(renameField: ("email", "email_address"));

        var ex = Assert.Throws<PactVerificationFailedException>(() => Verify(app));
        TestContext.Progress.WriteLine($"Verification failed as expected: {ex!.Message}");
    }

    /// <summary>
    /// Adding a field the consumer doesn't read is not a breaking change.
    /// The real provider already does this: it returns avatar and per_page,
    /// which aren't in the pact.
    /// </summary>
    [Test]
    public async Task AddingAFieldTheConsumerIgnores_StillPasses()
    {
        await using var app = await StartProviderAsync(addField: ("nickname", "jw"));

        Verify(app);
    }

    private static void Verify(WebApplication app)
    {
        var baseUri = new Uri(app.Urls.First());
        var config = new PactVerifierConfig
        {
            Outputters = new IOutput[] { new NUnitOutput() },
            LogLevel = PactLogLevel.Information,
        };

        using var verifier = new PactVerifier("users-api", config);
        verifier
            .WithHttpEndpoint(baseUri)
            .WithFileSource(new FileInfo(PactFile))
            .WithProviderStateUrl(new Uri(baseUri, "/provider-states"))
            .Verify();
    }

    private static async Task<WebApplication> StartProviderAsync(
        (string From, string To)? renameField = null,
        (string Name, string Value)? addField = null)
    {
        var app = UsersApi.Build(Array.Empty<string>());
        app.Urls.Add("http://127.0.0.1:0");

        if (renameField is not null || addField is not null)
        {
            app.Use(async (context, next) => await RewriteUserObjectsAsync(context, next, renameField, addField));
        }

        // Test-only endpoint the verifier calls before each interaction to
        // put the provider into the state the consumer described.
        app.MapPost("/provider-states", (ProviderState state, UserStore store) =>
        {
            if (state.Action == "teardown")
            {
                return Results.Ok();
            }
            switch (state.State)
            {
                // Interactions without a state still trigger a call; start
                // them from the default data.
                case null or "":
                    store.Reset(UserStore.SeedUsers());
                    break;
                case "user 2 exists":
                    store.Reset(UserStore.SeedUsers().Where(u => u.Id == 2));
                    break;
                case "user 999 does not exist":
                    store.Reset(UserStore.SeedUsers());
                    break;
                case "users exist":
                    store.Reset(UserStore.SeedUsers());
                    break;
                default:
                    // Fail loudly: an unknown state means the consumer expects
                    // setup that the provider never agreed to.
                    return Results.Problem($"Unknown provider state '{state.State}'", statusCode: 500);
            }
            return Results.Ok();
        });

        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Simulates a changed provider by editing every user object in the
    /// response, i.e. any JSON object that has an "email" property.
    /// </summary>
    private static async Task RewriteUserObjectsAsync(
        HttpContext context, Func<Task> next, (string From, string To)? rename, (string Name, string Value)? add)
    {
        var original = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        await next();
        context.Response.Body = original;

        buffer.Position = 0;
        if (context.Response.ContentType?.StartsWith("application/json") != true || buffer.Length == 0)
        {
            await buffer.CopyToAsync(original);
            return;
        }

        var json = JsonNode.Parse(buffer.ToArray())!;
        foreach (var user in UserObjects(json))
        {
            if (rename is { } r && user.Remove(r.From, out var value))
            {
                user[r.To] = value;
            }
            if (add is { } a)
            {
                user[a.Name] = a.Value;
            }
        }
        var bytes = JsonSerializer.SerializeToUtf8Bytes(json);
        context.Response.ContentLength = bytes.Length;
        await original.WriteAsync(bytes);
    }

    private static IEnumerable<JsonObject> UserObjects(JsonNode node) => node switch
    {
        JsonObject obj when obj.ContainsKey("email") => new[] { obj },
        JsonObject obj => obj.SelectMany(p => p.Value is null ? [] : UserObjects(p.Value)),
        JsonArray arr => arr.SelectMany(n => n is null ? [] : UserObjects(n)),
        _ => [],
    };

    private sealed record ProviderState(string? State, string? Action);
}
