using UnityEngine;

public class GameMapTile : MonoBehaviour
{
    public float x, y;
    public int tiletype;

    public GameMapTile(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public void SetTileType(int tiletype)
    {
        this.tiletype = tiletype;
    }
}
