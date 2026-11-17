using System.Collections.Generic;
using UnityEngine;

// Puntos de movimiento por turno. Cada casilla cuesta lo que indique su costeMovimiento
// (llanura 1, bosque 2, montana 3...), asi el terreno importa de verdad.
public class LimiteMovimiento : MonoBehaviour
{
    [SerializeField] private int puntosPorTurno = 3;

    public int PuntosRestantes { get; private set; }

    private void Awake()
    {
        PuntosRestantes = puntosPorTurno;
    }

    // Se llama al empezar el turno de su dueno
    public void Reiniciar()
    {
        PuntosRestantes = puntosPorTurno;
    }

    // Coste total del camino (la casilla de inicio no cuesta)
    public int CosteCamino(List<HexCelda> camino)
    {
        int coste = 0;
        for (int i = 1; i < camino.Count; i++)
        {
            coste += camino[i].costeMovimiento;
        }
        return coste;
    }

    public bool PuedeRecorrer(List<HexCelda> camino)
    {
        return CosteCamino(camino) <= PuntosRestantes;
    }

    public void Gastar(List<HexCelda> camino)
    {
        PuntosRestantes -= CosteCamino(camino);
    }
}
