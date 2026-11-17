using System.Collections.Generic;
using UnityEngine;

public static class HexGridUtils
{
    // Las 6 direcciones adyacentes en coordenadas axiales
    private static readonly Vector2Int[] DireccionesHex = new Vector2Int[]
    {
        new Vector2Int(+1, 0),  new Vector2Int(+1, -1), new Vector2Int(0, -1),
        new Vector2Int(-1, 0),  new Vector2Int(-1, +1), new Vector2Int(0, +1)
    };

    /// <summary>
    /// Obtiene las celdas adyacentes válidas a partir de una coordenada.
    /// </summary>
    public static List<HexCelda> ObtenerVecinos(Vector2Int coord, Dictionary<Vector2Int, HexCelda> mapa)
    {
        List<HexCelda> vecinos = new List<HexCelda>();

        foreach (var dir in DireccionesHex)
        {
            Vector2Int claveVecino = coord + dir;
            if (mapa.TryGetValue(claveVecino, out HexCelda celdaEncontrada))
            {
                vecinos.Add(celdaEncontrada);
            }
        }

        return vecinos;
    }

    /// <summary>
    /// Calcula la distancia mínima en pasos entre dos celdas hexagonales.
    /// </summary>
    public static int CalcularDistanciaHex(Vector2Int a, Vector2Int b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs((a.x + a.y) - (b.x + b.y))) / 2;
    }
}