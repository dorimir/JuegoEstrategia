using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// IA enemiga MUY basica: en su turno, cada unidad suya avanza hacia la unidad
// enemiga mas cercana todo lo que le permitan sus puntos de movimiento.
public class EnemigoIA : MonoBehaviour
{
    [SerializeField] private float retardoInicio = 0.5f; // pausa antes de mover
    [SerializeField] private float pausaFinal = 0.5f;    // pausa antes de pasar turno

    private void Start()
    {
        TurnManager.Instancia.OnTurnoIniciado += AlIniciarTurno;
    }

    private void OnDestroy()
    {
        if (TurnManager.Instancia != null) TurnManager.Instancia.OnTurnoIniciado -= AlIniciarTurno;
    }

    private void AlIniciarTurno(int jugador, int turno)
    {
        if (TurnManager.Instancia.TurnoDeIA)
        {
            StartCoroutine(JugarTurno(jugador));
        }
    }

    private IEnumerator JugarTurno(int jugador)
    {
        yield return new WaitForSeconds(retardoInicio);

        // Copia de la lista por si cambia durante el turno
        foreach (Unidad unidad in new List<Unidad>(Unidad.Todas))
        {
            if (unidad.jugadorId != jugador || unidad.CeldaActual == null) continue;

            Unidad objetivo = BuscarObjetivoMasCercano(unidad);
            if (objetivo == null) continue;

            List<HexCelda> camino = AEstrella.BuscarCamino(
                unidad.CeldaActual, objetivo.CeldaActual, unidad.Tablero.ObtenerMapaCeldas());
            if (camino == null || camino.Count < 2) continue;

            // La ultima casilla es la del objetivo (esta ocupada): nos paramos antes
            camino.RemoveAt(camino.Count - 1);

            List<HexCelda> recorte = RecortarPorPuntos(unidad, camino);

            if (unidad.MoverPorCamino(recorte))
            {
                while (unidad.EnMovimiento) yield return null;
            }
        }

        yield return new WaitForSeconds(pausaFinal);
        TurnManager.Instancia.FinalizarTurno();
    }

    private Unidad BuscarObjetivoMasCercano(Unidad yo)
    {
        Unidad mejor = null;
        int mejorDist = int.MaxValue;
        Vector2Int miPos = new Vector2Int(yo.CeldaActual.q, yo.CeldaActual.r);

        foreach (Unidad otra in Unidad.Todas)
        {
            if (otra.jugadorId == yo.jugadorId || otra.CeldaActual == null) continue;

            Vector2Int pos = new Vector2Int(otra.CeldaActual.q, otra.CeldaActual.r);
            int dist = HexGridUtils.CalcularDistanciaHex(miPos, pos);
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejor = otra;
            }
        }
        return mejor;
    }

    // Se queda con el trozo del camino que le llegan los puntos de movimiento
    private List<HexCelda> RecortarPorPuntos(Unidad unidad, List<HexCelda> camino)
    {
        LimiteMovimiento limite = unidad.GetComponent<LimiteMovimiento>();
        int puntos = limite != null ? limite.PuntosRestantes : int.MaxValue;

        List<HexCelda> resultado = new List<HexCelda> { camino[0] };
        int gastado = 0;

        for (int i = 1; i < camino.Count; i++)
        {
            int coste = camino[i].costeMovimiento;
            if (gastado + coste > puntos) break;
            if (Unidad.EnCelda(camino[i]) != null) break; // casilla ocupada

            gastado += coste;
            resultado.Add(camino[i]);
        }
        return resultado;
    }
}
