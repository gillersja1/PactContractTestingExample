using System.Collections.Concurrent;

namespace Users.Provider;

public sealed record UserRecord(int Id, string Email, string FirstName, string LastName, string Avatar);

/// <summary>
/// In-memory user storage. Stands in for a database so the example has no
/// external dependencies.
/// </summary>
public sealed class UserStore
{
    private readonly ConcurrentDictionary<int, UserRecord> _users = new();
    private int _nextId;

    public UserStore() => Reset(SeedUsers());

    public static IEnumerable<UserRecord> SeedUsers()
    {
        var names = new (string First, string Last)[]
        {
            ("George", "Bluth"), ("Janet", "Weaver"), ("Emma", "Wong"), ("Eve", "Holt"),
            ("Charles", "Morris"), ("Tracey", "Ramos"), ("Michael", "Lawson"), ("Lindsay", "Ferguson"),
        };
        return names.Select((n, i) => new UserRecord(
            i + 1,
            $"{n.First}.{n.Last}@example.com".ToLowerInvariant(),
            n.First,
            n.Last,
            $"https://example.com/avatars/{i + 1}.png"));
    }

    public void Reset(IEnumerable<UserRecord> users)
    {
        _users.Clear();
        foreach (var user in users)
        {
            _users[user.Id] = user;
        }
        _nextId = _users.IsEmpty ? 0 : _users.Keys.Max();
    }

    public UserRecord? Find(int id) => _users.GetValueOrDefault(id);

    public IReadOnlyList<UserRecord> All() => _users.Values.OrderBy(u => u.Id).ToList();

    public int NextId() => Interlocked.Increment(ref _nextId);
}
