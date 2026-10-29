# Pact Contract Testing Example

[![Contract Tests](https://github.com/gillersja1/PactContractTestingExample/actions/workflows/contract-tests.yml/badge.svg)](https://github.com/gillersja1/PactContractTestingExample/actions/workflows/contract-tests.yml)

Consumer-driven contract testing in C# with [PactNet](https://github.com/pact-foundation/pact-net),
NUnit and ASP.NET Core.

A front end (`users-web`) calls a Users API (`users-api`). Instead of
end-to-end tests that need both running, each side is tested on its own
against a shared **contract**:

1. The **consumer** tests run the real API client against a Pact mock server
   and record every request it makes and the response it needs. The result
   is a pact file: `pacts/users-web-users-api.json`.
2. The **provider** tests start the real API and replay every recorded
   request against it. If any response doesn't satisfy what the consumer
   declared, verification fails.

So the provider team finds out it would break the front end **before**
deploying, without a shared test environment.

## Why contract tests

| | End-to-end tests | Contract tests |
|---|---|---|
| Needs both services deployed together | Yes | No: each side runs alone |
| Speed | Slow | Seconds |
| When a breaking change is caught | After deploy to a shared environment | In the provider's own CI |
| Failure points to | Somewhere in the stack | The exact field and interaction |

They don't replace functional tests of the API: the contract only covers
what this consumer uses, not the provider's full behavior.

## What's in the contract

| Interaction | Provider state | Expected response |
|---|---|---|
| `GET /api/users/2` | user 2 exists | 200 with `id`, `email`, `first_name`, `last_name` |
| `GET /api/users/999` | user 999 does not exist | 404 |
| `GET /api/users?page=1` | users exist | 200, page 1, at least one user of the shape above |
| `POST /api/users` | | 201 with `id`, `name`, `job`, `created_at` |
| `POST /api/users` with empty name | | 400 with an `error` message |

Responses use **matchers**, not exact values: `Match.Type` (same type),
`Match.Integer`, `Match.Regex` (email, ISO 8601 timestamp) and
`Match.MinType` (an array of at least one item, each with the same shape).
The contract pins down the shape the client depends on, not the provider's
data.

Only the fields the client **reads** are in the contract. The provider also
returns `avatar`, `per_page` and `total`. It can change or remove those
freely, because no consumer depends on them.

### Provider states

An interaction like "user 2 exists" needs the provider to have that data.
Before each interaction, the verifier calls a test-only `/provider-states`
endpoint that resets the in-memory store into the right state. An unknown
state returns an error, so a consumer can't silently depend on setup the
provider never agreed to.

## Tests that prove the check works

A contract test that can't fail is worthless, so the provider suite also
verifies two simulated changes to the API:

| Test | Change | Result |
|---|---|---|
| `RenamingAFieldTheConsumerReads_FailsVerification` | `email` renamed to `email_address` | Verification **fails**: `$.data -> Actual map is missing the following keys: email` |
| `AddingAFieldTheConsumerIgnores_StillPasses` | New `nickname` field | Verification **passes**: not a breaking change |

The changes are applied by a test-only middleware that rewrites responses,
so the production code stays untouched.

## Project structure

```
PactContractTestingExample/
├── src/
│   ├── Users.Consumer/                  # UsersClient: the code under contract
│   └── Users.Provider/                  # Users API (ASP.NET Core minimal API, in-memory store)
├── tests/
│   ├── Users.Consumer.ContractTests/    # Generates the pact against a Pact mock server
│   └── Users.Provider.ContractTests/    # Verifies the real API against the pact
├── pacts/                               # Generated pact file (gitignored)
└── .github/workflows/contract-tests.yml
```

## Running locally

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
# 1. Consumer: writes pacts/users-web-users-api.json
dotnet test tests/Users.Consumer.ContractTests

# 2. Provider: verifies the API against that file
dotnet test tests/Users.Provider.ContractTests --logger "console;verbosity=normal"
```

Run them in that order. Running `dotnet test` on the whole solution runs
both projects in parallel, and the provider can start before the pact file
exists.

To try the API itself: `dotnet run --project src/Users.Provider`, then open
`http://localhost:5000/api/users/2`.

## CI

`.github/workflows/contract-tests.yml` runs on pushes and pull requests to
`main`:

1. **Consumer** job: runs the consumer tests and uploads the pact as an
   artifact.
2. **Provider** job: downloads that pact and verifies the API against it.

Both publish a test report to the run's checks.

## Going further

In a real multi-team setup, the artifact hand-off would be replaced by a
[Pact Broker](https://docs.pact.io/pact_broker) (or PactFlow):

- The consumer publishes each pact, tagged with its version and branch.
- The provider verifies every consumer's pacts and publishes the results.
- `can-i-deploy` checks the broker before a release, so neither side ships a
  version that would break the other.
