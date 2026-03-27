using ProductNormaliser.Core.Interfaces;
using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class TvAttributeNormaliserTests
{
    [TestCase("Yes", true)]
    [TestCase("No", false)]
    [TestCase("true", true)]
    [TestCase("0", false)]
    public void Normalise_ParsesBooleanValues(string rawValue, bool expected)
    {
        var sut = new TvAttributeNormaliser();

        var result = sut.Normalise(
            "tv",
            new Dictionary<string, SourceAttributeValue>
            {
                ["smart tv"] = new()
                {
                    AttributeKey = "Smart TV",
                    Value = rawValue,
                    ValueType = "string"
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(result["smart_tv"].Value, Is.EqualTo(expected));
            Assert.That(result["smart_tv"].Confidence, Is.EqualTo(0.97m));
            Assert.That(result["smart_tv"].OriginalValue, Is.EqualTo(rawValue));
        });
    }

    [TestCase("4K Ultra HD", "4K")]
    [TestCase("Ultra HD", "4K")]
    [TestCase("full hd", "1080p")]
    public void Normalise_MapsNativeResolutionValues(string rawValue, string expected)
    {
        var result = NormaliseSingle("Native Resolution", rawValue);

        Assert.Multiple(() =>
        {
            Assert.That(result.AttributeKey, Is.EqualTo("native_resolution"));
            Assert.That(result.Value, Is.EqualTo(expected));
            Assert.That(result.Confidence, Is.EqualTo(0.98m));
        });
    }

    [TestCase("OLED", "OLED")]
    [TestCase("qled", "QLED")]
    [TestCase(" led ", "LED")]
    public void Normalise_MapsDisplayTechnologyValues(string rawValue, string expected)
    {
        var result = NormaliseSingle("Display Technology", rawValue);

        Assert.Multiple(() =>
        {
            Assert.That(result.AttributeKey, Is.EqualTo("display_technology"));
            Assert.That(result.Value, Is.EqualTo(expected));
            Assert.That(result.ParseNotes, Is.EqualTo("Mapped known source value to canonical value."));
        });
    }

    [Test]
    public void Normalise_ConvertsCentimetresToMarketingInches()
    {
        var result = NormaliseSingle("Display Size", "140 cm");

        Assert.Multiple(() =>
        {
            Assert.That(result.AttributeKey, Is.EqualTo("screen_size_inch"));
            Assert.That(result.Value, Is.EqualTo(55m));
            Assert.That(result.Unit, Is.EqualTo("inch"));
            Assert.That(result.Confidence, Is.EqualTo(0.96m));
        });
    }

    [Test]
    public void Normalise_ParsesRefreshRateAndPortCount()
    {
        var sut = new TvAttributeNormaliser();

        var results = sut.Normalise(
            "tv",
            new Dictionary<string, SourceAttributeValue>
            {
                ["refresh rate"] = new()
                {
                    AttributeKey = "Refresh Rate",
                    Value = "100 Hz",
                    ValueType = "string"
                },
                ["hdmi ports"] = new()
                {
                    AttributeKey = "HDMI Ports",
                    Value = "4",
                    ValueType = "string"
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(results["refresh_rate_hz"].Value, Is.EqualTo(100));
            Assert.That(results["refresh_rate_hz"].Unit, Is.EqualTo("hz"));
            Assert.That(results["hdmi_port_count"].Value, Is.EqualTo(4));
            Assert.That(results["hdmi_port_count"].ParseNotes, Is.EqualTo("Parsed integer value."));
        });
    }

    [Test]
    public void Normalise_PreservesMalformedValuesAndAddsParseNotes()
    {
        var result = NormaliseSingle("Screen Size", "large");

        Assert.Multiple(() =>
        {
            Assert.That(result.AttributeKey, Is.EqualTo("screen_size_inch"));
            Assert.That(result.Value, Is.EqualTo("large"));
            Assert.That(result.OriginalValue, Is.EqualTo("large"));
            Assert.That(result.Confidence, Is.EqualTo(0.25m));
            Assert.That(result.ParseNotes, Does.Contain("Unable to parse"));
        });
    }

    [Test]
    public void Normalise_UsesCanonicalFallbackForUnknownAttributeNames()
    {
        var sut = new TvAttributeNormaliser();

        var results = sut.Normalise(
            "tv",
            new Dictionary<string, SourceAttributeValue>
            {
                ["Panel Depth"] = new()
                {
                    AttributeKey = "Panel Depth",
                    Value = "42 mm",
                    ValueType = "string"
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(results.ContainsKey("panel_depth"), Is.True);
            Assert.That(results["panel_depth"].Value, Is.EqualTo("42 mm"));
            Assert.That(results["panel_depth"].ParseNotes, Is.EqualTo("No schema definition found; preserved raw value."));
        });
    }

    [Test]
    public void Normalise_RecordsUnmappedAttributes()
    {
        var recorder = new RecordingUnmappedAttributeRecorder();
        var sut = new TvAttributeNormaliser(unmappedAttributeRecorder: recorder);

        sut.Normalise(
            "tv",
            new Dictionary<string, SourceAttributeValue>
            {
                ["Panel Depth"] = new()
                {
                    AttributeKey = "Panel Depth",
                    Value = "42 mm",
                    ValueType = "string",
                    SourcePath = "source:example-retailer|jsonld.additionalProperty"
                }
            });

        Assert.Multiple(() =>
        {
            Assert.That(recorder.Calls, Has.Count.EqualTo(1));
            Assert.That(recorder.Calls[0].CategoryKey, Is.EqualTo("tv"));
            Assert.That(recorder.Calls[0].CanonicalKey, Is.EqualTo("panel_depth"));
            Assert.That(recorder.Calls[0].RawAttribute.AttributeKey, Is.EqualTo("Panel Depth"));
            Assert.That(recorder.Calls[0].RawAttribute.SourcePath, Is.EqualTo("source:example-retailer|jsonld.additionalProperty"));
        });
    }

    private static NormalisedAttributeValue NormaliseSingle(string attributeName, string rawValue)
    {
        var sut = new TvAttributeNormaliser();

        var result = sut.Normalise(
            "tv",
            new Dictionary<string, SourceAttributeValue>
            {
                [attributeName] = new()
                {
                    AttributeKey = attributeName,
                    Value = rawValue,
                    ValueType = "string"
                }
            });

        return result.Values.Single();
    }

    private sealed class RecordingUnmappedAttributeRecorder : IUnmappedAttributeRecorder
    {
        public List<RecordedCall> Calls { get; } = [];

        public void Record(string categoryKey, string canonicalKey, SourceAttributeValue rawAttribute)
        {
            Calls.Add(new RecordedCall
            {
                CategoryKey = categoryKey,
                CanonicalKey = canonicalKey,
                RawAttribute = rawAttribute
            });
        }
    }

    private sealed class RecordedCall
    {
        public string CategoryKey { get; init; } = default!;
        public string CanonicalKey { get; init; } = default!;
        public SourceAttributeValue RawAttribute { get; init; } = default!;
    }
}