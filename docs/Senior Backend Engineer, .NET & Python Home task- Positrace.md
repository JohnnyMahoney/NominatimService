Dear candidate,
Thank you for your interest in joining our team!
This technical assessment represents the first stage of our hiring process. Its purpose is to help us evaluate your technical expertise, problem-solving skills, and approach to software development in a practical, real-world scenario.
We encourage you to complete the assignment independently and apply the same level of quality, organization, and best practices you would use in a professional environment. While the final solution is important, we are equally interested in understanding your thought process, code structure, and development approach.
Please read the instructions carefully before you begin. If anything is unclear, don't hesitate to reach out—we'll be happy to answer any questions.
We appreciate the time and effort you dedicate to this assessment and look forward to reviewing your work.
Instructions to the Testing Task - Senior Backend Engineer- Forward GeocodingWeb API
Build an ASP.NET Core Web API that geocodes street addresses using the Nominatim API. The service should expose a single POST endpoint that accepts a list of addresses and returns geocoding results. You have full control over the request/response structure — just make sure each result can be reliably mapped back to its source input. All addresses are Canadian.
Requirements
1. Address Normalization
Street addresses may arrive in non-standard formats. Before querying Nominatim, strip the following qualifiers from the address to give the geocoder the best chance of success: Apt. / Apt , Unit , # , Suite, and dash-prefixed unit numbers (e.g. 123-12 Main St → 123 Main St ).
2. Fallback Strategy
If a normalized address query returns no results, fall back to geocoding using the extracted postal code (if available). The response should indicate which strategy produced the result.
3.In-Flight Deduplication
The same address may be requested by multiple callers concurrently. Only one outbound Nominatim call should be made per unique address at any given time — other concurrent requests for the same address should await that result rather than each triggering their own call.
4. Persistent Cache
Since the same addresses tend to recur (vehicles travel the same routes), successful results should be cached in a persistent store so that Nominatim is not re-queried after a service restart. SQLite with EF Core or Dapper is a perfectly sufficient choice. Think carefully about what yo cache key should be — raw input or normalized address — and briefly explain your decision in the README. www.positrace.com
5. Rate Limiting
Nominatim enforces a 1 request/second limit. The service must respect this.
Deliverables
Working source code (GitHub link or zip)
A brief README.md covering how to run the project locally, your normalization approach and its known limitations, and your cache key decision
Notes
No frontend needed — Swagger UI is sufficient
Nominatim usage policy applies — include a valid User-Agent header in requests
Consider how the service handles transient Nominatim failures, and what operational visibility (logging, configuration) a production service would need
www.positrace.com