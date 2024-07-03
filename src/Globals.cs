using Godot;
using System;

namespace GPSMining;

/// <summary>
/// A Godot Singleton containing global parameters (Especially constants)
/// </summary>
public partial class Globals : Node
{
    public const string TileCacheDir = "user://tile_cache";
    public const int TileCacheTime = 86400 * 7;
    public const string HttpUri = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";

    public const string FailedTileTexPath = "res://assets/default_failed.png";
    public const string LoadingTileTexPath = "res://assets/default_loading.png";

    public const string UserAgent = "User-Agent: GPSMining/0.1 (Godot) https://github.com/Seud/GPSMining";

    public const int TileSize = 256;
    public const int CleanDistanceFactor = 2;
    public const int RenderedTilesMax = 1000;

    public const int DefaultZoomLevel = 17;
    public const int MaxZoomLevel = 19;

    public const int TargetSize = 256;
    public const int TileScale = 2;

    public const int EarthCircumferenceKm = 40075;

    public const double DefaultLatitude = Math.PI / 180 * 43.70;
    public const double DefaultLongitude = Math.PI / 180 * 07.26;
    public static readonly double DefaultScale = Math.Cos(DefaultLatitude);
}
