using ProductNormaliser.Core.Models;
using ProductNormaliser.Core.Normalisation;
using ProductNormaliser.Core.Schemas;

namespace ProductNormaliser.Tests;

[Category(TestResponsibilities.Normalisation)]
public sealed class CategoryNormalisationRegressionFixtureTests
{
    private static readonly CategoryAttributeNormaliserRegistry Registry = DefaultCategoryRegistries.CreateAttributeNormaliserRegistry();

    [Test]
    public void TvFixture_NormalisesRepresentativeRetailerSpecTable()
    {
        var result = Normalise("tv", new Dictionary<string, SourceAttributeValue>
        {
            ["Screen Diagonal"] = CreateAttribute("Screen Diagonal", "139.7 cm"),
            ["Native Resolution"] = CreateAttribute("Native Resolution", "3840 x 2160"),
            ["Display Technology"] = CreateAttribute("Display Technology", "qled"),
            ["Smart TV"] = CreateAttribute("Smart TV", "Yes"),
            ["Number of HDMI Ports"] = CreateAttribute("Number of HDMI Ports", "4")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["screen_size_inch"].Value, Is.EqualTo(55m));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("4K"));
            Assert.That(result["display_technology"].Value, Is.EqualTo("QLED"));
            Assert.That(result["smart_tv"].Value, Is.EqualTo(true));
            Assert.That(result["hdmi_port_count"].Value, Is.EqualTo(4));
        });
    }

    [Test]
    public void MonitorFixture_NormalisesRepresentativeRetailerSpecBlock()
    {
        var result = Normalise("monitor", new Dictionary<string, SourceAttributeValue>
        {
            ["Panel Technology"] = CreateAttribute("Panel Technology", "ips"),
            ["Screen Resolution"] = CreateAttribute("Screen Resolution", "2560 x 1440"),
            ["Display Size"] = CreateAttribute("Display Size", "68.6 cm"),
            ["DisplayPort Inputs"] = CreateAttribute("DisplayPort Inputs", "2")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["panel_type"].Value, Is.EqualTo("IPS"));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("1440p"));
            Assert.That(result["screen_size_inch"].Value, Is.EqualTo(27m));
            Assert.That(result["displayport_port_count"].Value, Is.EqualTo(2));
        });
    }

    [Test]
    public void LaptopFixture_NormalisesRepresentativeRetailerListing()
    {
        var result = Normalise("laptop", new Dictionary<string, SourceAttributeValue>
        {
            ["Processor Model"] = CreateAttribute("Processor Model", "Intel Core Ultra 7 155H"),
            ["Memory"] = CreateAttribute("Memory", "16 GB"),
            ["Storage Capacity"] = CreateAttribute("Storage Capacity", "1 TB"),
            ["Drive Type"] = CreateAttribute("Drive Type", "solid state drive"),
            ["Screen Resolution"] = CreateAttribute("Screen Resolution", "FHD"),
            ["Weight"] = CreateAttribute("Weight", "1250 g")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["cpu_model"].Value, Is.EqualTo("Intel Core Ultra 7 155H"));
            Assert.That(result["ram_gb"].Value, Is.EqualTo(16));
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(1024));
            Assert.That(result["storage_type"].Value, Is.EqualTo("SSD"));
            Assert.That(result["native_resolution"].Value, Is.EqualTo("1080p"));
            Assert.That(result["weight_kg"].Value, Is.EqualTo(1.25m));
        });
    }

    [Test]
    public void SmartphoneFixture_NormalisesRepresentativeMobileSpecTable()
    {
        var result = Normalise("smartphone", new Dictionary<string, SourceAttributeValue>
        {
            ["Internal Storage"] = CreateAttribute("Internal Storage", "256 GB"),
            ["Network Type"] = CreateAttribute("Network Type", "5G NR"),
            ["SIM Type"] = CreateAttribute("SIM Type", "nano sim + esim"),
            ["Ingress Protection"] = CreateAttribute("Ingress Protection", "IP 68")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(256));
            Assert.That(result["cellular_generation"].Value, Is.EqualTo("5G"));
            Assert.That(result["sim_form_factor"].Value, Is.EqualTo("Nano-SIM + eSIM"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IP68"));
        });
    }

    [Test]
    public void TabletFixture_NormalisesRepresentativeRetailerSpecGrid()
    {
        var result = Normalise("tablet", new Dictionary<string, SourceAttributeValue>
        {
            ["Internal Storage"] = CreateAttribute("Internal Storage", "1 TB"),
            ["Connectivity"] = CreateAttribute("Connectivity", "Wi Fi + 5G"),
            ["Network Type"] = CreateAttribute("Network Type", "5G"),
            ["Keyboard"] = CreateAttribute("Keyboard", "Included")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["storage_capacity_gb"].Value, Is.EqualTo(1024));
            Assert.That(result["connectivity"].Value, Is.EqualTo("Wi-Fi + Cellular"));
            Assert.That(result["cellular_generation"].Value, Is.EqualTo("5G"));
            Assert.That(result["keyboard_support"].Value, Is.EqualTo(true));
        });
    }

    [Test]
    public void HeadphonesFixture_NormalisesRepresentativeRetailerAccessoryTable()
    {
        var result = Normalise("headphones", new Dictionary<string, SourceAttributeValue>
        {
            ["Headphone Style"] = CreateAttribute("Headphone Style", "true wireless earbuds"),
            ["Bluetooth Ver."] = CreateAttribute("Bluetooth Ver.", "v5.3"),
            ["Charging Connector"] = CreateAttribute("Charging Connector", "USB Type C"),
            ["Water Resistance Rating"] = CreateAttribute("Water Resistance Rating", "ipx4")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["form_factor"].Value, Is.EqualTo("In-Ear"));
            Assert.That(result["bluetooth_version"].Value, Is.EqualTo("Bluetooth 5.3"));
            Assert.That(result["charging_port"].Value, Is.EqualTo("USB-C"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IPX4"));
        });
    }

    [Test]
    public void SpeakersFixture_NormalisesRepresentativeRetailerPortableAudioTable()
    {
        var result = Normalise("speakers", new Dictionary<string, SourceAttributeValue>
        {
            ["Speaker Category"] = CreateAttribute("Speaker Category", "portable bluetooth speaker"),
            ["Wireless Connectivity"] = CreateAttribute("Wireless Connectivity", "wi fi"),
            ["Voice Control"] = CreateAttribute("Voice Control", "amazon alexa"),
            ["Waterproof Rating"] = CreateAttribute("Waterproof Rating", "ip67")
        });

        Assert.Multiple(() =>
        {
            Assert.That(result["speaker_type"].Value, Is.EqualTo("Portable Bluetooth"));
            Assert.That(result["connection_type"].Value, Is.EqualTo("Wi-Fi"));
            Assert.That(result["voice_assistant"].Value, Is.EqualTo("Alexa"));
            Assert.That(result["ip_rating"].Value, Is.EqualTo("IP67"));
        });
    }

    private static IReadOnlyDictionary<string, NormalisedAttributeValue> Normalise(string categoryKey, IDictionary<string, SourceAttributeValue> attributes)
    {
        return Registry.Normalise(categoryKey, attributes.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
    }

    private static SourceAttributeValue CreateAttribute(string attributeKey, string value)
    {
        return new SourceAttributeValue
        {
            AttributeKey = attributeKey,
            Value = value,
            ValueType = "string"
        };
    }
}