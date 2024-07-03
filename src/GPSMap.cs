using Godot;
using System;

namespace GPSMining;

/// <summary>
/// Main node
/// </summary>
public partial class GPSMap : Node2D
{
    /// <summary>
    /// Distance to move when using arrows in the editor.
    /// </summary>
    public const int MoveDistance = 8;

    private float _statCoverageMin;
    private float _statCoverageMax;
    private float _statAverage;
    private float _statCurrentNoise;
    private float _statArea;

    private int _zoomMod = 0;
    private int _makeTimer = 0;
    private int _windowTimer = 0;

    /// <summary>
    /// Coordinates of the center
    /// </summary>
    private Vector2 _center = new();
    /// <summary>
    /// Scale of the noise map
    /// </summary>
    private int _noiseScale = 4;

    private MercatorMap _map = new();
    private NoiseMap _noiseSphere;
    private OSMTiles _osmTiles;

    /// <summary>
    /// Node rendering the noise map
    /// </summary>
    private Sprite2D _noiseOverlay;

    /// <summary>
    /// Renders the map around the current position
    /// </summary>
    public void MakeMap()
    {
        _statCoverageMin = 0.0001f;
        _statCoverageMax = 0;
        _statAverage = 0;

        int zoom = _map.GetZoom();
        Vector2I position = _map.GetPosition();

        _osmTiles.Move(position, zoom);

        int maxX = (int)Math.Ceiling((double)_center.X / _noiseScale);
        int maxY = (int)Math.Ceiling((double)_center.Y / _noiseScale);
        int maxPosition = MercatorMap.GetMaxPosition(zoom);

        _statArea = 4 * maxX * maxY;

        Image noiseImage = Image.Create(maxX * 2, maxY * 2, false, Image.Format.Rgba8);

        for (int x = -maxX; x < maxX; x++)
        {
            for (int y = -maxY; y < maxY; y++)
            {
                Vector2I offset = new Vector2I(x, y) * _noiseScale / Globals.TileScale;
                Vector2I positionAdjusted = _map.GetPosition() + offset;

                if (positionAdjusted.Y < 0 || positionAdjusted.Y > maxPosition)
                {
                    continue;
                }

                positionAdjusted = MercatorMap.Align(positionAdjusted, zoom);
                //double longitudeAdjusted = MercatorMap.GetLongitude(positionAdjusted, zoom);
                //double latitudeAdjusted = MercatorMap.GetLatitude(positionAdjusted, zoom);

                float noise = _noiseSphere.GetNoise(positionAdjusted, zoom);
                
                if (x == 0 && y == 0)
                {
                    _statCurrentNoise = noise;
                }

                if (noise > 0) _statCoverageMin += 1;
                if (noise == 1) _statCoverageMax += 1;
                _statAverage += Mathf.Clamp(noise, 0, 1);

                if (noise > 0)
                {
                    noiseImage.SetPixel(x + maxX, y + maxY, NoiseMap.GetColor(noise));
                }
            }
        }

        _noiseOverlay.Texture = ImageTexture.CreateFromImage(noiseImage);

        UpdateUI();
    }

    public void UpdateUI()
    {
        int zoom = _map.GetZoom();
        Vector2I position = _map.GetPosition();

        String text = $"" +
            $"Zoom Level : ({zoom})\n" +
            $"Position : ({position.X}, {position.Y})\n" +
            $"Noise here : {Math.Round(100 * _statCurrentNoise, 4)} %\n" +
            $"Latitude : {Math.Round(MercatorMap.GetLatitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Longitude : {Math.Round(MercatorMap.GetLongitude(position, zoom) * 180 / Math.PI, 4)} °\n" +
            $"Coverage (Min) : {Math.Round(100 * _statCoverageMin / _statArea, 4)} %\n" +
            $"Coverage (Max) : {Math.Round(100 * _statCoverageMax / _statArea, 4)} %\n" +
            $"Average : {Math.Round(100 * _statAverage / _statCoverageMin, 4)} %\n";

        RichTextLabel map_ui_text = GetNode<RichTextLabel>("%UIText");
        map_ui_text.Text = text;
    }

    /// <summary>
    /// Triggers when the window is resized
    /// </summary>
    public void WindowResized()
    {
        _windowTimer = 12;
    }

    /// <summary>
    /// Resize the map according to the new viewport size
    /// </summary>
    public void ResizeMap()
    {
        Vector2 viewport_size = GetViewportRect().End;

        _center = viewport_size / 2;
        _noiseScale = (int)Math.Ceiling(Math.Sqrt(_center.X * _center.Y) / Globals.TargetSize);
        _noiseOverlay.Scale = new(_noiseScale, _noiseScale);

        GD.Print();

        _osmTiles.Position = _center;
        _osmTiles.Scale = new(Globals.TileScale, Globals.TileScale);

        _osmTiles.UpdateSize(viewport_size);
        MakeMap();
    }

    public override void _Ready()
    {
        _noiseSphere = GD.Load<NoiseMap>("res://src/noises/TestSphere.tres");
        _noiseOverlay = GetNode<Sprite2D>("%NoiseOverlay");
        _osmTiles = GetNode<OSMTiles>("%OSMTiles");
        GetTree().Root.SizeChanged += () => WindowResized();
        ResizeMap();
        _map.Move(MercatorMap.GetPosition(Globals.DefaultLatitude, Globals.DefaultLongitude, Globals.DefaultZoomLevel), true);
        MakeMap();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventScreenDrag eventDrag)
        {
            Camera2D cam = GetNode<Camera2D>("%Camera");
            cam.Offset -= eventDrag.Relative;
            _makeTimer = 12;
        }
        if (@event is InputEventMouseButton eventMouse)
        {
            if (eventMouse.Pressed && eventMouse.ButtonIndex == MouseButton.WheelUp)
            {
                _zoomMod++;
            }
            else
            if (eventMouse.Pressed && eventMouse.ButtonIndex == MouseButton.WheelDown)
            {
                _zoomMod--;
            }
        }
    }

    public override void _Process(double delta)
    {
        bool make = false;
        bool moving = false;
        Vector2I move = new();

        if (_makeTimer > 0 && !Input.IsMouseButtonPressed(MouseButton.Left))
        {
            _makeTimer--;
            if (_makeTimer == 0)
            {
                Camera2D cam = GetNode<Camera2D>("%Camera");
                Vector2I offset = (Vector2I)cam.Offset / Globals.TileScale;
                cam.Offset = new(0, 0);
                move += offset;
                moving = true;
            }
        }

        if (_windowTimer > 0)
        {
            _windowTimer--;
            if (_windowTimer == 0)
            {
                ResizeMap();
            }
        }

        if (_zoomMod > 0)
        {
            _map.ZoomIn(_zoomMod);
            _zoomMod = 0;
            make = true;
        }
        if (_zoomMod < 0)
        {
            _map.ZoomOut(-_zoomMod);
            _zoomMod = 0;
            make = true;
        }

        if (Input.IsActionJustPressed("ui_accept"))
        {
            _noiseSphere.Fnl.Seed = (int)Math.Floor(Util.Rand.NextSingle() * 1000000);
            make = true;
        }

        if (Input.IsActionJustPressed("ui_text_delete"))
        {
            _osmTiles.CleanTiles();
            make = true;
        }

        if (Input.IsActionJustPressed("ui_left"))
        {
            move.X -= MoveDistance;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_right"))
        {
            move.X += MoveDistance;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_up"))
        {
            move.Y -= MoveDistance;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_down"))
        {
            move.Y += MoveDistance;
            moving = true;
        }

        if (Input.IsActionJustPressed("ui_page_up"))
        {
            _map.ZoomIn();
            make = true;
        }

        if (Input.IsActionJustPressed("ui_page_down"))
        {
            _map.ZoomOut();
            make = true;
        }

        if (moving)
        {
            _map.Move(move);
            make = true;
        }

        if (make) MakeMap();
    }
}