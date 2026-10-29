using System.Net;
using System.Net.Http.Json;

namespace Users.Consumer;

/// <summary>
/// Client for the Users API, as used by the "users-web" front end.
/// </summary>
public sealed class UsersClient(HttpClient http)
{
    /// <summary>Returns the user, or null if the API says it doesn't exist.</summary>
    public async Task<User?> GetUserAsync(int id, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"/api/users/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SingleUserResponse>(ct);
        return body!.Data;
    }

    public async Task<UsersPage> GetUsersAsync(int page, CancellationToken ct = default)
    {
        using var response = await http.GetAsync($"/api/users?page={page}", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UsersPage>(ct))!;
    }

    /// <exception cref="UserValidationException">The API rejected the input.</exception>
    public async Task<CreatedUser> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("/api/users", request, ct);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(ct);
            throw new UserValidationException(error!.Error);
        }
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedUser>(ct))!;
    }
}

public sealed class UserValidationException(string message) : Exception(message);
