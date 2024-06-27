using Godot;
using System;

namespace GPSMining;

/// <summary>
/// A map using a Mercator-style projection of a sphere
/// </summary>
public partial class MercatorMap : RefCounted
{
    /// <summary>
    /// Current zoom level
    /// </summary>
    private int _zoom = Globals.DefaultZoomLevel;

    /// <summary>
    /// Current position on the map
    /// (0,0) is the upper-left corner of the map
    /// </summary>
    private Vector2I _currentPosition = new(GetMaxPosition(Globals.DefaultZoomLevel) / 2, GetMaxPosition(Globals.DefaultZoomLevel) / 2);

    /// <summary>
    /// Returns the position corresponding to a certin latitude and longitude
    /// </summary>
    /// <param name="latitude">Latitude in Radians</param>
    /// <param name="longitude">Longitude in Radians</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Mercator position</returns>
    public static Vector2I GetPosition(double latitude, double longitude, int zoom)
    {
        Vector2I position = new()
        {
            X = (int)Math.Round((1 + longitude / Math.PI) * GetMaxPosition(zoom) / 2),
            Y = (int)Math.Round((1 - Math.Asinh(Math.Tan(latitude)) / Math.PI) * GetMaxPosition(zoom) / 2)
        };

        return position;
    }

    /// <summary>
    /// Returns the longitude of a given Mercator position
    /// </summary>
    /// <param name="position">Position</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Longitude in Radians</returns>
    public static double GetLongitude(Vector2I position, int zoom)
    {
        return Math.PI * (2 * (double)position.X / GetMaxPosition(zoom) - 1);
    }

    /// <summary>
    /// Returns the latitude of a given Mercator position
    /// </summary>
    /// <param name="position">Position</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Latitude in Radians</returns>
    public static double GetLatitude(Vector2I position, int zoom)
    {
        return Math.Atan(Math.Sinh(Math.PI * (1 - 2 * (double)position.Y / GetMaxPosition(zoom))));
    }

    /// <summary>
    /// Returns the exclusive maximum position on a map
    /// Actual maximum is 1 less
    /// </summary>
    /// <param name="zoom"></param>
    /// <returns>Maximum position, exclusive</returns>
    public static int GetMaxPosition(int zoom)
    {
        return Globals.TileSize << zoom;
    }

    /// <summary>
    /// Returns a Mercator position bound to the map limits
    /// Loops around the X asis and clamps the Y axis
    /// </summary>
    /// <param name="position">Position</param>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Mercator position</returns>
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

    /// <summary>
    /// Returns the current Mercator position
    /// </summary>
    /// <returns>Position</returns>
    public Vector2I GetPosition()
    {
        return _currentPosition;
    }

    /// <summary>
    /// Returns the current zoom level
    /// </summary>
    /// <returns>Zoom level</returns>
    public int GetZoom()
    {
        return _zoom;
    }

    /// <summary>
    /// Zooms in, up to the limit
    /// </summary>
    /// <param name="levels">Number of levels to zoom in</param>
    public void ZoomIn(int levels = 1)
    {
        if (_zoom == Globals.MaxZoomLevel || levels < 1)
        {
            return;
        }

        _currentPosition *= 2;
        _zoom += 1;

        if (levels > 1)
        {
            ZoomIn(levels - 1);
        }
    }

    /// <summary>
    /// Zooms out, down to the minimum
    /// </summary>
    /// <param name="levels">Number of levels to zoom out</param>
    public void ZoomOut(int levels = 1)
    {
        if (_zoom == 0 || levels < 1)
        {
            return;
        }

        _currentPosition /= 2;
        _zoom -= 1;

        if (levels > 1)
        {
            ZoomOut(levels - 1);
        }
    }

    /// <summary>
    /// Moves the map according to an offset
    /// </summary>
    /// <param name="move">Direction to move in</param>
    /// <param name="absolute">If true, the offset is treated as the new position</param>
    public void Move(Vector2I move, bool absolute = false)
    {
        if (absolute)
        {
            _currentPosition = Align(move, _zoom);
        }
        else
        {
            _currentPosition = Align(move + _currentPosition, _zoom);
        }

    }
}