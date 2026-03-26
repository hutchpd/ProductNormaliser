using System.Diagnostics.Metrics;

namespace ProductNormaliser.Tests;

internal sealed class TelemetryMetricCollector : IDisposable
{
    private readonly MeterListener listener;
    private readonly List<TelemetryMeasurement> measurements = [];

    public TelemetryMetricCollector(string meterName)
    {
        listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>(OnMeasurementRecorded);
        listener.SetMeasurementEventCallback<int>(OnMeasurementRecorded);
        listener.SetMeasurementEventCallback<double>(OnMeasurementRecorded);
        listener.Start();
    }

    public IReadOnlyList<TelemetryMeasurement> Measurements => measurements;

    public IReadOnlyList<TelemetryMeasurement> GetMeasurements(string instrumentName)
    {
        return measurements
            .Where(item => string.Equals(item.InstrumentName, instrumentName, StringComparison.Ordinal))
            .ToArray();
    }

    public void Dispose()
    {
        listener.Dispose();
    }

    private void OnMeasurementRecorded<T>(Instrument instrument, T measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        where T : struct
    {
        var capturedTags = tags.ToArray();
        measurements.Add(new TelemetryMeasurement(instrument.Name, measurement, capturedTags));
    }
}

internal sealed record TelemetryMeasurement(
    string InstrumentName,
    object Value,
    IReadOnlyList<KeyValuePair<string, object?>> Tags);