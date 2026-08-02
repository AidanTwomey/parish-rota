namespace ParishRota.Domain;

/// <summary>
/// The span of the liturgical year one Rota covers. Both bounds are inclusive:
/// <paramref name="End"/> is the last day Readers are rostered for, not the day
/// the next period begins.
/// </summary>
public sealed record RotaPeriod(string Name, DateOnly Start, DateOnly End);
