using Godot;
using System;

namespace GPSMining;

public partial class NoiseMap : Node2D
{
    private static readonly Random r = new();

    public const int TARGET_SIZE = 256;
    public const int MOVE = 8;

    private int x_max = 128;
    private int y_max = 128;
    private int map_scale = 4;

    private float power_min = 0.25f;
    private float power_max = 0.75f;

    private float coverage_min;
    private float coverage_max;
    private float average;

    private int zoomMod = 0;
    private int makeTimer = 0;
    private int windowTimer = 0;

    private Vector2 center = new();

    private MercatorMap map = new();
    private NoiseSphere noise_sphere = new();
    private OSMTiles osm_tiles;
    private TileMap tile_map;

    public int GetArea()
    {
        return x_max * y_max * 4;
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

        for (int x = -x_max; x < x_max; x++)
        {
            for (int y = -y_max; y < y_max; y++)
            {
                Vector2I offset = new(x, y);
                Vector2I position_adj = map.GetPosition() + offset;

                if (position_adj.Y < 0 || position_adj.Y > MercatorMap.GetMaxPosition(zoom))
                {
                    tile_map.SetCell(-1, new Vector2I(x, y));
                    continue;
                }

                double longitude_adj = MercatorMap.GetLongitude(position_adj, zoom);
                double latitude_adj = MercatorMap.GetLatitude(position_adj, zoom);

                float noise = noise_sphere.GetNoiseSphere(latitude_adj, longitude_adj);

                float noise_mod = (noise - power_min) / (power_max - power_min);

                if (noise_mod >= 0) coverage_min += 1;
                if (noise_mod >= 1) coverage_max += 1;
                average += Mathf.Clamp(noise_mod, 0, 1);

                int coord = (int)Math.Clamp(Mathf.Ceil(noise_mod * 10), 0, 11);

                tile_map.SetCell(0, new Vector2I(x, y), 0, new Vector2I(coord, 0));
            }
        }

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
            $"Tile : ({tile.X}, {tile.Y}), ({tile_local_position.X}, {tile_local_position.Y})\n" +
            $"Latitude : {Math.Round(MercatorMap.GetLatitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Longitude : {Math.Round(MercatorMap.GetLongitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Coverage (Min) : {Math.Round(100 * coverage_min / GetArea(), 4)} %\n" +
            $"Coverage (Max) : {Math.Round(100 * coverage_max / GetArea(), 4)} %\n" +
            $"Average : {Math.Round(100 * average / coverage_min, 4)}";

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

        map_scale = (int)Math.Floor(Math.Sqrt(viewport_size.X * viewport_size.Y)) / TARGET_SIZE;
        x_max = (int)Mathf.Ceil(viewport_size.X / (2 * map_scale));
        y_max = (int)Mathf.Ceil(viewport_size.Y / (2 * map_scale));
        center = viewport_size / 2;

        GD.Print($"DIM : {viewport_size.X}, {viewport_size.Y} - SIZ {x_max}, {y_max} - SCALE {map_scale}");

        tile_map.Clear();
        tile_map.Position = center;
        tile_map.Scale = new(map_scale, map_scale);

        osm_tiles.UpdateSize(viewport_size, map_scale);
        MakeMap();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        tile_map = GetNode<TileMap>("%Map");
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
                Vector2 offset = cam.Offset;
                cam.Offset = new(0, 0);
                move += (Vector2I)offset / map_scale;
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
            noise_sphere.fnl.Seed = (int)Math.Floor(r.NextDouble() * 1000000);
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