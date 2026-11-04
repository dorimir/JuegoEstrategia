using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMap : MonoBehaviour
{
    public float maxSizeTotal;
    public List<GameMapTile> allTiles;
    public Dictionary<string, GameMapTile> map;

    public GameMap(float maxsizeTotal)
    {
        this.maxSizeTotal = maxsizeTotal;
        allTiles = new List<GameMapTile>();
        map = new Dictionary<string, GameMapTile>();
        //Always create a middle tile for starting point, and not count towards the maxSize Total
        AddTile(0, 0, 0);
    }

    public void AddTile(float x, float y, int tileType)
    {
        GameMapTile newTile = new GameMapTile(x, y);
        newTile.tiletype = tileType;
        allTiles.Add(newTile);
        map.Add($"{x},{y}", newTile);
    }

    public void GenerateMapFromArray()
    {
        // 0 = Vacío / Agua
        // 1 a 8 = Tipos de hexágono / Terrenos
        int[,] mapLayout = new int[,]
        {
        { 0, 1, 1, 1, 0 },
        { 1, 2, 2, 2, 1 },
        { 1, 2, 3, 2, 1 },
        { 1, 2, 2, 2, 1 },
        { 0, 1, 1, 1, 0 }
        };

        int rows = mapLayout.GetLength(0);
        int cols = mapLayout.GetLength(1);

        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                int tileType = mapLayout[y, x];

                // Si no es 0, creamos la casilla
                if (tileType != 0)
                {
                    AddTile(x, y, tileType);
                }
            }
        }
    }
    public void AutogenerateMap()
    {
        float totalSize = this.maxSizeTotal;
        float currentTotalSize = totalSize; // for counting down to 0 of how many tiles still yet to be processed
        float currentLayer = 1; // Layer of tiles from the middle of the map
        int currentTileTypeForLayer = 1; // To determine which tile type we are working at the moment
        float currentExtraTileForLayer = (currentLayer);
        while(currentTotalSize > 0)
        {
            if(currentTileTypeForLayer < 5)//when the tile is expanding towards northwest, northeast, southwest, and southeast
            {
                switch (currentTileTypeForLayer)
                {
                    case 1:
                        AddTile(currentLayer, currentLayer, currentTileTypeForLayer);
                        break;
                    case 2:
                        AddTile(-currentLayer, currentLayer, currentTileTypeForLayer);
                        break;
                    case 3:
                        AddTile(currentLayer, -currentLayer, currentTileTypeForLayer);
                        break;
                    case 4:
                        AddTile(-currentLayer, -currentLayer, currentTileTypeForLayer);
                        break;
                }
                currentTileTypeForLayer++;
                currentTotalSize--;
            }
            else// when the tile is expaning towards west, east, north, and south
            {
                switch (currentTileTypeForLayer)
                {
                    case 5:
                        AddTile(currentLayer+1-(2f*currentExtraTileForLayer), 1+currentLayer, currentTileTypeForLayer);
                        break;
                    case 6:
                        AddTile(1+currentLayer, currentLayer+1-(2f * currentExtraTileForLayer), currentTileTypeForLayer);
                        break;
                    case 7:
                        AddTile(currentLayer+1-(2f * currentExtraTileForLayer), -1- currentLayer, currentTileTypeForLayer);
                        break;
                    case 8:
                        AddTile(-1-currentLayer, currentLayer+1-(2f * currentExtraTileForLayer), currentTileTypeForLayer);
                        break;
                }
                currentExtraTileForLayer--;
                currentTotalSize--;
                if(currentExtraTileForLayer == 0)
                {
                    currentExtraTileForLayer = currentLayer;
                    currentTileTypeForLayer++;
                }
            }
            if (currentTileTypeForLayer > 8) //Once it finishes dealing with all 8 directions, then reset fornext layer
            {
                currentTileTypeForLayer = 1;
                currentLayer++;
                currentExtraTileForLayer = currentLayer;
            }
        }
    }
}
