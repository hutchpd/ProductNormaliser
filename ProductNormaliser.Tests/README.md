# ProductNormaliser.Tests

ProductNormaliser.Tests is the NUnit validation suite for the entire solution. It covers the domain rules in Core, infrastructure behavior, worker orchestration, admin observability, and the long-term intelligence features added across later phases.

This project is important because ProductNormaliser is not just a CRUD system. Its value depends on nuanced decision logic such as identity resolution, merge weighting, semantic change detection, source trust evolution, and adaptive crawl scheduling. Those behaviors need explicit regression coverage.

## Responsibilities

- validate schema-driven normalisation behavior
- validate extraction and source-product construction
- validate Mongo-backed repositories and integration flows
- validate merge, conflict, and identity decisions
- validate worker orchestration behavior
- validate admin API read models and observability responses
- validate temporal intelligence, adaptive backoff, and disagreement tracking

## Test categories in this project

Representative test areas include:

- attribute alias and name normalisation
- measurement parsing and conversion
- TV attribute normalisation and schema coverage
- JSON-LD and HTML extraction
- source-product building
- Mongo repository integration
- identity and merge scenarios
- delta processing and semantic change detection
- worker orchestration
- admin observability and data-intelligence projections
- temporal intelligence
- adaptive backoff behavior
- source disagreement analytics

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
dotnet test ProductNormaliser.Tests/ProductNormaliser.Tests.csproj
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
dotnet build ProductNormaliser.Tests/ProductNormaliser.Tests.csproj
```

## Role in the overall platform

For a platform that aims to be trustworthy and explainable, tests are not just a delivery safeguard. They are part of the product promise. This project is where the repo proves that merge outcomes, change detection, and long-term behavior control stay coherent as the platform evolves.