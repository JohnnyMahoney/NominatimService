# SKILL.md — PosiTrace Geocoding Home Task

## Purpose

Build a small, production-minded SENIOR-LEVEL ASP.NET Core Web API for forward geocoding Canadian street addresses using Nominatim.

This file is not an implementation guide. It defines the problem, constraints, areas that require deliberate decisions, and the boundaries of the assignment.

Prefer simple, well-reasoned solutions over unnecessary infrastructure.

---

## Source of Truth

Use these sources when making decisions:

1. The original PosiTrace home-task PDF.
2. Official Nominatim Search API documentation:
   https://nominatim.org/release-docs/latest/api/Search/
3. Official Nominatim usage policy:
   https://operations.osmfoundation.org/policies/nominatim/

If this file conflicts with the assignment, the assignment wins.

---

## Core Assignment

The API must:

- expose a single POST endpoint;
- accept a list of Canadian street addresses;
- return geocoding results;
- make every result reliably traceable to its input;
- normalize addresses before calling Nominatim;
- use postal-code fallback when the normalized-address lookup returns no result;
- indicate which lookup strategy produced the result;
- avoid duplicate concurrent outbound lookups for the same address;
- persist successful results across service restarts;
- respect Nominatim's 1 request/second limit;
- send a valid identifying User-Agent;
- handle external failures thoughtfully;
- provide enough logging/configuration for a production-minded service.

Swagger is sufficient. No frontend is required.

---

## Address Normalization

The assignment explicitly requires stripping:

- Apt. / Apt
- Unit
- #
- Suite
- dash-prefixed unit numbers such as:
  `123-12 Main St` → `123 Main St`

Questions to keep in mind:

- What exactly counts as the same address?
- How deterministic is normalization?
- Could normalization accidentally alter a valid address?
- What limitations should be documented?

Normalization behavior should be testable.

---

## Postal Code Fallback

If the normalized address returns no result:

- use the extracted postal code when available;
- clearly identify fallback-based results;
- do not invent a postal code when one is missing;
- define what the API returns when neither strategy finds a result.

Postal-code extraction should be testable.

---

## In-Flight Deduplication

The same address may be requested concurrently by multiple callers.

The intended behavior is:

- only one outbound Nominatim lookup for the same effective address is active at a time;
- other concurrent callers wait for that work instead of triggering their own external request.

Important questions:

- What key defines "the same address"?
- How do concurrent callers share work safely?
- What happens when the shared operation fails or is cancelled?
- How and when is in-flight state cleaned up?

Do not confuse in-flight deduplication with persistent caching.

---

## Persistent Cache

Successful geocoding results must survive service restarts.

SQLite with EF Core or Dapper is explicitly acceptable.

Important questions:

- Should the cache key use raw input or normalized input?
- What information needs to be persisted?
- What should and should not be cached?
- What happens when the cache contains stale or invalid data?
- How does the cache interact with concurrent requests?

The README must briefly explain the cache-key decision.

---

## Nominatim Rate Limit

The public Nominatim service allows at most 1 request per second.

This requirement applies to outbound Nominatim traffic.

Consider:

- normalized-address requests;
- postal-code fallback requests;
- retries, if retries are introduced;
- concurrent API callers.

The implementation should respect the limit without wasting server threads.

---

## Async / Concurrency

Most work in this task is I/O-bound:

- HTTP calls to Nominatim;
- database access;
- waiting caused by rate limiting;
- waiting for shared in-flight work.

The service should use idiomatic asynchronous C# and avoid unnecessary blocking.

Questions to keep in mind:

- Where is async actually useful?
- Which operations share state?
- Where can race conditions appear?
- How does cancellation behave?
- Can one caller accidentally interfere with another caller?

---

## External API Failures

Nominatim is an external dependency and may:

- time out;
- return transient errors;
- return no results;
- become temporarily unavailable.

Decide deliberately:

- which failures are retryable;
- whether retries are needed at all;
- how many retries are reasonable;
- how retries interact with the 1 request/second limit;
- what the API returns when Nominatim is unavailable;
- what should be logged.

Do not silently treat upstream failures as valid geocoding results.

---

## API Contract

The assignment leaves the request/response shape open.

The contract should make it easy to:

- submit multiple addresses;
- identify each input;
- map each output back to its source;
- distinguish found, not-found, and failed outcomes;
- expose which lookup strategy succeeded.

Keep the contract small and clear.

---

## Observability and Configuration

A production-minded solution should consider:

- structured logging;
- Nominatim configuration;
- User-Agent/application identity;
- HTTP timeout;
- persistence configuration;
- useful error visibility.

Avoid scattering environment-specific constants through the code.

---

## Testing Focus

Testing should concentrate on behavior that is easy to get wrong.

Important areas:

- normalization;
- postal-code extraction;
- fallback behavior;
- cache behavior;
- duplicate concurrent requests;
- failure cleanup;
- rate limiting;
- API validation;
- persistence across service recreation.

Automated tests should not depend on the live public Nominatim API.

---

## README

The README should briefly cover:

- how to run the project;
- how to run tests;
- normalization approach;
- known normalization limitations;
- cache-key decision;
- relevant architectural trade-offs;
- any important production limitations.

Keep it concise.

---

## Scope Discipline

Do not introduce infrastructure unless it solves a real requirement in this assignment.

Technologies such as these are not automatically needed:

- RabbitMQ / MOM;
- Kafka;
- Redis;
- microservices;
- Kubernetes;
- distributed locks;
- background-job systems.

If one of them is introduced, there should be a clear reason.

---

## Production vs Home-Task Scope

A single-instance implementation can satisfy the assignment.

When making a design choice, distinguish between:

- what is required for this home task;
- what would change in a horizontally scaled production system.

Prefer documenting hypothetical production extensions instead of implementing them prematurely.

---

## Decision Principles

When choosing an approach, prioritize:

1. correctness against the assignment;
2. simplicity;
3. clear concurrency semantics;
4. respect for Nominatim policy;
5. testability;
6. failure safety;
7. maintainable C#;
8. observability;
9. performance where it matters.

---

## Working Guidance for Codex

When working in this repository:

- read the original assignment before making architectural decisions;
- verify Nominatim behavior against official docs;
- do not assume implementation details that have not been decided yet;
- prefer small changes;
- preserve async behavior for I/O paths;
- add or update tests when behavior changes;
- explain non-obvious trade-offs;
- avoid unnecessary packages or infrastructure;
- keep the solution aligned with the assignment rather than expanding scope.

If a requirement leaves room for multiple valid designs, surface the trade-off instead of silently choosing an elaborate solution.
