# GeocoderSolution

## Run locally

```powershell
dotnet restore
dotnet run --launch-profile http
```

The API listens on `http://localhost:5236`. On startup, EF Core automatically applies migrations and creates the local `geocoding-cache.db` SQLite file when needed. No external database is required.

Run tests with:

```powershell
dotnet test .\Tests\GeocoderSolution.Tests.csproj -c Release
```

## Normalization and cache keys

The normalizer removes the required leading unit qualifiers (`Apt`, `Unit`, `#`, and `Suite`) and follows the task's dash example (`123-12 Main St` becomes `123 Main St`), then normalizes whitespace and comma spacing. It is deliberately conservative: it does not correct typos, expand abbreviations, or otherwise rewrite address data.

Successful full-address results are cached by the normalized address rather than the raw input, so inputs that differ only by removable unit information reuse the same result. Keys are canonicalized for casing and whitespace and use explicit namespaces:

```text
address:453 WEST 12TH AVENUE, VANCOUVER, BC V5Z 1M2
postal:V5Z1M2
```

Full-address lookup remains higher priority than postal-code cache lookup because it can return a more precise location. Not-found results are not cached.

## Nominatim failures

Each result has a `status` of `found`, `notFound`, or `failed`. A timeout, network error, non-success HTTP response, or invalid Nominatim JSON marks only the affected address as `failed`; processing continues for the remaining batch. Failure details are logged while the API returns a safe generic error message. Failed results are not cached.

The outbound HTTP timeout is configured by `Nominatim:TimeoutSeconds`. Automatic retries are intentionally omitted because every retry consumes Nominatim rate-limit capacity and increases latency for all queued addresses.

## Production usage

Public Nominatim and a local SQLite database are used for the scope of this assessment. For a production vehicle-tracking workload, the public usage policy would require an alternative provider or a self-hosted Nominatim instance. A horizontally scaled deployment would also require shared caching and distributed coordination for deduplication and rate limiting.
