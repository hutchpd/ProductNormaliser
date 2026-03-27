# ProductNormaliser.Application.Tests

ProductNormaliser.Application.Tests is the current broad NUnit validation suite for the platform while the solution is being split into more focused layered test projects. It currently covers domain rules, infrastructure behavior, worker orchestration, admin observability, and the long-term intelligence features added across later phases.

This project is important because ProductNormaliser is not just a CRUD system. Its value depends on nuanced decision logic such as identity resolution, merge weighting, semantic change detection, source trust evolution, and adaptive crawl scheduling. Those behaviors need explicit regression coverage.

## Boundary

Belongs here now:

- cross-cutting tests that exercise Application flows together with Infrastructure, Worker, or AdminApi dependencies
- integration fixtures that rely on the shared Mongo2Go harness or broader backend wiring
- transitional end-to-end coverage that would be risky to drop while narrower test projects are still being split out

Should migrate outward later:

- tests whose primary subject belongs to a single owning project and no longer needs this broad harness
- Admin API read-model and projection tests that can live in ProductNormaliser.AdminApi.Tests
- repository, extraction, and persistence tests that can live in ProductNormaliser.Infrastructure.Tests
- worker orchestration and runtime tests that can live in ProductNormaliser.Worker.Tests
- pure normalisation, schema, and domain-rule tests that can live in ProductNormaliser.Domain.Tests or a narrower application-focused suite

When adding a new fixture, prefer the narrower owning test project first. Add it here only when the test genuinely spans multiple backend layers or the destination project does not exist yet. The current breakup tracker lives in [../docs/application-tests-breakup-tracker.md](../docs/application-tests-breakup-tracker.md).

## Responsibilities

- validate schema-driven normalisation behavior
- validate extraction and source-product construction
- validate Mongo-backed repositories and integration flows
- validate merge, conflict, and identity decisions
- validate worker orchestration behavior
- validate deterministic discovery and crawl seeding behavior
- validate the optional page-classification layer, its kill switch and timeout behavior, and the golden-dataset evaluation harness
- validate admin API read models and observability responses
- validate temporal intelligence, adaptive backoff, and disagreement tracking

## Responsibility tags in this project

Every fixture in this project now carries a single `Responsibility:*` NUnit category so the suite can be filtered and split intentionally instead of growing anonymously.

Current responsibility tags:

- `Responsibility:Normalisation`
- `Responsibility:CategorySchema`
- `Responsibility:Extraction`
- `Responsibility:Discovery`
- `Responsibility:SourceManagement`
- `Responsibility:Persistence`
- `Responsibility:CrawlOrchestration`
- `Responsibility:IdentityAndMerge`
- `Responsibility:Observability`
- `Responsibility:Intelligence`
- `Responsibility:AIClassification`

Run one responsibility slice with:

```bash
dotnet test ProductNormaliser.Application.Tests/ProductNormaliser.Application.Tests.csproj --filter "TestCategory=Responsibility:Discovery"
```

## Tooling

This project uses:

- NUnit
- Microsoft.NET.Test.Sdk
- NUnit3TestAdapter
- coverlet.collector
- Mongo2Go for isolated MongoDB-backed integration tests

## How to run

Run the whole solution test suite:

```bash
dotnet test ProductNormaliser.slnx
```

Or run only this project:

```bash
dotnet test ProductNormaliser.Application.Tests/ProductNormaliser.Application.Tests.csproj
```

## Why Mongo2Go is used

Several important behaviors in ProductNormaliser only make sense against a real persistence layer. Mongo2Go allows those flows to be tested without requiring a shared developer database, which keeps the integration suite reproducible and self-contained.

## When to add tests here

Add or update tests when you change:

- category schemas or normalisation rules
- merge heuristics or conflict thresholds
- queue scheduling behavior
- source trust, stability, or disagreement logic
- persistence shapes or repository queries
- admin DTO projections or endpoint-facing behavior

## Build

```bash
dotnet build ProductNormaliser.Application.Tests/ProductNormaliser.Application.Tests.csproj
```

## Why this project still exists as a broad suite

The repo now also contains Domain, AdminApi, and Web test projects, but the pre-existing end-to-end and integration-heavy test coverage has been preserved here to avoid breaking the validated backend during the structural split. Over time, tests can be redistributed into the narrower layer-specific projects as the new Application layer takes on more orchestration logic. The responsibility tags and breakup tracker are there to keep that migration explicit.

## Role in the overall platform

For a platform that aims to be trustworthy and explainable, tests are not just a delivery safeguard. They are part of the product promise. This project is where the repo proves that merge outcomes, change detection, and long-term behavior control stay coherent as the platform evolves.