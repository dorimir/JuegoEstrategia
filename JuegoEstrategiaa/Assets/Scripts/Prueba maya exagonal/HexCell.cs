using System.Collections.Generic;
using UnityEngine;

public class HexCell : MonoBehaviour
{
    public HexCoordinates Coordinates { get; private set; }
    public int TerrainTypeId { get; private set; }

    [SerializeField] private SpriteRenderer spriteRenderer;

    // Lista con las casillas vecinas existentes (excluye los ceros)
    public List<HexCell> Neighbors { get; private set; } = new List<HexCell>();

    public void Initialize(HexCoordinates coords, Vector3 worldPosition, int terrainTypeId, Sprite terrainSprite)
    {
        Coordinates = coords;
        transform.position = worldPosition;
        TerrainTypeId = terrainTypeId;
        gameObject.name = $"Hex_Cell_{coords.x}_{coords.y} [Type: {terrainTypeId}]";

        if (spriteRenderer != null && terrainSprite != null)
        {
            spriteRenderer.sprite = terrainSprite;
        }
    }

    public void AddNeighbor(HexCell neighbor)
    {
        if (neighbor != null && !Neighbors.Contains(neighbor))
        {
            Neighbors.Add(neighbor);
        }
    }
}