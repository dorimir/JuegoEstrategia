using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameMapManager : MonoBehaviour
{
    public float tileInclementX, tileIncrementY; //>This needs to be changed depending on how big the sprite used for each hexagon is
    public Color[] tileColor;//just show the the diferents type(directions) being created on the gamemap method
    public GameObject tileToGenerate;
    public GameMap currentMap;
    public int mapSize;

    public void Start()
    {
        currentMap = new GameMap(mapSize);
        currentMap.AutogenerateMap();//This is to generate the underlying coordinate for all the map tile, but it doesn't actually show on the UI yet
        showMap();
    }

    public void showMap()
    {
        foreach(GameMapTile tileInMap in currentMap.allTiles)
        {
            GameObject newTileObject = Instantiate(tileToGenerate);
            newTileObject.transform.position = new Vector2(tileInclementX * tileInMap.x, tileIncrementY * tileInMap.y);
            TileManager tileManager = newTileObject.GetComponent<TileManager>();
            tileManager.SetTileColor(tileColor[tileInMap.tiletype]);
        }
    }
}
