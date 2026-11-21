using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// IA enemiga MUY basica: en su turno, cada unidad suya avanza hacia la unidad
// enemiga mas cercana todo lo que le permitan sus puntos de movimiento.
public class EnemigoIA : MonoBehaviour
{
    [SerializeField] private float retardoInicio = 0.5f; // pausa antes de mover
    [SerializeField] private float pausaFinal = 0.5f;    // pausa antes de pasar turno

    [Header("Spawning AI")]
    [SerializeField] private List<Unidad> prefabsDisponibles; // Lista de unidades que la IA puede spawnear
    [SerializeField] private HexGridGenerator tablero;

    private void Start()
    {
        if (tablero == null) tablero = FindObjectOfType<HexGridGenerator>();
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

        // --- FASE DE SPAWN (Automático) ---
        EjecutarFaseSpawn(jugador);
        yield return new WaitForSeconds(0.5f); // Pausa tras spawnear

        // --- FASE DE MOVIMIENTO ---
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

    private void EjecutarFaseSpawn(int jugadorId)
    {
        if (prefabsDisponibles == null || prefabsDisponibles.Count == 0 || tablero == null) return;

        // 1. Encontrar cuarteles de la IA
        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            if (celda.terreno == TipoTerreno.Cuartel && celda.propietario == Propietario.IA)
            {
                IntentarSpawnearEnCuartel(celda, jugadorId);
            }
        }
    }

    private void IntentarSpawnearEnCuartel(HexCelda cuartel, int jugadorId)
    {
        // 2. Obtener vecinos libres
        List<HexCelda> vecinosLibres = new List<HexCelda>();
        Vector2Int coord = new Vector2Int(cuartel.q, cuartel.r);
        
        foreach (HexCelda vecina in HexGridUtils.ObtenerVecinos(coord, tablero.ObtenerMapaCeldas()))
        {
            if (vecina.costeMovimiento < 999 && Unidad.EnCelda(vecina) == null)
            {
                vecinosLibres.Add(vecina);
            }
        }

        if (vecinosLibres.Count == 0) return;

        // 3. Elección aleatoria de unidad y posición
        Unidad prefabElegido = prefabsDisponibles[Random.Range(0, prefabsDisponibles.Count)];
        HexCelda celdaElegida = vecinosLibres[Random.Range(0, vecinosLibres.Count)];

        // 4. Intentar spawnear (el SpawnManager valida el dinero)
        SpawnManager.Instancia.SpawnearUnidad(prefabElegido, celdaElegida, jugadorId);
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
