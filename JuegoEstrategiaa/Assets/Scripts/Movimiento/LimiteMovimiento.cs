using System.Collections.Generic;
using UnityEngine;

public class LimiteMovimiento : MonoBehaviour
{
    [SerializeField] private int maxCasillas = 3;
    public bool PuedeRecorrer(List<HexCelda> camino)
    {
        return camino.Count - 1 <= maxCasillas; // Pilla lista de casillas 
    }
}
