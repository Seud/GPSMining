using Godot;
using Godot.Collections;
using System;
using System.Security;

namespace GPSMining;

[GlobalClass]
public partial class OSMTiles : Node2D
{
    public const int TILE_SIZE = MercatorMap.TILE_SIZE;

    public const int CLEAN_DISTANCE_FACTOR = 2;

    public const int MAX_RENDERED_TILES = 1000;

    private int tile_amount_x = 3;
    private int tile_amount_y = 3;

    private int map_zoom = 0;
    private Vector2I map_position = new();
    private Vector2I map_tile = new();

    private int render_count = 0;

    ImageTexture default_tile_tex = new();
    private Dictionary<int, Dictionary<int, Dictionary<int, Sprite2D>>> tiles = new();

    public static String GetTileID(Vector2I tile, int zoom)
    {
        return $"z{zoom}x{tile.X}y{tile.Y}";
    }

    public static Vector2I GetTile(Vector2I position)
    {
        return position / TILE_SIZE;
    }

    public static Vector2I GetLocalPosition(Vector2I position)
    {
        return position % TILE_SIZE;
    }

    public static Vector2I GetTilePosition(Vector2I tile)
    {
        return tile * TILE_SIZE;
    }

    public static int GetMaxTile(int zoom)
    {
        return 1 << zoom;
    }

    public void UpdateSize(Vector2 size)
    {
        tile_amount_x = (int)Mathf.Ceil(size.X / (2 * TILE_SIZE * NoiseMap.TILE_SCALE));
        tile_amount_y = (int)Mathf.Ceil(size.Y / (2 * TILE_SIZE * NoiseMap.TILE_SCALE));

        GD.Print($"Tile grid {tile_amount_x}x{tile_amount_y}");
    }

    public void Move(Vector2I position, int zoom)
    {
        map_position = position;
        map_zoom = zoom;
        map_tile = GetTile(map_position);
        RenderTiles();
    }

    public void RenderTiles()
    {
        if (!tiles.ContainsKey(map_zoom))
        {
            tiles[map_zoom] = new();
        }

        for (int x = -tile_amount_x; x <= tile_amount_x; x++)
        {
            int target_x = map_tile.X + x;

            if (!tiles[map_zoom].ContainsKey(target_x))
            {
                tiles[map_zoom][target_x] = new();
            }

            for (int y = -tile_amount_y; y <= tile_amount_y; y++)
            {
                int target_y = map_tile.Y + y;

                if (target_y < 0 || target_y >= GetMaxTile(map_zoom))
                    continue;

                if (!tiles[map_zoom][target_x].ContainsKey(target_y))
                {
                    RenderTile(target_x, target_y, map_zoom);
                }

            }
        }

        GD.Print($"Total {render_count} tiles");

        if (render_count > MAX_RENDERED_TILES)
        {
            CleanTiles();
        }

        foreach (var (tile_zoom, x_dict) in tiles)
        {
            foreach (var (tile_x, y_dict) in x_dict)
            {
                foreach (var (tile_y, tile_node) in y_dict)
                {
                    if (tile_zoom == map_zoom)
                    {
                        Vector2I tile = new()
                        {
                            X = tile_x,
                            Y = tile_y
                        };
                        Vector2I tile_position = GetTilePosition(tile);
                        tile_node.Position = (tile_position - map_position);
                        tile_node.Visible = true;
                    } else
                    {
                        tile_node.Visible = false;
                    }
                }
            }
        }
    }

    public void RenderTile(int x, int y, int zoom)
    {
        Sprite2D sprite = new()
        {
            Texture = default_tile_tex,
            Centered = false,
        };

        tiles[zoom][x][y] = sprite;
        AddChild(sprite);
        render_count++;
    }

    public void CleanTiles()
    {
        System.Collections.Generic.List<int> to_clean = new();

        foreach (var (tile_zoom, x_dict) in tiles)
        {

            foreach (var (tile_x, y_dict) in x_dict)
            {
                int distance_x = Math.Abs(map_tile.X - tile_x);

                foreach (int tile_y in y_dict.Keys)
                {
                    int distance_y = Math.Abs(map_tile.Y - tile_y);

                    if(map_zoom != tile_zoom || distance_x > tile_amount_x * CLEAN_DISTANCE_FACTOR || distance_y > tile_amount_y * CLEAN_DISTANCE_FACTOR)
                    {
                        to_clean.Add(tile_y);
                    }
                }

                foreach(int tile_y in to_clean)
                {
                    render_count--;
                    y_dict[tile_y].QueueFree();
                    y_dict.Remove(tile_y);
                }
                to_clean.Clear();

            }
        }

        GD.Print($"(Cleaned) Total {render_count} tiles");
    }

    public override void _Ready()
    {
        Image default_tile_image = new();
        default_tile_image.Load("res://house256.jpg");
        default_tile_tex.SetImage(default_tile_image);
    }
}