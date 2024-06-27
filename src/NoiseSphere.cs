using Godot;
using System;

namespace GPSMining;

/// <summary>
/// A 3D noise generator in a sphere
/// </summary>
public partial class NoiseSphere : RefCounted
{
    /// <summary>
    /// The radius of the noise sphere
    /// </summary>
    private const double NoiseSphereRadius = 10000;

    /// <summary>
    /// The frequency of noise. Higher value means peaks and valleys are closer together
    /// </summary>
    private const float Frequency = 1f;
    /// <summary>
    /// The type of noise
    /// </summary>
    private const FastNoiseLite.NoiseTypeEnum NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
    /// <summary>
    /// The type of fractal noise modification
    /// </summary>
    private const FastNoiseLite.FractalTypeEnum FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
    /// <summary>
    /// The seed of the generator
    /// </summary>
    private int Seed = 0;

    /// <summary>
    /// The FastNoiseLite instance
    /// </summary>
    public FastNoiseLite fnl;

    public NoiseSphere()
    {
        fnl = new()
        {
            NoiseType = NoiseType,
            FractalType = FractalType,
            Seed = Seed,
            Frequency = Frequency
        };
    }

    /// <summary>
    /// Returns the noise value on given coordinates
    /// </summary>
    /// <param name="latitude">The latitude in Radians. Positive in North</param>
    /// <param name="longitude">The longitude in Radians. Positive is East</param>
    /// <returns></returns>
    public float GetNoiseSphere(double latitude, double longitude)
    {
        double noiseX = NoiseSphereRadius * Math.Cos(latitude) * Math.Sin(longitude);
        double noiseY = NoiseSphereRadius * Math.Cos(latitude) * Math.Cos(longitude);
        double noiseZ = NoiseSphereRadius * Math.Sin(latitude);

        float noise = fnl.GetNoise3D((float)noiseX, (float)noiseY, (float)noiseZ);

        return noise;
    }
}
