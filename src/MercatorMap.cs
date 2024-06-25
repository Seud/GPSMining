using Godot;
using System;

namespace GPSMining;

public partial class MercatorMap : RefCounted
{
    public const int TILE_SIZE_POW = 8;
    public const int TILE_SIZE = 1 << TILE_SIZE_POW; // 256
    public const int DEFAULT_ZOOM = 17;
    public const int MAX_ZOOM = 19;

    private int zoom = DEFAULT_ZOOM;
    private Vector2I position = new(GetMaxPosition(DEFAULT_ZOOM) / 2, GetMaxPosition(DEFAULT_ZOOM) / 2);

    public static Vector2I GetPosition(double latitude, double longitude, int zoom)
    {
        Vector2I position = new()
        {
            X = (int)Math.Round((1 + longitude / Math.PI) * GetMaxPosition(zoom) / 2),
            Y = (int)Math.Round((1 - Math.Asinh(Math.Tan(latitude)) / Math.PI) * GetMaxPosition(zoom) / 2)
        };

        return position;
    }

    public static double GetLongitude(Vector2I position, int zoom)
    {
        return Math.PI * (2 * (double)position.X / GetMaxPosition(zoom) - 1);
    }

    public static double GetLatitude(Vector2I position, int zoom)
    {
        return Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (double)position.Y / GetMaxPosition(zoom))));
    }

    public static int GetMaxPosition(int zoom)
    {
        return TILE_SIZE << zoom;
    }

    public static Vector2I Align(Vector2I position, int zoom)
    {
        int max = GetMaxPosition(zoom);
        Vector2I new_position = new()
        {
            X = (int)Util.Mod(position.X, max),
            Y = Math.Clamp(position.Y, 0, max - 1)
        };

        return new_position;
    }

    public Vector2I GetPosition()
    {
        return position;
    }

    public int GetZoom()
    {
        return zoom;
    }

    public void ZoomIn(int levels = 1)
    {
        if(zoom == MAX_ZOOM || levels < 1)
        {
            return;
        }

        position *= 2;
        zoom += 1;

        if(levels > 1)
        {
            ZoomIn(levels - 1);
        }
    }

    public void ZoomOut(int levels = 1)
    {
        if (zoom == 0 || levels < 1)
        {
            return;
        }

        position /= 2;
        zoom -= 1;

        if (levels > 1)
        {
            ZoomOut(levels - 1);
        }
    }

    public void Move(Vector2I move, bool absolute = false)
    {
        if (absolute)
        {
            position = Align(move, zoom);
        }
        else
        {
            position = Align(move + position, zoom);
        }
        
    }
}