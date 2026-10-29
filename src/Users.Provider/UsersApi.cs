using System.Text.Json;

namespace Users.Provider;

public sealed record CreateUserBody(string? Name, string? Job);

/// <summary>
/// The Users API. Built here rather than in Program.cs so the provider
/// contract tests can host the real app and add their own endpoints.
/// </summary>
public static class UsersApi
{
    public const int PageSize = 6;

    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddSingleton<UserStore>();
        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

        var app = builder.Build();
        MapUsers(app);
        return app;
    }

    private static void MapUsers(WebApplication app)
    {
        app.MapGet("/api/users", (UserStore store, int? page) =>
        {
            var current = Math.Max(page ?? 1, 1);
            var users = store.All();
            return Results.Ok(new
            {
                Page = current,
                PerPage = PageSize,
                Total = users.Count,
                TotalPages = (int)Math.Ceiling(users.Count / (double)PageSize),
                Data = users.Skip((current - 1) * PageSize).Take(PageSize),
            });
        });

        app.MapGet("/api/users/{id:int}", (UserStore store, int id) =>
            store.Find(id) is { } user
                ? Results.Ok(new { Data = user })
                : Results.NotFound(new { }));

        app.MapPost("/api/users", (UserStore store, CreateUserBody body) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
            {
                return Results.BadRequest(new { Error = "name is required" });
            }
            var id = store.NextId();
            return Results.Created($"/api/users/{id}", new
            {
                Id = id,
                body.Name,
                Job = body.Job ?? "",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        });
    }
}
