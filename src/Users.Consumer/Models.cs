using System.Text.Json.Serialization;

namespace Users.Consumer;

/// <summary>
/// The fields this client reads. The provider may send more (e.g. avatar);
/// only what is declared here ends up in the contract.
/// </summary>
public sealed record User(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName);

public sealed record UsersPage(
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("total_pages")] int TotalPages,
    [property: JsonPropertyName("data")] IReadOnlyList<User> Data);

public sealed record CreateUserRequest(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("job")] string Job);

public sealed record CreatedUser(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("job")] string Job,
    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt);

internal sealed record SingleUserResponse([property: JsonPropertyName("data")] User Data);

internal sealed record ErrorResponse([property: JsonPropertyName("error")] string Error);
