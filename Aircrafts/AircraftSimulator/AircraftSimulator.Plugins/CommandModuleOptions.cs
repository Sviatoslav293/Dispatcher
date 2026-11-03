namespace AircraftSimulator.Plugins;

public sealed class CommandModuleOptions
{
    public TimeSpan ManualHeadingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public double MinimumAltitudeMeters { get; set; } = 0;
    public double MaximumAltitudeMeters { get; set; } = 20_000;
    public double MaximumSpeedKmh { get; set; } = 900;

    public static CommandModuleOptions Default { get; } = new();

    public void Validate()
    {
        if (ManualHeadingDuration <= TimeSpan.Zero
            || !double.IsFinite(MinimumAltitudeMeters) || MinimumAltitudeMeters < 0
            || !double.IsFinite(MaximumAltitudeMeters) || MaximumAltitudeMeters <= MinimumAltitudeMeters
            || !double.IsFinite(MaximumSpeedKmh) || MaximumSpeedKmh <= 0)
        {
            throw new InvalidOperationException("Command module limits and manual heading duration must be valid positive values.");
        }
    }
}
