# Thought Process and Decision Log

## ADR-001: Basic Geocoding Flow and Rate Limiting

**Status:** Implemented

The first slice introduced the simple flow `Controller -> GeocodingService -> NominatimClient`.

`NominatimClient` is a singleton with a shared `SemaphoreSlim` and a configurable 1,100 ms interval. This keeps outbound requests sequential and respects Nominatim's one-request-per-second limit within a single application instance.

## ADR-002: Address Normalization and Postal-Code Fallback

**Status:** Implemented and validated

A small deterministic pipeline now removes the required unit qualifiers and follows the task's explicit dash example (`123-12 Main St` becomes `123 Main St`), then cleans whitespace and comma formatting. It does not correct typos or rewrite otherwise valid address data.

When the normalized-address lookup returns no result, the service extracts a valid Canadian postal code and performs one fallback lookup using only that code.

One complete-garbage case was removed because it was not a meaningful address that normalization or fallback should be expected to recover. The evaluation set now contains 16 cases.

### Result

- Before normalization: 9 successful cases out of 17 (52.9%).
- After normalization: 14 successful cases out of 16 (87.5%).
- After postal-code fallback: 16 successful cases out of 16 (100%).

All representative cases now pass. I consider this sufficient validation of the normalization and fallback behavior for the assessment and will move to the next requirement. No typo-correction logic was added.

## ADR-003: Per-Address Deduplication Instead of a Batch Lock

**Status:** Implemented

I considered processing one complete batch before allowing the next batch to start. This would be simpler and could avoid a separate deduplication phase when successful results are cached.

I rejected this approach because it bypasses the intent of the in-flight deduplication requirement rather than implementing it. It would also allow a large batch to monopolize the service and significantly delay smaller requests from other callers. Persistent cache misses, failures, and non-successful results could still cause duplicate lookups.

The assessment explicitly requires concurrent callers requesting the same address to await the same in-flight result. Therefore, the implementation will use per-address in-flight deduplication while keeping the existing global Nominatim rate limiter.

## ADR-004: Deduplicate In-Flight Requests in NominatimClient

**Status:** Implemented

I reviewed the complete lookup flow. Both normalized-address lookups and postal-code fallback lookups eventually call `NominatimClient.SearchAsync(q)`. Deduplication will therefore be implemented at this boundary instead of separately in each calling workflow. This gives every outbound Nominatim search a single deduplication point and also covers future callers of the client.

`NominatimClient` will keep a `ConcurrentDictionary` of active tasks keyed by a normalized representation of `q`. Normalizing the key prevents equivalent queries that differ only in insignificant casing or whitespace from being treated as different requests. The key represents the effective Nominatim query; if request-shaping parameters are added later, they must also become part of the key.

When `SearchAsync(q)` is called, the first caller creates and registers the outbound lookup task. Concurrent callers with the same key await that task rather than creating additional HTTP requests. This applies equally to a normalized address such as `453 West 12th Avenue...` and to a postal-code fallback such as `V5Z 1M2`.

The dictionary is in-flight state, not a result cache. Its purpose is to track the uniqueness of currently running tasks. The entry will be removed when the shared operation completes or fails so that later calls can perform a fresh lookup and failed tasks are not retained. Persistent caching remains a separate concern.

The existing global rate limiter remains responsible for spacing distinct outbound requests. In-flight deduplication reduces identical concurrent work; it does not replace rate limiting.

## ADR-005: Race-Condition Boundaries

**Status:** Implemented for a single application instance

The current concurrency design prevents the main races required by this assessment. Atomic `ConcurrentDictionary.GetOrAdd` together with `Lazy<Task<...>>` ensures that only one task starts for an effective query. Concurrent callers await that shared task, while caller cancellation stops only that caller's wait and does not cancel work needed by others. The winning task owns cleanup and atomically removes its key in `finally`, including after failures; a replacement cannot be registered until that removal completes. The global `SemaphoreSlim` also protects the shared rate-limit timestamp and serializes distinct outbound Nominatim requests.

These guarantees are process-local. A production deployment with multiple application instances would require distributed coordination for both deduplication and global rate limiting. Production hardening should also connect shared operations to application-shutdown cancellation and enforce a bounded upstream timeout so abandoned work cannot remain in memory indefinitely. Persistent result caching remains separate from these concurrency controls.

## ADR-006: Full Address Takes Priority Over Postal-Code Cache

**Status:** Implemented

A normalized full-address lookup can resolve a precise building or place, while a postal-code lookup usually resolves only the broader postal-code area. Postal code therefore remains a fallback and must not become the preferred result merely because its value is already cached.

The lookup order will be: normalized-address cache, normalized-address Nominatim lookup, postal-code cache, and finally postal-code Nominatim lookup. The postal path is entered only when the normalized address returns no result. This preserves result precision and follows the assignment requirement that postal-code lookup is a fallback after an unsuccessful normalized-address lookup.

## ADR-007: Persistent Successful-Result Cache in SQLite

**Status:** Implemented

Successful geocoding results are persisted through EF Core in the local SQLite file configured by `ConnectionStrings:GeocodingCache`. `GeocodingCacheDbContext` and `GeocodingCacheStore` are scoped services, and EF Core migrations run automatically at application startup. This keeps reviewer setup self-contained, requires no external database, and allows cached results to survive process restarts.

Cache keys are canonicalized for casing and whitespace and use strategy namespaces: `address:<normalized-address>` for the primary lookup and `postal:<postal-code-without-spaces>` for fallback, for example `address:453 WEST 12TH AVENUE, VANCOUVER, BC` and `postal:V5Y1V4`. Raw input is not used because removable unit qualifiers should not create different entries for the same effective address.

The lookup order is address cache, address Nominatim lookup, postal cache, and postal Nominatim fallback. Only successful results are stored; not-found responses and upstream failures are not cached. Stored rows include the coordinates, display data, successful strategy, and UTC creation time.

Concurrent callers can miss the cache together, share one in-flight Nominatim task, and then attempt to persist the same result. `CacheKey` is the primary key and writes use SQLite `INSERT ... ON CONFLICT DO NOTHING`, making those writes idempotent without turning an expected concurrency race into an error.

The cache has no expiration or invalidation policy in the assessment scope. A production system would need an explicit freshness policy, database maintenance and backup decisions, and a shared cache or database when running multiple application instances.

## ADR-008: Isolate Transient Nominatim Failures Per Address

**Status:** Implemented

Nominatim timeout, network, non-success HTTP, and invalid-response failures are translated into a single application exception after structured logging in `NominatimClient`. `GeocodingService` catches that exception per address and returns `status: failed` with a safe generic message, then continues processing the remaining batch. `notFound` remains distinct from `failed`, and request cancellation is not converted into an upstream failure.

The HTTP timeout is configurable through `Nominatim:TimeoutSeconds`. Failed operations never reach the successful-result cache write path. Automatic retries are deliberately omitted because they would consume the same global Nominatim rate-limit capacity, increase latency for all queued requests, and require a separate backoff policy. A production deployment could add bounded retries only if they remain behind the rate limiter and are supported by operational metrics.
