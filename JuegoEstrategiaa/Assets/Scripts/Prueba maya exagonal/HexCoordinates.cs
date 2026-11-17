using UnityEngine;

[System.Serializable]
public struct HexCoordinates
{
    public int x;
    public int y;
    public int z => -x - y; // En coordenadas de cubo, la suma de x + y + z siempre es 0

    public HexCoordinates(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    // Calcular la distancia en casillas entre dos coordenadas hexagonales
    public static int Distance(HexCoordinates a, HexCoordinates b)
    {
        return (Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y) + Mathf.Abs(a.z - b.z)) / 2;
    }
}