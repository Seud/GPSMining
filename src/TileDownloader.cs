using Godot;
using System;

namespace GPSMining;

/// <summary>
/// A Node that can request OpenStreetMaps raster tiles
/// Stores the downloaded tiles on disk to minimize network usage
/// </summary>
[GlobalClass]
public partial class TileDownloader : Node
{
    /// <summary>
    /// Sent when a Tile texture is ready for display
    /// </summary>
    /// <param name="texture">The texture</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    [Signal]
    public delegate void TextureReadyEventHandler(Texture2D texture, int x, int y, int zoom);

    /// <summary>
    /// Extension of image files
    /// WebP compression is more efficient than PNG and lossless
    /// </summary>
    public const string TileFormat = "webp";

    /// <summary>
    /// Texture to use for tiles that failed to load
    /// </summary>
    public static readonly Texture2D FailedTileTex = GD.Load<Texture2D>(Globals.FailedTileTexPath);
    /// <summary>
    /// Texture to use for tiles being loaded
    /// </summary>
    public static readonly Texture2D LoadingFileTex = GD.Load<Texture2D>(Globals.LoadingTileTexPath);

    /// <summary>
    /// Gets a string representation of a tile identifier
    /// Used to generate a file name
    /// </summary>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    /// <returns>Tile ID</returns>
    public static string GetTileID(int x, int y, int zoom)
    {
        return $"z{zoom}x{x}y{y}";
    }

    /// <summary>
    /// Re-downloads an existing tile
    /// Used to fix corruption or refresh an old cache
    /// </summary>
    /// <param name="xFake">Tile X coordinate (Displayed)</param>
    /// <param name="x">Tile X coordinate (Actual)</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    private void UpdateTile(int xFake, int x, int y, int zoom)
    {
        string tileID = GetTileID(x, y, zoom);

        DirAccess da = DirAccess.Open(Globals.TileCacheDir);
        da.Remove($"{tileID}.{TileFormat}");

        Callable.From(() => RequestTexture(xFake, y, zoom)).CallDeferred();
    }

    /// <summary>
    /// Loads an existing tile from disk then updates the corresponding tile
    /// </summary>
    /// <param name="xFake">Tile X coordinate (Displayed)</param>
    /// <param name="x">Tile X coordinate (Actual)</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    /// <param name="tilePath">Path to the file</param>
    private void LoadTextureFromDisk(int xFake, int x, int y, int zoom, string tilePath)
    {
        Image tileImage = Image.LoadFromFile(tilePath);
        if (tileImage == null)
        {
            GD.PushError($"File {tilePath} is corrupted, reloading");
            UpdateTile(xFake, x, y, zoom);
            return;
        }

        Texture2D tileTexture = ImageTexture.CreateFromImage(tileImage);
        Callable.From(() => EmitSignal(SignalName.TextureReady, tileTexture, xFake, y, zoom)).CallDeferred();
    }

    /// <summary>
    /// Downloads a tile and saves it to the disk
    /// </summary>
    /// <param name="xFake">Tile X coordinate (Displayed)</param>
    /// <param name="x">Tile X coordinate (Actual)</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    /// <param name="tilePath">Path to the file</param>
    private void DownloadTexture(int xFake, int x, int y, int zoom, string tilePath)
    {
        HttpRequest request = new();
        AddChild(request);
        request.RequestCompleted += (long result, long responseCode, string[] headers, byte[] body) => OnRequestCompleted(result, body, xFake, x, y, zoom, tilePath, request);

        string requestUrl = string.Format(Globals.HttpUri, zoom, x, y);
        Error error = request.Request(requestUrl, new string[] { Globals.UserAgent });
        if (error != Error.Ok)
        {
            GD.PushError($"Request {requestUrl} failed !");
            EmitSignal(SignalName.TextureReady, FailedTileTex, xFake, y, zoom);
            return;
        }
    }

    /// <summary>
    /// Requests a tile image to be loaded or downloaded
    /// Fake coordinates are used for tiles that are outside of the map to permit looping around
    /// </summary>
    /// <param name="xFake">Tile X coordinate (Displayed)</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    public void RequestTexture(int xFake, int y, int zoom)
    {
        int x = (int)Util.Mod(xFake, MercatorMap.GetMaxPosition(zoom) / Globals.TileSize);

        string tileID = GetTileID(x, y, zoom);
        string tilePath = $"{Globals.TileCacheDir}/{tileID}.{TileFormat}";

        if (!Godot.FileAccess.FileExists(tilePath))
        {
            DownloadTexture(xFake, x, y, zoom, tilePath);
        }
        else
        {
            // Check if the tile needs a refresh
            ulong mTime = Godot.FileAccess.GetModifiedTime(tilePath);
            double currentTime = Time.GetUnixTimeFromSystem();
            int timeDifference = (int)(Math.Floor(currentTime) - mTime);

            if (timeDifference > Globals.TileCacheTime)
            {
                GD.Print($"File {tileID} is old, refreshing");
                UpdateTile(xFake, x, y, zoom);
            }
            else
            {
                WorkerThreadPool.AddTask(Callable.From(() => LoadTextureFromDisk(xFake, x, y, zoom, tilePath)));
            }
        }

    }

    /// <summary>
    /// Executed when a tile has finished downloading
    /// Checks if the download was successful and saves the image on disk
    /// </summary>
    /// <param name="result">Result of the request</param>
    /// <param name="body">Contents of the response</param>
    /// <param name="xFake">Tile X coordinate (Displayed)</param>
    /// <param name="x">Tile X coordinate (Actual)</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    /// <param name="tilePath">Path to the file</param>
    /// <param name="request">Request node, freed once the request is over</param>
    private void OnRequestCompleted(long result, byte[] body, int xFake, int x, int y, int zoom, String tilePath, HttpRequest request)
    {
        request.QueueFree();

        if (result != (long)HttpRequest.Result.Success)
        {
            GD.PushError($"Request for {zoom},{x},{y} is {result} !");
            EmitSignal(SignalName.TextureReady, FailedTileTex, xFake, y, zoom);
            return;
        }

        Image downloadedTileImage = new();
        Error error = downloadedTileImage.LoadPngFromBuffer(body);

        if (error != Error.Ok)
        {
            GD.PushError($"Loading image for {zoom},{x},{y} is {error} !");
            EmitSignal(SignalName.TextureReady, FailedTileTex, xFake, y, zoom);
            return;
        }

        downloadedTileImage.SaveWebp($"{tilePath}");

        WorkerThreadPool.AddTask(Callable.From(() => LoadTextureFromDisk(xFake, x, y, zoom, tilePath)));
    }

    public override void _Ready()
    {
        DirAccess.MakeDirAbsolute(Globals.TileCacheDir);
    }

}
