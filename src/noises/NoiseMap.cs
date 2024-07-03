using Godot;

namespace GPSMining;

[GlobalClass]
/// <summary>
/// A noise map that can use multiple projections
/// </summary>
public partial class NoiseMap : Resource
{
    private static readonly NoiseMap TerrainTemp = GD.Load<NoiseMap>("res://src/noises/TerrainTempSphere.tres");
    private static readonly NoiseMap TerrainPol = GD.Load<NoiseMap>("res://src/noises/TerrainPolSphere.tres");

    /// <summary>
    /// The size of the noise map
    /// </summary>
    private const float NoiseMapSize = Globals.EarthCircumferenceKm * 100;

    /*
    /// <summary>
    /// Size multiplier for a sphere
    /// </summary>
    private const double NoiseSphereFactor = 1;

    /// <summary>
    /// Size multiplier for a sinusoid
    /// </summary>
    private const double NoiseSinFactor = 1;
    */

    private const int NoisePrecision = 1 << 3;

    [ExportCategory("Common")]
    [Export]
    public FastNoiseLite Fnl;

    [Export]
    private float PowerMin = 0f;

    [Export]
    private float PowerMax = 1f;

    [ExportCategory("Temperature parameters")]
    [Export]
    private bool TempAffected = false;

    [Export]
    private float TempLowValue = 0f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float TempLowEnd = 0.2f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float TempMidStart = 0.4f;

    [Export]
    private float TempMidValue = 0f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float TempMidEnd = 0.6f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float TempHighStart = 0.8f;

    [Export]
    private float TempHighValue = 0f;

    [ExportCategory("Polarity parameters")]
    [Export]
    private bool PolAffected = false;

    [Export]
    private float PolLowValue = 0f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float PolLowEnd = 0.2f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float PolMidStart = 0.4f;

    [Export]
    private float PolMidValue = 0f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float PolMidEnd = 0.6f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    private float PolHighStart = 0.8f;

    [Export]
    private float PolHighValue = 0f;

    /// <summary>
    /// Returns a Color based on the noise value
    /// 0 is blue, 0.5 is green, 1 is red, intermediate values are interpolated
    /// </summary>
    /// <param name="noise">Noise</param>
    /// <returns>Color</returns>
    public static Color GetColor(float noise)
    {
        float hue = (1 - noise) * 2 / 3;
        return Color.FromHsv(hue, 1f, 1f);
    }

    /// <summary>
    /// Returns the noise value on given coordinates by using a Mercator projection,
    /// </summary>
    /// <param name="position">Mercator position</param>
    /// <param name="zoom">Current zoom level</param>
    /// <returns>Noise in [0-1] range</returns>
    private float GetMercatorNoise(Vector2I position, int zoom, float offset)
    {
        float noiseMod = MercatorMap.GetMaxPosition(zoom);
        float noiseX = Mathf.Floor(NoiseMapSize * (float)Globals.DefaultScale * ((position.X / noiseMod) - 0.5f) * NoisePrecision) / NoisePrecision;
        float noiseY = Mathf.Floor(NoiseMapSize * (float)Globals.DefaultScale * ((position.Y / noiseMod) - 0.5f) * NoisePrecision) / NoisePrecision;

        float noiseRaw = Fnl.GetNoise2D((float)noiseX, (float)noiseY);

        float noise = Mathf.Clamp(offset + (noiseRaw - PowerMin) / (PowerMax - PowerMin), 0, 1);

        return noise;
    }

    public float GetNoise(Vector2I position, int zoom)
    {
        float offset = 0;
        if (TempAffected) offset += GetTemperatureOffset(position, zoom);
        if (PolAffected) offset += GetPolarityOffset(position, zoom);

        return GetMercatorNoise(position, zoom, offset); ;
    }

    /// <summary>
    /// Calculates the noise offset of a terrain based on the terrain value
    /// If within a range, returns the corresponding value, otherwise perform a linear interpolation
    /// </summary>
    /// <param name="noise"></param>
    /// <param name="lowValue">Offset within the low range</param>
    /// <param name="lowEnd">End of the low range</param>
    /// <param name="midStart">Start of the middle range</param>
    /// <param name="midValue">Offset within the middle range</param>
    /// <param name="midEnd">End of the middle range</param>
    /// <param name="highStart">Start of the high range</param>
    /// <param name="highValue">Offset within the high range</param>
    /// <returns>Offset</returns>
    private static float GetTerrainOffset(float noise, float lowValue, float lowEnd, float midStart, float midValue, float midEnd, float highStart, float highValue)
    {
        if (noise <= lowEnd) return lowValue;
        if (noise < midStart) return Mathf.Lerp(lowValue, midValue, Mathf.InverseLerp(lowEnd, midStart, noise));
        if (noise <= midEnd) return midValue;
        if (noise < highStart) return Mathf.Lerp(midValue, highValue, Mathf.InverseLerp(midEnd, highStart, noise));
        return highValue;
    }

    /// <summary>
    /// Returns the offset based on Temperature noise
    /// </summary>
    /// <param name="position">Mercator position</param>
    /// <param name="zoom">Current zoom level</param>
    /// <returns>Offset</returns>
    private float GetTemperatureOffset(Vector2I position, int zoom)
    {
        float noise = TerrainTemp.GetMercatorNoise(position, zoom, 0);
        return GetTerrainOffset(noise, TempLowValue, TempLowEnd, TempMidStart, TempMidValue, TempMidEnd, TempHighStart, TempHighValue);
    }

    /// <summary>
    /// Returns the offset based on Polarity noise
    /// </summary>
    /// <param name="position">Mercator position</param>
    /// <param name="zoom">Current zoom level</param>
    /// <returns>Offset</returns>
    private float GetPolarityOffset(Vector2I position, int zoom)
    {
        float noise = TerrainPol.GetMercatorNoise(position, zoom, 0);
        return GetTerrainOffset(noise, PolLowValue, PolLowEnd, PolMidStart, PolMidValue, PolMidEnd, PolHighStart, PolHighValue);
    }

    /*
    /// <summary>
    /// Returns the noise value on given coordinates by using a Sinusoidal projection,
    /// </summary>
    /// <param name="latitude">The latitude in Radians. Positive in North</param>
    /// <param name="longitude">The longitude in Radians. Positive is East</param>
    /// <returns>Noise in [0-1] range</returns>
    private float GetSinusoidalNoise(double latitude, double longitude)
    {
        double noiseX = NoiseMapSize * NoiseSinFactor * longitude * Math.Cos(latitude);
        double noiseY = NoiseMapSize * NoiseSinFactor * latitude;

        float noiseRaw = Fnl.GetNoise2D((float)noiseX, (float)noiseY);

        float noise = Mathf.Clamp((noiseRaw - PowerMin) / (PowerMax - PowerMin), 0, 1);

        return noise;
    }

    /// <summary>
    /// Returns the noise value on given coordinates by using a 3D sphere
    /// </summary>
    /// <param name="latitude">The latitude in Radians. Positive in North</param>
    /// <param name="longitude">The longitude in Radians. Positive is East</param>
    /// <returns>Noise in [0-1] range</returns>
    private float GetSphericalNoise(double latitude, double longitude)
    {
        double noiseX = NoiseMapSize * NoiseSphereFactor * Math.Cos(latitude) * Math.Sin(longitude);
        double noiseY = NoiseMapSize * NoiseSphereFactor * Math.Cos(latitude) * Math.Cos(longitude);
        double noiseZ = NoiseMapSize * NoiseSphereFactor * Math.Sin(latitude);

        float noiseRaw = Fnl.GetNoise3D((float)noiseX, (float)noiseY, (float)noiseZ);

        float noise = Mathf.Clamp((noiseRaw - PowerMin) / (PowerMax - PowerMin), 0, 1);

        return noise;
    }
    */
}
