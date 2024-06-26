using Godot;
using System;

namespace GPSMining;

public partial class NoiseSphere : RefCounted
{
    public const double NOISE_SPHERE_RADIUS = 10000;
    public const float FREQUENCY = 1f;
    public const FastNoiseLite.NoiseTypeEnum NOISE_TYPE = FastNoiseLite.NoiseTypeEnum.Simplex;
    public const FastNoiseLite.FractalTypeEnum FRACTAL_TYPE = FastNoiseLite.FractalTypeEnum.Fbm;

    public int seed = 0;

    public FastNoiseLite fnl;

    public NoiseSphere()
    {
        fnl = new()
        {
            NoiseType = NOISE_TYPE,
            FractalType = FRACTAL_TYPE,
            Seed = seed,
            Frequency = FREQUENCY
        };
    }

    public float GetNoiseSphere(double latitude, double longitude)
    {
        double noise_x = NOISE_SPHERE_RADIUS * Math.Cos(latitude) * Math.Sin(longitude);
        double noise_y = NOISE_SPHERE_RADIUS * Math.Cos(latitude) * Math.Cos(longitude);
        double noise_z = NOISE_SPHERE_RADIUS * Math.Sin(latitude);

        float noise = fnl.GetNoise3D((float)noise_x, (float)noise_y, (float)noise_z);

        return noise;
    }
}
