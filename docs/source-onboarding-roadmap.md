# Source Onboarding Roadmap

This note turns the current code-review findings into an incremental delivery plan for closing the gap between the existing operator-assisted candidate discovery flow and the actual product goal:

> discover good product-spec sources on the web, register them as sources, and crawl them to build the product database, while still preserving a first-class manual workflow.

The plan is intentionally additive. It builds on the current implementation in the Sources area, keeps manual source registration first-class, and delays worker-heavy automation until the platform can validate that accepted sources are both crawlable and extractable.

## Current baseline

Today the system can:

- discover source candidates statelessly from the Sources page
- score candidates with lightweight search and probe heuristics
- register sources manually through the existing source-management path
- seed category crawls only from already registered and enabled sources
- discover listing and product URLs from registered sources
- ingest product data only when extraction succeeds, which currently relies heavily on schema.org JSON-LD

The gaps are:

1. no clean bridge from candidate result to registered source
2. source quality checks are still shallow
3. extraction robustness is not strong enough to trust source onboarding automatically
4. downstream promoted product crawl work is not fully linked back to the initiating job
5. locale and market are mainly search hints, not onboarding or crawl boundaries
6. guarded automation does not yet exist

## Recommended priority order

1. Phase 21: close the onboarding gap
2. Phase 22: prove extractability and source quality before trust
3. Phase 23: improve extraction robustness and no-product handling
4. Phase 24: link downstream product crawl work into job observability
5. Phase 25: add explicit market and locale controls to sources and candidate onboarding
6. Phase 26: add guarded automation only after the earlier phases are stable

This order matches the current codebase. It avoids introducing persistence-heavy automation before the platform can tell whether a source is actually useful.

## Phase 21: Close The Onboarding Gap

### Phase goal

Bridge a discovered candidate into registered source creation without weakening the existing manual path.

### Why this phase is needed

The current candidate discovery flow ends in a dead end: candidates are visible, but registration still happens through a separate manual form and endpoint. Until that bridge exists, the system still does not behave like a source-onboarding workflow.

### Recommended scope

Implement this in two slices:

1. Slice 21A, must-have: prefill registration from a selected candidate result.
2. Slice 21B, must-have before the product goal is claimed: explicit accept-and-register using the same underlying registration rules.

The key principle is that acceptance should reuse the existing source-registration path rather than bypass it.

### Likely files, services, and pages to change

- [ProductNormaliser.Web/Pages/Sources/Index.cshtml](../ProductNormaliser.Web/Pages/Sources/Index.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs](../ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs)
- [ProductNormaliser.Web/Contracts/AdminApiContracts.cs](../ProductNormaliser.Web/Contracts/AdminApiContracts.cs)
- [ProductNormaliser.Web/Services/IProductNormaliserAdminApiClient.cs](../ProductNormaliser.Web/Services/IProductNormaliserAdminApiClient.cs)
- [ProductNormaliser.Web/Services/ProductNormaliserAdminApiClient.cs](../ProductNormaliser.Web/Services/ProductNormaliserAdminApiClient.cs)
- [ProductNormaliser.Web/Services/AdminApiContractValidator.cs](../ProductNormaliser.Web/Services/AdminApiContractValidator.cs)
- [ProductNormaliser.AdminApi/Contracts/RegisterSourceRequest.cs](../ProductNormaliser.AdminApi/Contracts/RegisterSourceRequest.cs)
- [ProductNormaliser.AdminApi/Controllers/SourcesController.cs](../ProductNormaliser.AdminApi/Controllers/SourcesController.cs)
- [ProductNormaliser.Application/Sources/SourceManagementService.cs](../ProductNormaliser.Application/Sources/SourceManagementService.cs)
- [ProductNormaliser.Domain/Models/CrawlSource.cs](../ProductNormaliser.Domain/Models/CrawlSource.cs) if source metadata needs light augmentation for acceptance provenance

### Implementation approach

- Slice 21A: add a Use candidate action on the Sources page that copies candidate data into the existing registration form.
- Map candidate `DisplayName`, `BaseUrl`, supported categories, and conservative probe-derived discovery hints into editable registration inputs.
- Keep the form editable before submission so manual workflow remains first-class.
- Slice 21B: add an explicit Accept candidate action that still posts through the normal `POST /api/sources` registration pipeline, not a separate persistence path.
- Only include probe-derived discovery hints that are low-risk defaults, such as sitemap URLs and category entry page hints. Do not auto-apply aggressive allow or deny rules yet.
- Store a minimal acceptance note only if it materially helps later observability. If provenance is added, keep it small and additive.

### Must-have or optional

- Slice 21A: must-have
- Slice 21B: must-have
- Candidate provenance metadata: optional in this phase, useful later for observability

### Key risks

- probe-derived hints may overfit noisy candidate pages and create bad default discovery profiles
- a direct accept path may drift from the manual registration rules unless it reuses `SourceManagementService`
- UI complexity may rise if candidate details and registration editing are mixed poorly

### Suggested tests

- web page tests for selecting a candidate and pre-populating the registration form
- web page tests for editing a prefilled registration before submission
- admin API tests verifying accepted candidates still fail on the same validation rules as manual registration
- application tests verifying discovery-profile defaults remain conservative when candidate hints are missing or partial

Suggested test files:

- [ProductNormaliser.Web.Tests/SourceManagementPageTests.cs](../ProductNormaliser.Web.Tests/SourceManagementPageTests.cs)
- [ProductNormaliser.Web.Tests/SourceManagementRenderingTests.cs](../ProductNormaliser.Web.Tests/SourceManagementRenderingTests.cs)
- [ProductNormaliser.AdminApi.Tests/SourcesControllerTests.cs](../ProductNormaliser.AdminApi.Tests/SourcesControllerTests.cs)
- [ProductNormaliser.Application.Tests/CategoryManagementServiceTests.cs](../ProductNormaliser.Application.Tests/CategoryManagementServiceTests.cs)

### What success looks like

- an operator can move from candidate discovery to a ready-to-submit registration in one action
- accepted candidates become ordinary registered sources with no separate lifecycle
- manual registration still works cleanly without candidate discovery

## Phase 22: Prove Source Quality And Extractability

### Phase goal

Extend candidate validation far enough that the system can distinguish between a site that merely looks plausible and a site that is likely to yield usable product records.

### Why this phase is needed

Right now candidate quality is mostly inferred from homepage, robots, category keywords, and simple URL-pattern heuristics. That is not enough to trust a source for onboarding, and it is definitely not enough for later auto-acceptance.

### Recommended scope

Add deeper but still stateless candidate validation. Do not add source persistence or autonomous workers yet.

### Likely files, services, and pages to change

- [ProductNormaliser.Application/Sources/ISourceCandidateProbeService.cs](../ProductNormaliser.Application/Sources/ISourceCandidateProbeService.cs)
- [ProductNormaliser.Application/Sources/SourceCandidateProbeResult.cs](../ProductNormaliser.Application/Sources/SourceCandidateProbeResult.cs)
- [ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs](../ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs)
- [ProductNormaliser.Infrastructure/Sources/HttpSourceCandidateProbeService.cs](../ProductNormaliser.Infrastructure/Sources/HttpSourceCandidateProbeService.cs)
- [ProductNormaliser.Infrastructure/Sources/SearchApiSourceCandidateSearchProvider.cs](../ProductNormaliser.Infrastructure/Sources/SearchApiSourceCandidateSearchProvider.cs)
- [ProductNormaliser.AdminApi/Contracts/SourceCandidateProbeDto.cs](../ProductNormaliser.AdminApi/Contracts/SourceCandidateProbeDto.cs)
- [ProductNormaliser.AdminApi/Controllers/SourceCandidateDiscoveryController.cs](../ProductNormaliser.AdminApi/Controllers/SourceCandidateDiscoveryController.cs)
- [ProductNormaliser.Web/Pages/Sources/Index.cshtml](../ProductNormaliser.Web/Pages/Sources/Index.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs](../ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs)

### Implementation approach

- probe beyond the homepage by sampling:
  - one or more inferred category pages
  - one likely product page when available
- enrich probe results with:
  - representative page fetch success
  - whether structured product evidence is present on representative product pages
  - whether spec tables or recurring technical-attribute blocks exist even when schema.org does not
  - whether the site returns mostly search, blog, support, or marketing pages instead of product catalog pages
- separate score dimensions:
  - crawlability confidence
  - category relevance confidence
  - extractability confidence
  - duplicate or regional-overlap risk
- mark candidates as:
  - recommended
  - manual review only
  - do not accept

### Must-have or optional

- deeper category and product-page validation: must-have
- richer multi-dimensional scoring: must-have
- full candidate-state persistence: optional and not recommended yet

### Key risks

- extra probing can become slow or noisy if it fetches too many pages
- sample pages may be misleading on large mixed-purpose domains
- adding a hard threshold too early may hide useful niche sources from operators

### Suggested tests

- application tests for candidate scoring when homepage signals are good but representative pages fail
- application tests for candidates with crawlable pages but no product evidence
- probe-service tests for mixed retailer, manufacturer, and irrelevant support-content domains
- web tests verifying the UI distinguishes recommended candidates from manual-review-only candidates

Suggested test files:

- [ProductNormaliser.Application.Tests/SourceCandidateDiscoveryServiceTests.cs](../ProductNormaliser.Application.Tests/SourceCandidateDiscoveryServiceTests.cs)
- [ProductNormaliser.Application.Tests/HttpSourceCandidateProbeServiceTests.cs](../ProductNormaliser.Application.Tests/HttpSourceCandidateProbeServiceTests.cs)
- [ProductNormaliser.Application.Tests/SearchApiSourceCandidateSearchProviderTests.cs](../ProductNormaliser.Application.Tests/SearchApiSourceCandidateSearchProviderTests.cs)
- [ProductNormaliser.Web.Tests/SourceManagementPageTests.cs](../ProductNormaliser.Web.Tests/SourceManagementPageTests.cs)

### What success looks like

- a candidate that looks reachable but yields no plausible product evidence is clearly downgraded
- a candidate that exposes product pages and useful spec signals is clearly distinguished from a generic retailer domain
- operators can decide whether to accept a source without manually trial-crawling it first

## Phase 23: Improve Extraction Robustness

### Phase goal

Reduce the gap between crawl success and product ingestion success.

### Why this phase is needed

Even a correctly onboarded source may fail to populate the product database if product pages do not expose JSON-LD in the current expected form. Until that changes, source validation and later automation will both underperform.

### Recommended scope

Start with extraction validation and lightweight fallbacks. Do not attempt a broad scraper redesign.

### Likely files, services, and pages to change

- [ProductNormaliser.Domain/Interfaces/IStructuredDataExtractor.cs](../ProductNormaliser.Domain/Interfaces/IStructuredDataExtractor.cs)
- [ProductNormaliser.Infrastructure/StructuredData/SchemaOrgJsonLdExtractor.cs](../ProductNormaliser.Infrastructure/StructuredData/SchemaOrgJsonLdExtractor.cs)
- [ProductNormaliser.Worker/CrawlOrchestrator.cs](../ProductNormaliser.Worker/CrawlOrchestrator.cs)
- [ProductNormaliser.Worker/Program.cs](../ProductNormaliser.Worker/Program.cs)
- [ProductNormaliser.Domain/Models/CrawlLog.cs](../ProductNormaliser.Domain/Models/CrawlLog.cs) if crawl outcomes need a richer no-product reason
- [ProductNormaliser.Domain/Models/SourceQualitySnapshot.cs](../ProductNormaliser.Domain/Models/SourceQualitySnapshot.cs) if extractability or no-product rates are surfaced in quality views
- [ProductNormaliser.AdminApi/Services/SourceOperationalInsightsProvider.cs](../ProductNormaliser.AdminApi/Services/SourceOperationalInsightsProvider.cs)
- [ProductNormaliser.Web/Pages/Sources/Details.cshtml](../ProductNormaliser.Web/Pages/Sources/Details.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Intelligence.cshtml](../ProductNormaliser.Web/Pages/Sources/Intelligence.cshtml)

### Implementation approach

- first, make candidate validation reuse the same extractors or evidence detectors that the crawl pipeline uses
- then introduce a composite extraction strategy with conservative fallbacks such as:
  - schema.org JSON-LD
  - product microdata if present
  - stable HTML specification tables or key-value spec blocks for selected rollout categories
- when a crawl fetch succeeds but yields no product records, record that explicitly as an extraction outcome rather than a generic completed crawl
- surface per-source no-product rates and recent extractability failures so operators can disable or reconfigure poor sources quickly

### Must-have or optional

- explicit no-product outcome tracking: must-have
- lightweight fallback extraction for the rollout categories: must-have before claiming broad onboarding success
- broad custom scraper framework per site: optional and later

### Key risks

- fallback extraction can introduce low-quality or hallucinated attributes if parsing is too loose
- category-specific fallback parsing may sprawl if not constrained to the rollout categories
- more permissive extraction can increase merge noise and disagreement churn

### Suggested tests

- extractor tests for JSON-LD, microdata, and spec-table fallbacks on representative fixtures
- crawl-orchestrator tests for pages that fetch successfully but produce no products
- source-intelligence tests for surfacing extractability failure rates

Suggested test files:

- [ProductNormaliser.Application.Tests/DiscoveryRuntimeTests.cs](../ProductNormaliser.Application.Tests/DiscoveryRuntimeTests.cs)
- [ProductNormaliser.Application.Tests/DataIntelligenceTests.cs](../ProductNormaliser.Application.Tests/DataIntelligenceTests.cs)
- [ProductNormaliser.Web.Tests/SourceManagementRenderingTests.cs](../ProductNormaliser.Web.Tests/SourceManagementRenderingTests.cs)

### What success looks like

- a technically successful crawl that yields zero products is visible as a real quality problem
- useful sources without clean JSON-LD can still contribute products for the rollout categories
- candidate acceptance can rely on the same evidence that real crawling uses

## Phase 24: Link Downstream Product Crawl Work Into Job Observability

### Phase goal

Make it clear whether a discovery or category crawl actually produced crawlable products and usable product records, not just discovered URLs.

### Why this phase is needed

The current queue and job model under-reports downstream product crawl work promoted out of discovery. Operators can see discovered URL counts and some queue metrics, but they cannot reliably treat the initiating job as the full story.

### Recommended scope

Add lightweight origin linkage and surface it in the existing crawl job detail views. Do not redesign the queue model.

### Likely files, services, and pages to change

- [ProductNormaliser.Domain/Models/CrawlQueueItem.cs](../ProductNormaliser.Domain/Models/CrawlQueueItem.cs)
- [ProductNormaliser.Domain/Models/CrawlJob.cs](../ProductNormaliser.Domain/Models/CrawlJob.cs)
- [ProductNormaliser.Application/Discovery/ProductTargetEnqueuer.cs](../ProductNormaliser.Application/Discovery/ProductTargetEnqueuer.cs)
- [ProductNormaliser.Infrastructure/Crawling/CrawlQueueService.cs](../ProductNormaliser.Infrastructure/Crawling/CrawlQueueService.cs)
- [ProductNormaliser.Application/Crawls/CrawlJobService.cs](../ProductNormaliser.Application/Crawls/CrawlJobService.cs)
- [ProductNormaliser.AdminApi/Controllers/CrawlJobsController.cs](../ProductNormaliser.AdminApi/Controllers/CrawlJobsController.cs)
- [ProductNormaliser.AdminApi/Contracts/CrawlJobDto.cs](../ProductNormaliser.AdminApi/Contracts/CrawlJobDto.cs)
- [ProductNormaliser.Web/Contracts/AdminApiContracts.cs](../ProductNormaliser.Web/Contracts/AdminApiContracts.cs)
- [ProductNormaliser.Web/Pages/CrawlJobs/Details.cshtml](../ProductNormaliser.Web/Pages/CrawlJobs/Details.cshtml)
- [ProductNormaliser.Web/Pages/CrawlJobs/Details.cshtml.cs](../ProductNormaliser.Web/Pages/CrawlJobs/Details.cshtml.cs)
- [ProductNormaliser.AdminApi/Services/SourceOperationalInsightsProvider.cs](../ProductNormaliser.AdminApi/Services/SourceOperationalInsightsProvider.cs)

### Implementation approach

- add a lightweight initiating-job or discovery-origin field to promoted crawl targets instead of relying only on `JobId`
- count and surface:
  - promoted product targets
  - promoted product targets processed
  - promoted product targets that yielded products
  - promoted product targets with fetch success but zero extracted products
- update crawl job detail views so operators can see the whole path:
  - seeds written
  - discovery pages processed
  - product targets promoted
  - product targets crawled
  - products extracted

### Must-have or optional

- origin linkage and downstream metrics: must-have
- richer dashboards beyond the current job pages: optional

### Key risks

- if origin linkage is attached to the wrong field, recurring crawl scheduling behavior may regress
- counts can become confusing if one promoted product target contributes to multiple logical summaries
- job completion semantics may need careful wording if downstream promoted work can outlive the initial discovery pass

### Suggested tests

- application tests for promoted targets carrying origin metadata
- crawl-job service tests for downstream counts and terminal state updates
- admin API tests for expanded crawl-job contract shapes
- web tests for crawl-job detail rendering of promoted-target metrics

Suggested test files:

- [ProductNormaliser.Application.Tests/CrawlJobServiceTests.cs](../ProductNormaliser.Application.Tests/CrawlJobServiceTests.cs)
- [ProductNormaliser.Application.Tests/DiscoveryApplicationServiceTests.cs](../ProductNormaliser.Application.Tests/DiscoveryApplicationServiceTests.cs)
- [ProductNormaliser.AdminApi.Tests](../ProductNormaliser.AdminApi.Tests)
- [ProductNormaliser.Web.Tests/CrawlJobsPageTests.cs](../ProductNormaliser.Web.Tests/CrawlJobsPageTests.cs)

### What success looks like

- operators can tell whether a crawl job found products, not just URLs
- a source can be judged on downstream yield, not only discovery volume
- later automation has the telemetry needed for safe guardrails

## Phase 25: Add Market And Locale Controls

### Phase goal

Make market and locale part of source onboarding and crawl policy rather than only search-query hints.

### Why this phase is needed

The product primarily cares about the UK but wants operator-controlled allowed markets. Without explicit source-level market metadata, duplicate regional variants and off-market sources will continue to leak into candidate recommendations and onboarding.

### Recommended scope

Add a small source market model and use it in candidate ranking, acceptance, and source management. Keep the operator UI simple.

### Likely files, services, and pages to change

- [ProductNormaliser.Domain/Models/CrawlSource.cs](../ProductNormaliser.Domain/Models/CrawlSource.cs)
- [ProductNormaliser.Domain/Models/SourceDiscoveryProfile.cs](../ProductNormaliser.Domain/Models/SourceDiscoveryProfile.cs)
- [ProductNormaliser.Application/Sources/SourceManagementService.cs](../ProductNormaliser.Application/Sources/SourceManagementService.cs)
- [ProductNormaliser.Application/Sources/DiscoverSourceCandidatesRequest.cs](../ProductNormaliser.Application/Sources/DiscoverSourceCandidatesRequest.cs)
- [ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs](../ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs)
- [ProductNormaliser.Infrastructure/Sources/SearchApiSourceCandidateSearchProvider.cs](../ProductNormaliser.Infrastructure/Sources/SearchApiSourceCandidateSearchProvider.cs)
- [ProductNormaliser.AdminApi/Contracts/RegisterSourceRequest.cs](../ProductNormaliser.AdminApi/Contracts/RegisterSourceRequest.cs)
- [ProductNormaliser.AdminApi/Contracts/SourceDto.cs](../ProductNormaliser.AdminApi/Contracts/SourceDto.cs)
- [ProductNormaliser.Web/Contracts/AdminApiContracts.cs](../ProductNormaliser.Web/Contracts/AdminApiContracts.cs)
- [ProductNormaliser.Web/Pages/Sources/Index.cshtml](../ProductNormaliser.Web/Pages/Sources/Index.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Details.cshtml](../ProductNormaliser.Web/Pages/Sources/Details.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Details.cshtml.cs](../ProductNormaliser.Web/Pages/Sources/Details.cshtml.cs)

### Implementation approach

- add explicit allowed-market and preferred-locale fields to sources, defaulting conservatively to UK-first settings where appropriate
- use these fields in candidate deduplication and ranking so `example.co.uk` and `example.com` do not look like equally good independent candidates for the same operator scope
- allow the acceptance flow to set market and locale explicitly when converting a candidate into a source
- keep the manual source form first-class by exposing the same fields there
- do not overcomplicate the UI: an allowed-market selector plus preferred locale is sufficient for the first pass

### Must-have or optional

- source-level market and locale metadata: must-have
- sophisticated cross-market routing or geo-testing: optional later

### Key risks

- a simple market model may still be too coarse for multinational retailers
- domain-based regional assumptions can misclassify sites using path or cookie-based localization
- expanding source identity with market metadata may affect duplicate detection rules

### Suggested tests

- source-management tests for registering and editing market metadata
- candidate-discovery tests for regional duplicate handling and ranking
- web tests for market and locale fields in both manual and candidate-assisted registration

Suggested test files:

- [ProductNormaliser.Application.Tests/SourceCandidateDiscoveryServiceTests.cs](../ProductNormaliser.Application.Tests/SourceCandidateDiscoveryServiceTests.cs)
- [ProductNormaliser.AdminApi.Tests/SourcesControllerTests.cs](../ProductNormaliser.AdminApi.Tests/SourcesControllerTests.cs)
- [ProductNormaliser.Web.Tests/SourceManagementPageTests.cs](../ProductNormaliser.Web.Tests/SourceManagementPageTests.cs)

### What success looks like

- operators can restrict onboarding to approved markets cleanly
- regional site variants are less likely to create duplicate source records
- candidate acceptance produces sources with explicit market boundaries instead of implicit query hints

## Phase 26: Add Guarded Automation

### Phase goal

Introduce optional automation only after source acceptance, validation, extraction confidence, and observability are good enough to trust.

### Why this phase is needed

Automation is the last step, not the first one. Without the earlier phases, automation would only scale noisy acceptance, shallow validation, and weak observability.

### Recommended scope

Roll automation out in three guarded modes, all opt-in and reversible.

### Likely files, services, and pages to change

- [ProductNormaliser.Web/Pages/Sources/Index.cshtml](../ProductNormaliser.Web/Pages/Sources/Index.cshtml)
- [ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs](../ProductNormaliser.Web/Pages/Sources/Index.cshtml.cs)
- [ProductNormaliser.Web/Pages/Sources/Details.cshtml](../ProductNormaliser.Web/Pages/Sources/Details.cshtml)
- [ProductNormaliser.AdminApi/appsettings.json](../ProductNormaliser.AdminApi/appsettings.json)
- [ProductNormaliser.AdminApi/Program.cs](../ProductNormaliser.AdminApi/Program.cs)
- [ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs](../ProductNormaliser.Application/Sources/SourceCandidateDiscoveryService.cs)
- [ProductNormaliser.Application/Sources/SourceManagementService.cs](../ProductNormaliser.Application/Sources/SourceManagementService.cs)
- [ProductNormaliser.Application/Discovery/SourceDiscoveryService.cs](../ProductNormaliser.Application/Discovery/SourceDiscoveryService.cs)
- [ProductNormaliser.Application/Crawls/CrawlJobService.cs](../ProductNormaliser.Application/Crawls/CrawlJobService.cs)
- [ProductNormaliser.Worker/Program.cs](../ProductNormaliser.Worker/Program.cs) only if a later scheduled automation worker is genuinely required

### Implementation approach

Roll out in this order:

1. operator-assisted only: current flow plus acceptance and validation gates
2. auto-accept suggestion: the system marks candidates as safe to accept, but the operator still confirms
3. optional auto-accept and auto-seed: for high-confidence candidates only, behind explicit source-management settings and conservative thresholds

Automation guardrails should require:

- approved market match
- no governance warning
- low duplicate risk
- successful representative-page validation
- extractability confidence above threshold
- clear observability of downstream yield

Automation controls should remain visible on the Sources surface and default to off.

### Must-have or optional

- operator-confirmed suggestion mode: optional but recommended
- true auto-accept and auto-seed: optional and only valid after earlier phases are complete and stable

### Key risks

- false positives can quickly create low-quality registered sources at scale
- auto-seeding crawls can create queue pressure if source validation is too permissive
- operators may lose trust if automated onboarding is hard to explain or reverse

### Suggested tests

- application tests for automation threshold decisions
- admin API tests for automation settings contracts
- web tests for enabling or disabling automation and reviewing suggested candidates
- end-to-end smoke tests proving that automated acceptance still preserves manual override and disable paths

### What success looks like

- operators can leave discovery and onboarding manual, assisted, or partially automated by choice
- high-confidence candidates can be accepted and seeded safely
- the system can be honestly described as automatic source discovery and crawl onboarding for approved markets under guarded policies

## Delivery checkpoints

### Recommended next phase to implement immediately

Phase 21.

Start with Slice 21A first: candidate-to-registration prefill on the Sources page. It is the smallest change that closes the most visible product gap without creating new lifecycle complexity.

### Smallest useful stopping point after that

Finish Phase 21 at Slice 21B and then stop.

That gives the platform a real onboarding bridge:

- discover candidate
- inspect candidate
- accept candidate
- register source through the normal path
- launch seeded crawls from the newly registered source

This is the smallest useful point where the system feels like a coherent operator-assisted onboarding workflow.

### When we can honestly market this as automatic source discovery and crawl onboarding

Not before Phase 26.

More specifically, all of these need to be true first:

1. candidate acceptance exists and uses the normal source-registration rules
2. candidate validation proves representative extractability, not just homepage reachability
3. crawl ingestion can handle useful sources that do not expose clean JSON-LD only
4. job tracking shows whether accepted sources produced products, not just discovered URLs
5. source onboarding is constrained by explicit market and locale rules
6. automation is guarded, explainable, opt-in, and reversible

Before that point, the accurate product statement is still:

> operator-assisted source discovery with manual acceptance and seeded crawl onboarding
