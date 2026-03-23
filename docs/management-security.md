# Management Security Design

## Goals

- require authenticated access for the management web UI
- require authenticated and authorized access for management API endpoints
- persist an operator audit trail for high-impact operational changes
- block obviously unsafe crawl targets and oversize crawl launches before they enter the queue

## Authentication and Authorization

### Web UI

- cookie authentication protects the Razor Pages management surface
- a configured user list supplies operator identities and roles
- the entire UI requires the `operator` role
- unauthenticated users are redirected to `/Login`
- authenticated users without the required role are redirected to `/Forbidden`

### Admin API

- API-key authentication protects every controller route
- each configured API key carries a role claim
- all controller routes require the `operator` role
- requests without a valid key receive `401`
- authenticated keys without the required role receive `403`

### Web-to-API trust boundary

- the web app sends the configured service API key on every Admin API call
- when a web operator is signed in, the web app also forwards the signed-in user id and display name headers
- the Admin API records both the authenticated caller and the forwarded operator identity in audit entries

## Audit Trail

Audit entries are stored as persistent `management_audit_entries` documents with:

- action name
- target type and target id
- timestamp
- authenticated caller identity and type
- forwarded web operator identity when present
- structured detail values relevant to the change

Audited actions in this rollout:

- crawl job creation
- source enable
- source disable
- source category changes

## Crawl Guardrails

### Domain controls

- source base URLs are rejected if their host matches the configured block list
- if an allow list is configured, only matching hosts and subdomains are accepted
- local and private-network targets are rejected unless explicitly allowed

### Bulk crawl safety

- crawl creation is rejected when the resolved target count exceeds `MaxTargetsPerJob`
- large category-wide launches require explicit source selection when `RequireExplicitSourcesForLargeCategoryCrawls` is enabled

### Throttling visibility

- source list and crawl launch cards display the current requests-per-minute, concurrency, and robots posture
- source details continue to expose editable throttling settings

## Configuration

### Admin API

- `ManagementApiSecurity:ApiKeyHeaderName`
- `ManagementApiSecurity:ApiKeys`
- `CrawlGovernance:*`

### Web UI

- `ManagementWebSecurity:CookieName`
- `ManagementWebSecurity:Users`
- `AdminApi:BaseUrl`
- `AdminApi:ApiKeyHeaderName`
- `AdminApi:ApiKey`

## Operational Notes

- the repository ships configuration structure, not production secrets
- production credentials and API keys should be supplied through environment variables or a secrets store
- the forwarded operator headers are only descriptive audit metadata; authorization still depends on the authenticated API key and role