using Godot;
using System;

namespace GPSMining;

public partial class Util : RefCounted
{
    public static readonly Random rand = new();

    public static double Mod(double a, double r)
    {
        if (a < 0)
        {
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
