namespace SpotiFlow.Logic;
/// <summary>
/// Atrybuty audio, wszystkie w przedziale 0-1
///  - Danceability  - taneczność
///  - Energy        - energia
///  - Valence       - nastrój (0 = smutny, 1 = radosny)
///  - Acousticness  - akustyczność (1 = gitara akustyczna, 0 = elektronika)
/// </summary>
///  <returns></returns>
public record AudioProfile(double Danceability, double Energy, double Valence, double Acousticness)
{
/// <summary>
/// Odwracanie wartości przy pominięciu
/// </summary>
/// <returns></returns>
    public AudioProfile Inverted() => new(
        1 - Danceability,
        1 - Energy,
        1 - Valence,
        1 - Acousticness);

    /// <summary>
    /// Obliczanie odległości kanddata do słuchanej piosenki
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public double DistanceTo(AudioProfile other) =>
        Math.Abs(Danceability - other.Danceability) +
        Math.Abs(Energy - other.Energy) +
        Math.Abs(Valence - other.Valence) +
        Math.Abs(Acousticness - other.Acousticness);

    public override string ToString() =>
        $"taneczność {Danceability:0.00} - energia {Energy:0.00} - nastrój {Valence:0.00} - akustyczność {Acousticness:0.00}";
}
