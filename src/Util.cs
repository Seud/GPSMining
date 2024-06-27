using Godot;
using System;

namespace GPSMining;

/// <summary>
/// A class with many static elements for some generic useful functions
/// </summary>
public partial class Util : RefCounted
{
    /// <summary>
    /// Public random generator
    /// </summary>
    public static readonly Random Rand = new();

    /// <summary>
    /// Returns the positive modulo of a number
    /// </summary>
    /// <param name="a">The number</param>
    /// <param name="r">The base</param>
    /// <returns></returns>
    public static double Mod(double a, double r)
    {
        if (a < 0)
        {
            // Shifts negative modulos to the equivalent positive
            // Ex : -3 % 5 becomes 2
            double mod = a % r;
            if (mod == 0)
                return 0;
            else
                return r + mod;
        }
        else
        {
            return a % r;
        }
    }
}
