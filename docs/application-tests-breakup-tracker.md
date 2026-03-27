# Application.Tests breakup tracker

Status: active technical debt

ProductNormaliser.Application.Tests should remain a temporary cross-cutting backend suite, not the default destination for every new test.

## Guardrails

- New fixtures go to the narrower owning test project unless the test spans multiple backend layers or depends on this shared harness.
- If a fixture only targets one assembly boundary, move it when that file is next touched for meaningful work.
- Every fixture in this project must keep a `Responsibility:*` category so it stays visible in filtered runs and future migration passes.

## Candidate migration buckets

- Domain or schema rules: normalisation, attribute dictionaries, measurement parsing, and category-schema fixtures.
- Infrastructure integration: repository, extraction, and source-product construction fixtures.
- Worker runtime: crawl orchestration, queue priority, and runtime pipeline fixtures.
- Admin API read models: admin observability and data-intelligence projection fixtures.
- Application services: discovery, source-management, and longitudinal intelligence fixtures that still bridge multiple backend concerns today.

## Exit criteria

- Application.Tests is mostly cross-layer integration coverage.
- Single-boundary fixtures have an owning test project outside this catch-all suite.
- Adding a new backend test here requires an explicit justification instead of being the default path.