using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public struct TerrainMapping
{
    public int id;          // Ej: 1 = Hierba, 2 = Agua, 3 = Montaña
    public Sprite sprite;   // Sprite asignado a este ID
}

public class HexGridController : MonoBehaviour
{
    [Header("Configuración de Malla")]
    public float hexRadius = 1f;
    public HexCell cellPrefab; // Prefab base que contiene HexCell y SpriteRenderer

    [Header("Mapeo de Tipos por ID")]
    public List<TerrainMapping> terrainMappings;

    // MATRIZ INICIAL (Puedes cambiar el tamaño y los números libremente)
    // 0 = Sin casilla
    // 1, 2, 3... = Diferentes terrenos/prefabs
    private readonly int[,] initialMatrix = new int[,]
    {
        { 1, 1, 1, 0, 1 },
        { 1, 2, 2, 1, 1 },
        { 0, 1, 3, 3, 0 },
        { 1, 1, 2, 1, 1 },
        { 1, 0, 1, 1, 1 }
    };

    private float OuterRadius => hexRadius;
    private float InnerRadius => hexRadius * 0.866025404f;

    // Diccionarios para búsqueda rápida
    private Dictionary<HexCoordinates, HexCell> gridCells = new Dictionary<HexCoordinates, HexCell>();
    private Dictionary<int, Sprite> spriteDictionary = new Dictionary<int, Sprite>();

    // Las 6 direcciones en coordenadas de cubo para encontrar vecinos
    private static readonly HexCoordinates[] HexDirections = new HexCoordinates[]
    {
        new HexCoordinates( 1,  0), new HexCoordinates( 1, -1), new HexCoordinates( 0, -1),
        new HexCoordinates(-1,  0), new HexCoordinates(-1,  1), new HexCoordinates( 0,  1)
    };

    private void Start()
    {
        // 1. Cargar el mapeo de IDs a Sprites
        foreach (var mapping in terrainMappings)
        {
            if (!spriteDictionary.ContainsKey(mapping.id))
                spriteDictionary.Add(mapping.id, mapping.sprite);
        }

        // 2. Generar celdas basadas en la matriz inicial
        BuildGridFromMatrix();

        // 3. Conectar vecinos entre celdas válidas
        CalculateAllNeighbors();
    }

    private void BuildGridFromMatrix()
    {
        gridCells.Clear();

        int height = initialMatrix.GetLength(0);
        int width = initialMatrix.GetLength(1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int tileType = initialMatrix[y, x];

                // Si es 0, ignoramos y no creamos ninguna casilla ahí
                if (tileType == 0) continue;

                int hexX = x - y / 2;
                HexCoordinates coords = new HexCoordinates(hexX, y);
                Vector3 worldPos = HexToWorld(coords);

                // Instanciar el prefab de la casilla
                HexCell newCell = Instantiate(cellPrefab, worldPos, Quaternion.identity, transform);

                Sprite cellSprite = spriteDictionary.ContainsKey(tileType) ? spriteDictionary[tileType] : null;
                newCell.Initialize(coords, worldPos, tileType, cellSprite);

                gridCells.Add(coords, newCell);
            }
        }
    }

    private void CalculateAllNeighbors()
    {
        foreach (var pair in gridCells)
        {
            HexCoordinates currentCoords = pair.Key;
            HexCell currentCell = pair.Value;

            // Recorremos las 6 direcciones posibles
            foreach (HexCoordinates dir in HexDirections)
            {
                HexCoordinates neighborCoords = new HexCoordinates(
                    currentCoords.x + dir.x,
                    currentCoords.y + dir.y
                );

                // Solo agregamos el vecino si existe en el diccionario (los ceros no están en el diccionario)
                if (gridCells.TryGetValue(neighborCoords, out HexCell neighborCell))
                {
                    currentCell.AddNeighbor(neighborCell);
                }
            }
        }
    }

    private void Update()
    {
        DetectHexSelection();
    }

    private void DetectHexSelection()
    {
        if (Pointer.current == null) return;

        if (Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 screenPos = Pointer.current.position.ReadValue();
            Vector3 mouseScreenWithDepth = new Vector3(screenPos.x, screenPos.y, Mathf.Abs(Camera.main.transform.position.z));
            Vector3 worldPos = Camera.main.ScreenToWorldPoint(mouseScreenWithDepth);
            worldPos.z = 0f;

            HexCoordinates clickedCoords = WorldToHex(worldPos);

            // Obtener la casilla seleccionada
            if (gridCells.TryGetValue(clickedCoords, out HexCell selectedCell))
            {
                Debug.Log($"Casilla seleccionada: Tipo {selectedCell.TerrainTypeId} | Vecinos válidos: {selectedCell.Neighbors.Count}");
            }
        }
    }

    public Vector3 HexToWorld(HexCoordinates coords)
    {
        float posX = (coords.x + coords.y * 0.5f) * (InnerRadius * 2f);
        float posY = coords.y * (OuterRadius * 1.5f);
        return new Vector3(posX, posY, 0f);
    }

    public HexCoordinates WorldToHex(Vector3 worldPos)
    {
        float q = (Mathf.Sqrt(3f) / 3f * worldPos.x - 1f / 3f * worldPos.y) / OuterRadius;
        float r = (2f / 3f * worldPos.y) / OuterRadius;

        float rx = Mathf.Round(q);
        float ry = Mathf.Round(-q - r);
        float rz = Mathf.Round(r);

        float xDiff = Mathf.Abs(rx - q);
        float yDiff = Mathf.Abs(ry - (-q - r));
        float zDiff = Mathf.Abs(rz - r);

        if (xDiff > yDiff && xDiff > zDiff)
        {
            rx = -ry - rz;
        }
        else if (yDiff > zDiff)
        {
            ry = -rx - rz;
        }

        return new HexCoordinates((int)rx, (int)-rx - (int)rz);
    }
}