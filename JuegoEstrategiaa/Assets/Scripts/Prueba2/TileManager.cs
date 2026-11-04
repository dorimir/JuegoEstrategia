using UnityEngine;

public class TileManager : MonoBehaviour
{
    public SpriteRenderer SpriteRenderer;
    public void SetTileColor(Color color)
    {
        SpriteRenderer.color = color;
    }
}
