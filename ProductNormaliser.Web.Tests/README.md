# ProductNormaliser.Web.Tests

ProductNormaliser.Web.Tests is the focused test project for the web UI host.

It is intended to hold tests for:

- UI host composition
- page and component behavior
- HTTP client integration boundaries to backend APIs
- browser-smoke coverage for critical operator flows using Playwright against a real local test host
- frontend-specific view and interaction logic as the dashboard grows
- source registration and source-management flows
- category-seeded crawl launch validation and operator boot-and-populate workflow rendering
- source-candidate discovery rendering, guarded automation controls, and classification-informed operator explanations

## Build

```bash
dotnet build ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj
```

## Test

```bash
dotnet test ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj
```

## Playwright browser smoke tests

The browser layer uses Playwright to launch a real Chromium instance against a real local Kestrel host wired to the existing fake admin client.

Build the project once so the Playwright install script is generated:

```bash
dotnet build ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj
```

Install Chromium:

```powershell
pwsh ProductNormaliser.Web.Tests/bin/Debug/net10.0/playwright.ps1 install chromium
```

Run only the browser smoke layer:

```bash
dotnet test ProductNormaliser.Web.Tests/ProductNormaliser.Web.Tests.csproj --filter "Category=BrowserSmoke"
```

If Chromium is not installed, the Playwright smoke fixture is ignored rather than failing the rest of the suite.