using Godot;
using System;
using System.IO;

namespace GPSMining;

[GlobalClass]
public partial class TileDownloader : Node
{
    [Signal]
    public delegate void TextureReadyEventHandler(Texture2D tex, int x, int y, int zoom);

    public const String TILE_CACHE_DIR = "user://tile_cache";
    public const int TILE_CACHE_TIME = 86400 * 7;
    public const String TILE_FORMAT = "webp";
    public const String HTTP_SOURCE = "https://tile.openstreetmap.org/{0}/{1}/{2}.png";

    public static readonly Texture2D failed_tile_tex = GD.Load<Texture2D>("res://assets/default_failed.png");
    public static readonly Texture2D default_tile_tex = GD.Load<Texture2D>("res://assets/default_loading.png");

    public static String GetTileID(int x, int y, int zoom)
    {
        return $"z{zoom}x{x}y{y}";
    }

    private void UpdateTile(int fake_x, int x, int y, int zoom)
    {
        String tile_id = GetTileID(x, y, zoom);

        DirAccess da = DirAccess.Open(TILE_CACHE_DIR);
        da.Remove($"{tile_id}.{TILE_FORMAT}");
        Callable.From(() => RequestTexture(fake_x, y, zoom)).CallDeferred();
    }

    private void LoadTextureFromDisk(int fake_x, int x, int y, int zoom, String tile_path)
    {
        Image tile_image = Image.LoadFromFile(tile_path);
        if (tile_image == null)
        {
            GD.PushError($"File {tile_path} is corrupted, reloading");
            UpdateTile(fake_x, x, y, zoom);
            return;
        }
        Texture2D tile_tex = ImageTexture.CreateFromImage(tile_image);

        Callable.From(() => EmitSignal(SignalName.TextureReady, tile_tex, fake_x, y, zoom)).CallDeferred();
    }

    private void DownloadTexture(int fake_x, int x, int y, int zoom, String tile_path)
    {
        HttpRequest request = new();
        AddChild(request);
        request.RequestCompleted += (long result, long responseCode, string[] headers, byte[] body) => OnRequestCompleted(result, body, fake_x, x, y, zoom, tile_path, request);

        String request_url = String.Format(HTTP_SOURCE, zoom, x, y);
        Error error = request.Request(request_url, new string[] { "User-Agent: GPSMining/0.1 (Godot) https://github.com/Seud/GPSMining" });
        if (error != Error.Ok)
        {
            GD.PushError($"Request {request_url} failed !");
            EmitSignal(SignalName.TextureReady, failed_tile_tex, fake_x, y, zoom);
            return;
        }
    }

    public void RequestTexture(int fake_x, int y, int zoom)
    {
        int x = (int) Util.Mod(fake_x, MercatorMap.GetMaxPosition(zoom) / MercatorMap.TILE_SIZE);

        //GD.Print($"Magic ! {fake_x} is now {x} !");
        //return;

        String tile_id = GetTileID(x, y, zoom);
        String tile_path = $"{TILE_CACHE_DIR}/{tile_id}.{TILE_FORMAT}";

        if (!Godot.FileAccess.FileExists(tile_path))
        {
            DownloadTexture(fake_x, x, y, zoom, tile_path);
        }
        else
        {
            ulong mtime = Godot.FileAccess.GetModifiedTime(tile_path);
            double curtime = Time.GetUnixTimeFromSystem();
            int time_diff = (int) (Math.Floor(curtime) - mtime);

            if (time_diff > TILE_CACHE_TIME)
            {
                GD.Print($"File {tile_id} is old, refreshing");
                UpdateTile(fake_x, x, y, zoom);
            } else
            {
                WorkerThreadPool.AddTask(Callable.From(() => LoadTextureFromDisk(fake_x, x, y, zoom, tile_path)));
            }
        }

    }

    private void OnRequestCompleted(long result, byte[] body, int fake_x, int x, int y, int zoom, String tile_path, HttpRequest request)
    {
        request.QueueFree();

        if(result != (long)HttpRequest.Result.Success)
        {
            GD.PushError($"Request for {zoom},{x},{y} is {result} !");
            EmitSignal(SignalName.TextureReady, failed_tile_tex, fake_x, y, zoom);
            return;
        }

        Image tmp_tile_image = new();
        Error error = tmp_tile_image.LoadPngFromBuffer(body);

        if (error != Error.Ok)
        {
            GD.PushError($"Loading image for {zoom},{x},{y} is {error} !");
            EmitSignal(SignalName.TextureReady, failed_tile_tex, fake_x, y, zoom);
            return;
        }

        tmp_tile_image.SaveWebp($"{tile_path}");

        WorkerThreadPool.AddTask(Callable.From(() => LoadTextureFromDisk(fake_x, x, y, zoom, tile_path)));
    }

    public override void _Ready()
    {
        DirAccess.MakeDirAbsolute(TILE_CACHE_DIR);
    }

    public override void _Process(double delta)
    {

    }

}
