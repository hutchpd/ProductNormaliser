using Microsoft.AspNetCore.Mvc;
using ProductNormaliser.AdminApi.Contracts;
using ProductNormaliser.AdminApi.Controllers;
using ProductNormaliser.Application.Sources;
using ProductNormaliser.Core.Models;

namespace ProductNormaliser.AdminApi.Tests;

public sealed class SourcesControllerTests
{
    [Test]
    public async Task GetSources_ReturnsMappedDtos()
    {
        var controller = new SourcesController(new FakeSourceManagementService(CreateSource("alpha")));

        var result = await controller.GetSources();

        var ok = result as OkObjectResult;
        Assert.That(ok, Is.Not.Null);
        var payload = ok!.Value as SourceDto[];
        Assert.That(payload, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(payload!, Has.Length.EqualTo(1));
            Assert.That(payload[0].SourceId, Is.EqualTo("alpha"));
            Assert.That(payload[0].ThrottlingPolicy.MaxConcurrentRequests, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetSource_ReturnsNotFoundForUnknownSource()
    {
        var controller = new SourcesController(new FakeSourceManagementService());

        var result = await controller.GetSource("missing");

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task RegisterSource_ReturnsCreatedResult()
    {
        var service = new FakeSourceManagementService();
        var controller = new SourcesController(service);

        var result = await controller.RegisterSource(new RegisterSourceRequest
        {
            SourceId = "alpha",
            DisplayName = "Alpha",
            BaseUrl = "https://alpha.example",
            SupportedCategoryKeys = ["tv"]
        });

        var created = result as CreatedAtActionResult;
        Assert.That(created, Is.Not.Null);
        var payload = created!.Value as SourceDto;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.SourceId, Is.EqualTo("alpha"));
    }

    [Test]
    public async Task AssignCategories_ReturnsValidationProblemForUnknownCategory()
    {
        var controller = new SourcesController(new FakeSourceManagementService(assignCategoriesException: new ArgumentException("Unknown category keys: smartwatch.", "categoryKeys")));

        var result = await controller.AssignCategories("alpha", new AssignSourceCategoriesRequest
        {
            CategoryKeys = ["smartwatch"]
        });

        Assert.That(result, Is.InstanceOf<ObjectResult>());
        var objectResult = (ObjectResult)result;
        Assert.That(objectResult.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task UpdateThrottling_ReturnsNotFoundForUnknownSource()
    {
        var controller = new SourcesController(new FakeSourceManagementService(throttlingException: new KeyNotFoundException("missing")));

        var result = await controller.UpdateThrottling("missing", new UpdateSourceThrottlingRequest
        {
            MinDelayMs = 1000,
            MaxDelayMs = 2000,
            MaxConcurrentRequests = 1,
            RequestsPerMinute = 10,
            RespectRobotsTxt = true
        });

        Assert.That(result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task EnableAndDisable_ReturnUpdatedSource()
    {
        var controller = new SourcesController(new FakeSourceManagementService(CreateSource("alpha", isEnabled: false)));

        var enabled = await controller.EnableSource("alpha") as OkObjectResult;
        var disabled = await controller.DisableSource("alpha") as OkObjectResult;

        Assert.Multiple(() =>
        {
            Assert.That(((SourceDto)enabled!.Value!).IsEnabled, Is.True);
            Assert.That(((SourceDto)disabled!.Value!).IsEnabled, Is.False);
        });
    }

    private static CrawlSource CreateSource(string id, bool isEnabled = true)
    {
        return new CrawlSource
        {
            Id = id,
            DisplayName = char.ToUpperInvariant(id[0]) + id[1..],
            BaseUrl = $"https://{id}.example",
            Host = $"{id}.example",
            IsEnabled = isEnabled,
            SupportedCategoryKeys = ["tv"],
            ThrottlingPolicy = new SourceThrottlingPolicy
            {
                MinDelayMs = 1000,
                MaxDelayMs = 3000,
                MaxConcurrentRequests = 1,
                RequestsPerMinute = 30,
                RespectRobotsTxt = true
            },
            CreatedUtc = DateTime.UtcNow,
            UpdatedUtc = DateTime.UtcNow
        };
    }

    private sealed class FakeSourceManagementService(
        CrawlSource? source = null,
        Exception? assignCategoriesException = null,
        Exception? throttlingException = null) : ISourceManagementService
    {
        private readonly List<CrawlSource> sources = source is null ? [] : [source];
        private readonly Exception? assignCategoriesException = assignCategoriesException;
        private readonly Exception? throttlingException = throttlingException;

        public Task<IReadOnlyList<CrawlSource>> ListAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CrawlSource>>(sources.ToArray());

        public Task<CrawlSource?> GetAsync(string sourceId, CancellationToken cancellationToken = default)
            => Task.FromResult(sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase)));

        public Task<CrawlSource> RegisterAsync(CrawlSourceRegistration registration, CancellationToken cancellationToken = default)
        {
            var created = CreateSource(registration.SourceId, registration.IsEnabled);
            created.DisplayName = registration.DisplayName;
            created.BaseUrl = registration.BaseUrl;
            created.Host = new Uri(registration.BaseUrl).Host;
            created.Description = registration.Description;
            created.SupportedCategoryKeys = registration.SupportedCategoryKeys.OrderBy(key => key).ToList();
            if (registration.ThrottlingPolicy is not null)
            {
                created.ThrottlingPolicy = registration.ThrottlingPolicy;
            }

            sources.RemoveAll(item => string.Equals(item.Id, created.Id, StringComparison.OrdinalIgnoreCase));
            sources.Add(created);
            return Task.FromResult(created);
        }

        public Task<CrawlSource> UpdateAsync(string sourceId, CrawlSourceUpdate update, CancellationToken cancellationToken = default)
        {
            var existing = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException(sourceId);
            existing.DisplayName = update.DisplayName;
            existing.BaseUrl = update.BaseUrl;
            existing.Description = update.Description;
            existing.Host = new Uri(update.BaseUrl).Host;
            return Task.FromResult(existing);
        }

        public Task<CrawlSource> EnableAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            var existing = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException(sourceId);
            existing.IsEnabled = true;
            return Task.FromResult(existing);
        }

        public Task<CrawlSource> DisableAsync(string sourceId, CancellationToken cancellationToken = default)
        {
            var existing = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException(sourceId);
            existing.IsEnabled = false;
            return Task.FromResult(existing);
        }

        public Task<CrawlSource> AssignCategoriesAsync(string sourceId, IReadOnlyCollection<string> categoryKeys, CancellationToken cancellationToken = default)
        {
            if (assignCategoriesException is not null)
            {
                throw assignCategoriesException;
            }

            var existing = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException(sourceId);
            existing.SupportedCategoryKeys = categoryKeys.OrderBy(key => key).ToList();
            return Task.FromResult(existing);
        }

        public Task<CrawlSource> SetThrottlingAsync(string sourceId, SourceThrottlingPolicy policy, CancellationToken cancellationToken = default)
        {
            if (throttlingException is not null)
            {
                throw throttlingException;
            }

            var existing = sources.FirstOrDefault(item => string.Equals(item.Id, sourceId, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException(sourceId);
            existing.ThrottlingPolicy = policy;
            return Task.FromResult(existing);
        }
    }
}