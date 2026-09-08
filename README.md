# Nominatim Forward Geocoding API

An ASP.NET Core Web API that accepts batches of Canadian street addresses and resolves them through the Nominatim Search API. The implementation covers address normalization, postal-code fallback, in-flight request deduplication, persistent SQLite caching, rate limiting, failure isolation, structured logging, and Swagger UI.

## Run locally

### Prerequisites

- .NET 10 SDK
- No external database or other infrastructure is required

From the repository root, run:

```powershell
dotnet restore
dotnet run --launch-profile http
```

The `http` launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and starts the API at:

- API: `http://localhost:5236`
- Swagger UI: `http://localhost:5236/swagger`
- OpenAPI document: `http://localhost:5236/openapi/v1.json`

On startup, EF Core automatically applies migrations and creates `geocoding-cache.db` in the project directory when it does not exist. The file is excluded from Git and survives application restarts.

Run the automated tests with:

```powershell
dotnet test .\Tests\GeocoderSolution.Tests.csproj -c Release
```

The current suite contains 24 tests and does not call the public Nominatim service.

## Quick API check

Send a `POST` request to `http://localhost:5236/geocode` with `Content-Type: application/json`:

```json
{
  "addresses": [
    {
      "id": "city-hall",
      "address": "Apt. 42, 453 West 12th Avenue, Vancouver, BC V5Y 1V4"
    },
    {
      "id": "postal-fallback",
      "address": "999999 Totally Fake Road, Vancouver, BC V5Y 1V4"
    }
  ]
}
```

Each output carries the original `id` and `address`, so it can be reliably mapped back to its input. A result also contains:

- `status`: `found`, `notFound`, or `failed`;
- `found`: convenient boolean representation of the outcome;
- `strategy`: `normalizedAddress` or `postalCode` when found;
- coordinates and Nominatim display data when available;
- a safe error message when the upstream provider fails.

The complete manual evaluation set is available in [`address-test-case.json`](address-test-case.json) and can be used directly as the Postman request body.

## Verification screenshots

### Postman

><img width="2196" height="1307" alt="image" src="https://github.com/user-attachments/assets/af57de84-a872-40e1-a9db-512effa5e3df" />


### Swagger UI

> Add the Swagger UI screenshot here, for example as `docs/images/swagger-ui.png`.

<!-- After adding the image, replace the blockquote above with:
![Geocoding endpoint in Swagger UI](docs/images/swagger-ui.png)
-->

## Source of truth and development approach

The assignment and the supplied documentation are the source of truth for this implementation. Decisions were made in this order:

1. [`Senior Backend Engineer, .NET & Python Home task - PosiTrace`](<docs/Senior Backend Engineer, .NET & Python Home task- Positrace.md>)
2. [`Nominatim Search API documentation`](docs/nominatim.org-release-docs-latest-api-Search.md)
3. [`Nominatim usage policy`](docs/Nominatim-usage-policy.md)

If supporting guidance conflicts with the assignment, the assignment wins.

Codex was used as the implementation agent after receiving the Nominatim Search use case, the assignment requirements, and the relevant project context. [`SKILL.md`](SKILL.md) records the project-specific constraints and working guidance supplied to the agent. Work was performed in small, reviewable slices under explicit human direction rather than as one broad generation step.

Important decisions and trade-offs encountered during those slices are documented in the ADR-like [`thought-process.md`](thought-process.md). It includes the normalization and fallback evaluation, deduplication placement, concurrency boundaries, cache design, and failure-handling decisions. This keeps the README focused on the final system while preserving the reasoning that led to it.

## Project structure

The repository uses a small responsibility-based folder structure. This makes important components easy to locate and prevents HTTP, business-flow, persistence, and configuration concerns from being mixed in one class.

```text
Controllers/       HTTP endpoint and request validation
Configuration/     Typed Nominatim configuration
DTOs/              External API and Nominatim data contracts
Services/          Normalization, postal extraction, geocoding flow, HTTP client
Data/              EF Core context, cache model, key policy, migrations
Tests/             Focused unit and integration-style component tests
docs/              Assignment and supplied Nominatim documentation
Program.cs         Dependency registration and application startup
SKILL.md           Project guidance given to Codex
thought-process.md ADR-like decision history
```

The runtime path remains intentionally small:

```text
POST /geocode
    -> GeocodingController
    -> GeocodingService
       -> AddressNormalizer
       -> GeocodingCacheStore
       -> NominatimClient
       -> CanadianPostalCodeExtractor when fallback is required
```

## End-to-end lookup flow

For each input address, the service performs the following steps:

1. Normalize the full address.
2. Look up `address:<normalized-address>` in SQLite.
3. On a cache miss, call `NominatimClient.SearchAsync(normalizedAddress)`.
4. Deduplicate an equivalent request if it is already in flight.
5. Pass a genuinely new outbound lookup through the global rate limiter.
6. Persist and return a successful full-address result.
7. If the full-address lookup returns no result, extract a Canadian postal code.
8. Look up `postal:<canonical-postal-code>` in SQLite.
9. On a postal cache miss, perform the same deduplicated and rate-limited Nominatim lookup.
10. Persist and return a successful postal result, or return `notFound` if neither strategy succeeds.

An upstream timeout, network failure, non-success status, or invalid JSON produces `failed`, not `notFound`. Only successful results enter the persistent cache.

## Requirement coverage

| Requirement | Implementation | Why it satisfies the requirement |
| --- | --- | --- |
| Address normalization | Deterministic `AddressNormalizer` pipeline | Removes all explicitly required leading unit formats before lookup |
| Postal fallback | `GeocodingService` invokes a postal-only lookup after an empty address result | Preserves precise full-address results while recovering inputs that contain a valid postal code |
| In-flight deduplication | `ConcurrentDictionary<string, Lazy<Task<NominatimPlace?>>>` in `NominatimClient` | Concurrent equivalent queries await one shared outbound task |
| Persistent cache | SQLite + async EF Core operations and startup migrations | Successful results remain available after process restart without external infrastructure |
| Rate limiting | Singleton `NominatimClient`, `SemaphoreSlim`, monotonic timestamp, configurable delay | Distinct outbound calls are serialized and start more than one second apart in one process |
| Nominatim policy | Restricted Canadian search, `limit=1`, configured User-Agent | Identifies the application and keeps each search narrowly scoped |
| API traceability | Caller-provided `id` and original address are copied to every result | Batch outputs can be mapped reliably to source inputs |
| Failure handling | Per-address failure isolation and structured logs | Provider failures are visible and do not silently become valid not-found outcomes |
| Reviewer UI | OpenAPI + NSwag Swagger UI in Development | The only required endpoint can be exercised without a frontend |

## Async I/O

The controller, geocoding workflow, HTTP calls, SQLite operations, rate-limit delay, and shared-task waits use `async`/`await`. These operations are I/O-bound: while the service is waiting for Nominatim, SQLite, the limiter, or shared work, it does not synchronously block a request thread. The continuation can resume on a thread-pool thread when the awaited operation completes.

This improves thread utilization; it does not make the Nominatim rate limit disappear. The batch is deliberately processed in a clear sequential loop, while concurrency between separate callers is coordinated in `NominatimClient`.

## Address normalization

The normalizer implements the formats explicitly required by the assignment:

- `Apt.` and `Apt`;
- `Unit`;
- `#`;
- `Suite`;
- the required dash form, for example `123-12 Main St` becomes `123 Main St`;
- repeated whitespace and inconsistent comma spacing.

Examples:

```text
Apt. 42, 453 West 12th Avenue -> 453 West 12th Avenue
#42, 453 West 12th Avenue     -> 453 West 12th Avenue
123-12 Main St                -> 123 Main St
```

The pipeline is deterministic and deliberately conservative. It removes unit qualifiers only when they appear at the beginning and match the supported patterns. It does not correct spelling mistakes, standardize street suffixes, expand abbreviations, infer missing city/province data, or perform a general postal-address parse. The dash rule follows the assignment's explicit semantics but could be ambiguous for a legitimate hyphenated civic number. A production-grade system would require a richer Canadian address parser and a clearer domain policy for those cases.

### Evaluation set

[`address-test-case.json`](address-test-case.json) contains representative canonical, abbreviated, lowercase, spacing, qualifier, punctuation, typo, postal-only, and invalid-street inputs.

The iterative manual evaluation recorded in [`thought-process.md`](thought-process.md) produced:

- before normalization: 9 successful results out of 17 (`52.9%`);
- after normalization: 14 successful results out of 16 (`87.5%`);
- after postal-code fallback: 16 successful results out of 16 (`100%`).

One complete-garbage input was removed between the first and second measurements because neither normalization nor postal fallback could reasonably recover it. These numbers describe the small project evaluation set, not a general geocoder accuracy benchmark.

## Postal-code fallback and result precision

The normalized full address always has priority because it can identify a precise building or place. A postal-code lookup generally identifies a broader delivery area. Therefore, a postal code is used only after the full normalized address returns no Nominatim result.

The extractor accepts Canadian postal codes with or without a space, validates the allowed letter positions, and emits the canonical format `A1A 1A1`. If no valid postal code exists, the service returns `notFound` after the unsuccessful full-address lookup.

This order also applies to cache access. An existing postal cache row does not bypass a fresh full-address lookup, because doing so could replace a more precise result with a less precise one.

## Rate limiting

`NominatimClient` is registered as a singleton, so all requests in one application process share the same limiter. Before an outbound call:

1. `SemaphoreSlim(1, 1)` admits one distinct request at a time.
2. A monotonic `Stopwatch` timestamp records when the previous request started.
3. If the configured minimum interval has not elapsed, `Task.Delay` asynchronously waits for the remainder.
4. The next HTTP request starts only after that delay.

The default interval is `1100 ms`, providing a small margin over the public Nominatim limit of one request per second. Both normalized-address and postal-fallback requests go through this same boundary. Cache hits do not consume rate-limit capacity, and identical concurrent requests are deduplicated before they reach it.

The default is stored in `appsettings.json` and can be overridden through standard .NET configuration, for example:

```powershell
$env:Nominatim__MinimumRequestIntervalMilliseconds = "1100"
dotnet run --launch-profile http
```

The guarantee is process-local. Multiple replicas would each own a limiter and could collectively exceed the provider limit; production scale-out therefore needs distributed coordination, a centralized outbound proxy, or a provider whose quota model supports that deployment.

## In-flight deduplication

Deduplication lives in `NominatimClient.SearchAsync`, the single boundary through which every Nominatim search passes. This automatically covers primary normalized-address searches, postal-code fallback, and future callers without duplicating concurrency logic in separate workflows.

The flow is:

1. Canonicalize the actual Nominatim `q` value by collapsing whitespace and converting it to uppercase.
2. Create a `Lazy<Task<NominatimPlace?>>` for the potential outbound operation.
3. Atomically register it with `ConcurrentDictionary.GetOrAdd`.
4. The winning `Lazy` starts exactly one shared task; callers that lose the race await the existing task.
5. Remove the dictionary entry in `finally` after success, no result, or failure, allowing a later request to try again.

`LazyThreadSafetyMode.ExecutionAndPublication` is important because a `ConcurrentDictionary` value factory may be evaluated by more than one caller. Only the `Lazy` stored in the dictionary is evaluated, so losing candidates do not start duplicate HTTP work.

Caller cancellation cancels only that caller's wait through `WaitAsync`; it does not cancel the shared operation still needed by other callers. Deduplication is intentionally temporary and in-memory. It prevents duplicate simultaneous work but does not store results—that is the persistent cache's responsibility.

Like the rate limiter, this dictionary is process-local. Multiple service instances require distributed deduplication if the same guarantee must apply across the entire deployment.

## Persistent cache and cache keys

The cache uses SQLite through EF Core. Database code and migrations are isolated under `Data/`, the connection string is kept in configuration, and `Database.MigrateAsync()` applies migrations during startup. No manual database command is needed for a reviewer.

Each successful row stores:

- `CacheKey` as the primary key;
- `Latitude` and `Longitude`;
- `Name` and `DisplayName`;
- the successful `Strategy`;
- UTC `CreatedAt`.

Only successful results are cached. `notFound` is not cached because negative caching requires a deliberate expiration policy, and transient upstream failures are never cached.

### Cache-key decision

The raw caller input is not a suitable key: `Apt. 42, 453 West 12th Avenue` and `453 West 12th Avenue` produce the same effective geocoding query and should reuse one result. The address key therefore uses the normalized address, then canonicalizes casing and repeated whitespace.

Explicit namespaces keep lookup strategies separate:

```text
address:453 WEST 12TH AVENUE, VANCOUVER, BC V5Y 1V4
postal:V5Y1V4
```

The namespace prevents a postal-only result from colliding with an address query and preserves the semantic difference between precise primary lookup and broader fallback. Postal keys remove spaces because `V5Y 1V4` and `V5Y1V4` are equivalent.

Concurrent requests may both miss SQLite before sharing the same in-flight Nominatim task. They may then both try to save its result. `CacheKey` is unique and the write uses SQLite `INSERT ... ON CONFLICT DO NOTHING`, making that expected race idempotent instead of an error.

Because the default connection string is `Data Source=geocoding-cache.db`, data is written to a real local file rather than memory. Recreating the process and EF `DbContext` reconnects to that file, so successful entries remain available after restart. This behavior is also covered by an automated test that writes with one context and reads with a newly created context.

The assessment cache has no expiration or invalidation policy. Production would need an explicit freshness window, cleanup strategy, backup policy, and shared storage when running multiple instances.

## Failures, logging, and configuration

An empty successful Nominatim response is a valid `notFound`. It is different from an upstream failure. `NominatimClient` detects and logs:

- HTTP timeout;
- network/HTTP failure, including non-success status where available;
- invalid JSON returned by Nominatim.

These cases are translated to `NominatimUnavailableException`. `GeocodingService` catches that exception per address, returns `status: failed` with a safe generic message, and continues processing the remaining batch. Failed work is removed from in-flight state and is not written to SQLite, so a later call can try again.

Structured `ILogger` messages provide operational visibility for:

- cache hit and cache miss;
- successful cache write;
- a real outbound Nominatim request;
- reuse of an existing in-flight task;
- timeout, HTTP/network failure, and malformed upstream response.

The logs make manual behavior easy to verify: the first successful request should show a cache miss, an outbound call, and a save; a repeated request should show a cache hit without another outbound log. After restarting the app, the same request should still produce a cache hit.

Relevant configuration is centralized in `appsettings.json` and can be overridden by environment variables:

| Setting | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:GeocodingCache` | `Data Source=geocoding-cache.db` | Persistent SQLite location |
| `Nominatim:BaseUrl` | Public Nominatim URL | External provider endpoint |
| `Nominatim:UserAgent` | Project-specific identifying value | Nominatim usage-policy identification |
| `Nominatim:MinimumRequestIntervalMilliseconds` | `1100` | Minimum delay between outbound request starts |
| `Nominatim:TimeoutSeconds` | `15` | Bounded upstream HTTP wait |

Startup validation rejects a missing/invalid base URL, empty User-Agent, an interval that is not greater than one second, or a non-positive timeout.

Automatic retries are deliberately not implemented. Every retry would consume the same rate-limit capacity and delay all queued requests. Production retries should be bounded, use backoff/jitter, target only known transient failures, and remain behind the global limiter. Useful production additions would include metrics for cache hit rate, provider latency, failure rate, rate-limit queue time, and deduplication reuse, plus tracing, alerts, health checks, and a policy for protecting address data in logs.

## Swagger

Swagger UI is enabled only in the `Development` environment and uses the generated OpenAPI document. It provides a simple way to inspect the request/response schemas and execute `POST /geocode`; no frontend is required by the assignment.

The local launch profile is intentionally HTTP-only for a simple reviewer setup. A production deployment should terminate HTTPS at the application host or a trusted reverse proxy.

## Testing

The automated suite focuses on behavior that is easy to get wrong:

- required normalization formats and conservative no-op behavior;
- postal-code extraction with and without a space;
- postal fallback, missing postal code, and primary-address priority;
- separation of `found`, `notFound`, and `failed` outcomes;
- continuation of a batch after an upstream failure;
- successful caching and the decision not to cache `notFound`;
- persistence after recreating the EF Core context;
- canonical postal cache keys;
- concurrent equivalent-query deduplication;
- in-flight cleanup after completion and failure;
- diagnostic logs for outbound and reused requests.

HTTP tests use deterministic message-handler stubs and cache tests use isolated SQLite databases. This keeps the suite fast, repeatable, and independent of public Nominatim availability and policy.

## Production boundaries

This is intentionally a single-instance home-task solution. Within one process it satisfies the assignment's concurrency, persistence, and rate-limit requirements without introducing Redis, queues, distributed locks, or external database setup.

For a horizontally scaled production service, the main extensions would be:

- distributed rate limiting and in-flight coordination across replicas;
- a shared persistent cache with expiration and maintenance policies;
- bounded retry/circuit-breaker policies aligned with provider quotas;
- metrics, tracing, alerting, and health checks;
- capacity limits for request batch size and queued outbound work;
- explicit shutdown cancellation for shared operations;
- HTTPS and authentication/authorization appropriate to the deployment;
- review of the public Nominatim usage policy and likely use of a commercial provider or self-hosted Nominatim for sustained vehicle-tracking traffic.

Those concerns are documented instead of adding infrastructure that is not required to demonstrate the requested behavior.
