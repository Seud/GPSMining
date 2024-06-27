using Godot;
using Godot.Collections;
using System;

namespace GPSMining;

/// <summary>
/// A 2D node that renders a map using OpenStreetMap raster tiles
/// </summary>
[GlobalClass]
public partial class OSMTiles : Node2D
{
    /// <summary>
    /// Amount of tiles to display around the origin (Horizontal)
    /// </summary>
    private int _tileAmountX = 3;
    /// <summary>
    /// Amount of tiles to display around the origin (Vertical)
    /// </summary>
    private int _timeAmountY = 3;

    /// <summary>
    /// Current zoom level
    /// </summary>
    private int _mapZoom = 0;
    /// <summary>
    /// Current map position (absolute, Mercator)
    /// </summary>
    private Vector2I _mapPosition = new();
    /// <summary>
    /// Current map tile
    /// </summary>
    private Vector2I _mapTile = new();

    /// <summary>
    /// Current count of rendered tiles
    /// </summary>
    private int _renderCount = 0;

    /// <summary>
    /// Local TileDownloader
    /// </summary>
    private TileDownloader _tileDownloader;

    /// <summary>
    /// Dictionary nest of individual tile Nodes
    /// Uses 2 nested levels. Root is Zoom, Level 1 is X, Level 2 is Y.
    /// </summary>
    private Dictionary<int, Dictionary<int, Dictionary<int, Sprite2D>>> tiles = new();

    /// <summary>
    /// Returns the tile corresponding to a given Mercator position
    /// Note : Does not ensure that the tile is actually within the map's bounds.
    /// </summary>
    /// <param name="position">Mercator position</param>
    /// <returns>Tile coordinates</returns>
    public static Vector2I GetTile(Vector2I position)
    {
        return position / Globals.TileSize;
    }

    /// <summary>
    /// Returns the Mercator position of the upper left corner of a given tile
    /// </summary>
    /// <param name="tile">Tile coordinates</param>
    /// <returns>Mercator position</returns>
    public static Vector2I GetTilePosition(Vector2I tile)
    {
        return tile * Globals.TileSize;
    }

    /// <summary>
    /// Returns the total amount of tiles along a coordinate for a given zoom level
    /// Max tile ID is one lower since the first tile is ID 0
    /// </summary>
    /// <param name="zoom">Zoom level</param>
    /// <returns>Tile count</returns>
    public static int GetMaxTile(int zoom)
    {
        return 1 << zoom;
    }

    /// <summary>
    /// Updates the size of the map grid based on the Viewport size
    /// Ensures that the entire screen is rendered
    /// </summary>
    /// <param name="size">The size of the Viewport</param>
    public void UpdateSize(Vector2 size)
    {
        _tileAmountX = (int)Mathf.Ceil(size.X / (2 * Globals.TileSize * Globals.TileScale));
        _timeAmountY = (int)Mathf.Ceil(size.Y / (2 * Globals.TileSize * Globals.TileScale));
    }

    /// <summary>
    /// Updates local position according to a new Mercator position (Absolute)
    /// </summary>
    /// <param name="position">Position</param>
    /// <param name="zoom">Zoom level</param>
    public void Move(Vector2I position, int zoom)
    {
        _mapPosition = position;
        _mapZoom = zoom;
        _mapTile = GetTile(_mapPosition);
        RenderTiles();
    }

    /// <summary>
    /// Renders all currently visible tiles
    /// Requests missing tiles and cleans up far-away or excessive tiles
    /// </summary>
    public void RenderTiles()
    {
        if (!tiles.ContainsKey(_mapZoom))
        {
            tiles[_mapZoom] = new();
        }

        // Render any missing tiles
        for (int x = -_tileAmountX; x <= _tileAmountX; x++)
        {
            int targetX = _mapTile.X + x;

            if (!tiles[_mapZoom].ContainsKey(targetX))
            {
                tiles[_mapZoom][targetX] = new();
            }

            for (int y = -_timeAmountY; y <= _timeAmountY; y++)
            {
                int targetY = _mapTile.Y + y;

                if (targetY < 0 || targetY >= GetMaxTile(_mapZoom))
                    continue;

                if (!tiles[_mapZoom][targetX].ContainsKey(targetY))
                {
                    RenderTile(targetX, targetY, _mapZoom);
                }

            }
        }

        // If too many tiles are rendered, cull some of them
        if (_renderCount > Globals.RenderedTilesMax)
        {
            CleanTiles();
        }

        // Update all tiles position and visibility
        foreach (var (tile_zoom, x_dict) in tiles)
        {
            foreach (var (tile_x, y_dict) in x_dict)
            {
                foreach (var (tile_y, tile_node) in y_dict)
                {
                    if (tile_zoom == _mapZoom)
                    {
                        Vector2I tile = new()
                        {
                            X = tile_x,
                            Y = tile_y
                        };
                        Vector2I tile_position = GetTilePosition(tile);
                        tile_node.Position = (tile_position - _mapPosition);
                        tile_node.Visible = true;
                    }
                    else
                    {
                        tile_node.Visible = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Renders a specific tile
    /// </summary>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    public void RenderTile(int x, int y, int zoom)
    {
        Sprite2D sprite = new()
        {
            Texture = TileDownloader.LoadingFileTex,
            Centered = false,
        };

        tiles[zoom][x][y] = sprite;
        AddChild(sprite);
        _renderCount++;

        _tileDownloader.RequestTexture(x, y, zoom);
    }

    /// <summary>
    /// Culls any tiles within a different zoom level or too far away
    /// These tiles are reloaded from disk if needed
    /// </summary>
    public void CleanTiles()
    {
        System.Collections.Generic.List<int> tilesToClean = new();

        foreach (var (tile_zoom, x_dict) in tiles)
        {

            foreach (var (tile_x, y_dict) in x_dict)
            {
                int distanceX = Math.Abs(_mapTile.X - tile_x);

                foreach (int tile_y in y_dict.Keys)
                {
                    int distanceY = Math.Abs(_mapTile.Y - tile_y);

                    if (_mapZoom != tile_zoom || distanceX > _tileAmountX * Globals.CleanDistanceFactor || distanceY > _timeAmountY * Globals.CleanDistanceFactor)
                    {
                        tilesToClean.Add(tile_y);
                    }
                }

                foreach (int tile_y in tilesToClean)
                {
                    _renderCount--;
                    y_dict[tile_y].QueueFree();
                    y_dict.Remove(tile_y);
                }
                tilesToClean.Clear();

            }
        }
    }

    /// <summary>
    /// Called when tile texture is ready
    /// Updates a tile's Node graphic
    /// </summary>
    /// <param name="texture">Texture</param>
    /// <param name="x">Tile X coordinate</param>
    /// <param name="y">Tile Y coordinate</param>
    /// <param name="zoom">Tile zoom level</param>
    public void OnTextureReady(Texture2D texture, int x, int y, int zoom)
    {
        if (tiles[zoom][x].ContainsKey(y))
        {
            tiles[zoom][x][y].Texture = texture;
        }

    }

    public override void _Ready()
    {
        _tileDownloader = GetNode<TileDownloader>("%TileDownloader");
    }
}