# ProductNormaliser.Application

ProductNormaliser.Application is the new application-layer seam for the solution.

Its purpose is to host use cases, orchestration services, command and query flows, and category-agnostic workflow logic that should sit above the domain model but below the runtime hosts.

At this restructuring stage, the project is no longer just a placeholder seam. It now owns the transport-agnostic orchestration for source management, source-candidate discovery, guarded onboarding decisions, discovery seeding, discovery-progress tracking, crawl-job creation rules, and classification-layer contracts. It exists so host-specific controllers and pages can stay thin while rules and workflow validation live in one place.

## Why this project exists

- separate use-case orchestration from the Domain model
- give AdminApi and Worker a stable application boundary to depend on
- provide a clean place for category-selection, source-candidate evaluation, discovery-seeding, crawl-request, and dashboard-facing workflows
- prepare for broader electrical-goods support without pushing host-specific logic into Domain or Infrastructure

## Current contents

The application layer now includes:

- `CategoryManagementService` for category listing, family grouping, schema lookup, enabled-category filtering, and combined metadata plus schema detail payloads
- `SourceManagementService` for source registration, update, enable or disable, category assignment, throttling validation, and startup discovery defaults when a source is registered without an explicit discovery profile
- `SourceCandidateDiscoveryService` for candidate search-result collapsing, probe scoring, duplicate detection, market-aware ranking, and final recommendation decisions that combine heuristics with the optional classification layer
- `CrawlJobService` for creating category, source, and targeted crawl jobs, with category jobs now seeding discovery from eligible managed sources rather than relying only on known targets
- `SourceDiscoveryService` for deterministic discovery coordination over source policies, queue state, and discovered URLs
- `DiscoveryJobProgressService` for turning discovery and promotion state into job-level progress signals
- `ProductTargetEnqueuer` for promoting confirmed product URLs into the crawl queue
- `IPageClassificationService` and related evaluation result models so the optional classification layer stays behind a small application-facing contract
- store abstractions that let Mongo-backed infrastructure stay behind application-facing contracts

These services are what the Admin API, Worker, and Web dashboard now consume for management workflows and the discovery-first crawl pipeline.

The classification-layer contract is intentionally modest. It exists to help the system distinguish likely product pages and promising sources from noise when heuristics alone are not enough, while still allowing the main discovery and onboarding rules to remain explainable and measurable.

## Build

```bash
dotnet build ProductNormaliser.Application/ProductNormaliser.Application.csproj
```