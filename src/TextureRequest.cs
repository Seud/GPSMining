using Godot;
using System;

namespace GPSMining;

public partial class TextureRequest : RefCounted
{

	public int tile_x;
	public int tile_y;
	public int tile_zoom;
	public String tile_path;
    public bool on_disk;

	public TextureRequest(int tile_x, int tile_y, int tile_zoom, string tile_path, bool on_disk)
    {
        this.tile_x = tile_x;
        this.tile_y = tile_y;
        this.tile_zoom = tile_zoom;
        this.tile_path = tile_path;
        this.on_disk = on_disk;
    }

}
