using Godot;
using System;

namespace GPSMining;

public partial class NoiseMap : Node2D
{
    private readonly Random r = new();

    public const int TARGET_SIZE = 256;
    public const int TILE_SCALE = 2;
    public const int MOVE = 8;

    private float power_min = 0.25f;
    private float power_max = 0.75f;

    private float coverage_min;
    private float coverage_max;
    private float average;
    private float center_noise;

    private int zoomMod = 0;
    private int makeTimer = 0;
    private int windowTimer = 0;

    private Vector2 center = new();
    private int noise_scale = 4;

    private MercatorMap map = new();
    private NoiseSphere noise_sphere = new();
    private OSMTiles osm_tiles;
    private Sprite2D noise_overlay;

    public int GetArea()
    {
        return (int)center.X * (int)center.Y * 4;
    }

    public void MakeMap()
    {
        if (power_min >= power_max)
        {
            UpdateUI();
            return;
        }

        coverage_min = 0.0001f;
        coverage_max = 0;
        average = 0;

        int zoom = map.GetZoom();
        Vector2I position = map.GetPosition();

        osm_tiles.Move(position, zoom);

        int max_x = (int) Math.Ceiling((double) center.X / noise_scale);
        int max_y = (int) Math.Ceiling((double) center.Y / noise_scale);

        Image noise_image = Image.Create(max_x * 2, max_y * 2, false, Image.Format.Rgba8);

        for (int x = -max_x; x < max_x; x++)
        {
            for (int y = -max_y; y < max_y; y++)
            {
                Vector2I offset = new Vector2I(x, y) * noise_scale / TILE_SCALE;
                Vector2I position_adj = map.GetPosition() + offset;

                if (position_adj.Y < 0 || position_adj.Y > MercatorMap.GetMaxPosition(zoom))
                {
                    continue;
                }

                double longitude_adj = MercatorMap.GetLongitude(position_adj, zoom);
                double latitude_adj = MercatorMap.GetLatitude(position_adj, zoom);

                float noise_raw = noise_sphere.GetNoiseSphere(latitude_adj, longitude_adj);

                float noise = Mathf.Clamp((noise_raw - power_min) / (power_max - power_min), 0, 1);
                if (x == 0 && y == 0)
                {
                    center_noise = noise;
                }

                if (noise >= 0) coverage_min += 1;
                if (noise >= 1) coverage_max += 1;
                average += Mathf.Clamp(noise, 0, 1);

                if (noise > 0)
                {
                    noise_image.SetPixel(x + max_x, y + max_y, Color.FromHsv((1 - noise) * 2 / 3, 0.8f + 0.2f * noise, 1));
                }
            }
        }

        noise_overlay.Texture = ImageTexture.CreateFromImage(noise_image);

        UpdateUI();
    }

    public void UpdateUI()
    {
        int zoom = map.GetZoom();
        Vector2I position = map.GetPosition();
        Vector2I tile = OSMTiles.GetTile(position);
        Vector2I tile_local_position = OSMTiles.GetLocalPosition(position);

        String text = $"" +
            $"Zoom Level : ({zoom})\n" +
            $"Position : ({position.X}, {position.Y})\n" +
            $"Noise here : {Math.Round(100 * center_noise, 4)} %\n" +
            $"Tile : ({tile.X}, {tile.Y}), ({tile_local_position.X}, {tile_local_position.Y})\n" +
            $"Latitude : {Math.Round(MercatorMap.GetLatitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Longitude : {Math.Round(MercatorMap.GetLongitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Coverage (Min) : {Math.Round(100 * coverage_min / GetArea(), 4)} %\n" +
            $"Coverage (Max) : {Math.Round(100 * coverage_max / GetArea(), 4)} %\n" +
            $"Average : {Math.Round(100 * average / coverage_min, 4)} %\n";

        RichTextLabel map_ui_text = GetNode<RichTextLabel>("%UIText");
        map_ui_text.Text = text;
    }

    public void WindowResized()
    {
        windowTimer = 12;
    }

    public void ResizeMap()
    {
        Vector2 viewport_size = GetViewportRect().End;

        center = viewport_size / 2;
        noise_scale = (int)Math.Ceiling(Math.Sqrt(center.X * center.Y) / TARGET_SIZE);
        noise_overlay.Scale = new(noise_scale, noise_scale);

        GD.Print();

        osm_tiles.Position = center;
        osm_tiles.Scale = new(TILE_SCALE, TILE_SCALE);

        osm_tiles.UpdateSize(viewport_size);
        MakeMap();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        noise_overlay = GetNode<Sprite2D>("%NoiseOverlay");
        osm_tiles = GetNode<OSMTiles>("%OSMTiles");
        GetTree().Root.SizeChanged += () => WindowResized();
        ResizeMap();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventScreenDrag eventDrag)
        {
            Camera2D cam = GetNode<Camera2D>("%Camera");
            cam.Offset -= eventDrag.Relative;
            makeTimer = 12;
        }
        if (@event is InputEventMouseButton eventMouse)
        {
            if (eventMouse.Pressed && eventMouse.ButtonIndex == MouseButton.WheelUp)
            {
                zoomMod++;
            }
            else
            if (eventMouse.Pressed && eventMouse.ButtonIndex == MouseButton.WheelDown)
            {
                zoomMod--;
            }
        }
    }

    public override void _Process(double delta)
    {
        bool make = false;
        bool moving = false;
        Vector2I move = new();

        if (makeTimer > 0 && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            makeTimer--;
            if (makeTimer == 0)
            {
                Camera2D cam = GetNode<Camera2D>("%Camera");
                Vector2I offset = (Vector2I) cam.Offset / TILE_SCALE;
                cam.Offset = new(0, 0);
                move += offset;
                moving = true;
            }
        }

        if (windowTimer > 0)
        {
            windowTimer--;
            if (windowTimer == 0)
            {
                ResizeMap();
            }
        }

        if (zoomMod > 0)
        {
            map.ZoomIn(zoomMod);
            zoomMod = 0;
            make = true;
        }
        if (zoomMod < 0)
        {
            map.ZoomOut(-zoomMod);
            zoomMod = 0;
            make = true;
        }

        if (Input.IsActionJustPressed("ui_accept"))
        {
            noise_sphere.fnl.Seed = (int)Math.Floor(r.NextSingle() * 1000000);
            make = true;
        }

        if (Input.IsActionJustPressed("ui_text_delete"))
        {
            osm_tiles.CleanTiles();
            make = true;
        }

        if (Input.IsActionJustPressed("ui_left"))
        {
            move.X -= MOVE;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_right"))
        {
            move.X += MOVE;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_up"))
        {
            move.Y -= MOVE;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_down"))
        {
            move.Y += MOVE;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_page_up"))
        {
            map.ZoomIn();
            make = true;
        }

        if (Input.IsActionJustPressed("ui_page_down"))
        {
            map.ZoomOut();
            make = true;
        }

        if (moving)
        {
            map.Move(move);
            make = true;
        }

        if (make) MakeMap();
    }
}