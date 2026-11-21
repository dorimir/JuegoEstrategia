using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    public static SpawnManager Instancia { get; private set; }

    [Header("Referencias")]
    [SerializeField] private MonedasYBanderas sistemaMonedas;
    [SerializeField] private HexGridGenerator gridGenerator;

    [Header("Enemigo Lobo / Punto Rojo")]
    [SerializeField] private Unidad prefabLobo;
    [SerializeField] private Vector2Int coordenadaPuntoRojo = new Vector2Int(7, 0);
    [SerializeField] private int aiJugadorId = 1;
    [SerializeField] private bool spawnearLoboAlIniciar = true;
    [SerializeField] private bool spawnearEnVecinosAleatoriosAlTurnoIA = true;

    [Header("IA Temporal (Pruebas sin GOAP)")]
    [SerializeField] private bool habilitarIATemporal = true;

    private bool suscritoATurno = false;

    private void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);

        if (gridGenerator == null)
        {
            gridGenerator = FindFirstObjectByType<HexGridGenerator>();
        }
    }

    private void OnEnable()
    {
        IntentarSuscribirTurno();
    }

    private void Start()
    {
        IntentarSuscribirTurno();

    }

    private void OnDisable()
    {
        if (TurnManager.Instancia != null && suscritoATurno)
        {
            TurnManager.Instancia.OnTurnoIniciado -= AlIniciarTurno;
            suscritoATurno = false;
        }
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }

    private void IntentarSuscribirTurno()
    {
        if (!suscritoATurno && TurnManager.Instancia != null)
        {
            TurnManager.Instancia.OnTurnoIniciado += AlIniciarTurno;
            suscritoATurno = true;
        }
    }

    /// <summary>
    /// Intenta spawnear una unidad para un jugador específico en una celda determinada.
    /// Maneja el coste, la instanciación y la configuración inicial.
    /// </summary>
    public bool SpawnearUnidad(Unidad prefab, HexCelda celda, int jugadorId, bool cobrarMonedas = true)
    {

        if (prefab == null || celda == null)
        {
            Debug.LogWarning("[SpawnManager] Prefab o celda nula al intentar spawnear.");
            return false;
        }

        // 1. Validar si el jugador tiene suficiente dinero (opcional para spawns iniciales/debug)
        if (cobrarMonedas && sistemaMonedas != null && !sistemaMonedas.Gastar(jugadorId, prefab.Coste))
        {
            Debug.Log($"[SpawnManager] Jugador {jugadorId} no tiene suficientes monedas para {prefab.name} (Coste: {prefab.Coste}).");
            return false;
        }

        // 2. Validar que la celda esté libre
        if (Unidad.EnCelda(celda) != null)
        {
            Debug.LogWarning($"[SpawnManager] La celda ({celda.q}, {celda.r}) ya está ocupada.");
            return false;
        }

        // 3. Instanciar la unidad
        Unidad nueva = Instantiate(prefab, celda.transform.position + Vector3.back, Quaternion.identity);
        // 4. Configurar propiedades básicas
        nueva.jugadorId = jugadorId;
        nueva.propietario = (jugadorId == 0) ? Propietario.Jugador : Propietario.IA;
        nueva.EstablecerCoordenadaInicial(new Vector2Int(celda.q, celda.r));

        Debug.Log($"[SpawnManager] Jugador {jugadorId} ha spawneado {prefab.name} en ({celda.q}, {celda.r}).");
        return true;
    }

    /// <summary>
    /// Coloca el lobo directamente en la celda del punto rojo (7, 0) al iniciar.
    /// </summary>
    public bool ColocarLoboEnPuntoRojo()
    {
        if (prefabLobo == null)
        {
            Debug.LogWarning("[SpawnManager] No se ha asignado 'prefabLobo'.");
            return false;
        }

        HexCelda puntoRojo = ObtenerCeldaPuntoRojo();
        if (puntoRojo == null)
        {
            Debug.LogWarning($"[SpawnManager] No se encontró la celda ({coordenadaPuntoRojo.x}, {coordenadaPuntoRojo.y}) en el mapa.");
            return false;
        }

        return SpawnearUnidad(prefabLobo, puntoRojo, aiJugadorId, cobrarMonedas: false);
    }

    /// <summary>
    /// Spawnea el lobo aleatoriamente en una de las celdas adyacentes libres alrededor de (7, 0).
    /// </summary>
    public bool SpawnearLoboEnCeldasAlrededorDelRojo()
    {
        if (prefabLobo == null)
        {
            Debug.LogWarning("[SpawnManager] No se ha asignado 'prefabLobo'.");
            return false;
        }

        Dictionary<Vector2Int, HexCelda> mapa = gridGenerator != null ? gridGenerator.ObtenerMapaCeldas() : null;
        HexCelda puntoRojo = ObtenerCeldaPuntoRojo();

        if (mapa == null || mapa.Count == 0 || puntoRojo == null)
        {
            Debug.LogWarning("[SpawnManager] Falta mapa o celda (7, 0) para spawnear alrededor.");
            return false;
        }

        List<HexCelda> vecinosLibres = HexGridUtils
            .ObtenerVecinos(new Vector2Int(puntoRojo.q, puntoRojo.r), mapa)
            .Where(c => c != null && c.costeMovimiento < 999 && Unidad.EnCelda(c) == null)
            .ToList();

        if (vecinosLibres.Count == 0)
        {
            Debug.Log("[SpawnManager] No hay celdas vecinas libres alrededor de (7, 0).");
            return false;
        }

        HexCelda elegida = vecinosLibres[Random.Range(0, vecinosLibres.Count)];
        return SpawnearUnidad(prefabLobo, elegida, aiJugadorId, cobrarMonedas: false);
    }

    private HexCelda ObtenerCeldaPuntoRojo()
    {
        Dictionary<Vector2Int, HexCelda> mapa = gridGenerator != null ? gridGenerator.ObtenerMapaCeldas() : null;
        if (mapa == null || mapa.Count == 0) return null;

        mapa.TryGetValue(coordenadaPuntoRojo, out HexCelda celda);
        return celda;
    }

    // --- TEMPORAL: Spawning y movimiento aleatorio para la IA hasta integrar GOAP ---
    private void AlIniciarTurno(int jugadorActual, int numeroTurno)
    {
        if (!habilitarIATemporal || jugadorActual != aiJugadorId) return;

        Dictionary<Vector2Int, HexCelda> mapa = gridGenerator != null
            ? gridGenerator.ObtenerMapaCeldas()
            : null;

        if (mapa == null || mapa.Count == 0) return;

        if (spawnearEnVecinosAleatoriosAlTurnoIA && numeroTurno > 1)
        {
            SpawnearLoboEnCeldasAlrededorDelRojo();
        }

        MoverUnidadesIAAleatoriamente(mapa);
    }

    private void MoverUnidadesIAAleatoriamente(Dictionary<Vector2Int, HexCelda> mapa)
    {
        List<Unidad> unidadesIA = Unidad.Todas
            .Where(u => u != null && u.jugadorId == aiJugadorId)
            .ToList();

        foreach (Unidad unidad in unidadesIA)
        {
            HexCelda origen = unidad.CeldaActual;
            if (origen == null) continue;

            List<HexCelda> destinosValidos = ObtenerCeldasAlcanzablesLibres(origen, unidad.rangoMovimientoCasillas, mapa);
            if (destinosValidos.Count == 0) continue;

            HexCelda destino = destinosValidos[Random.Range(0, destinosValidos.Count)];
            List<HexCelda> camino = AEstrella.BuscarCamino(
                origen,
                destino,
                mapa,
                aiJugadorId,
                pesoEstructuras: unidad.pesoEstructuras,
                esAtaque: false,
                pesoMovimiento: unidad.pesoMovimiento,
                pesoTropas: unidad.pesoTropas
            );

            if (camino != null && camino.Count > 1)
            {
                unidad.MoverPorCamino(camino);
            }
        }
    }

    private List<HexCelda> ObtenerCeldasAlcanzablesLibres(HexCelda origen, int rango, Dictionary<Vector2Int, HexCelda> mapa)
    {
        List<HexCelda> libres = new List<HexCelda>();
        Vector2Int coordOrigen = new Vector2Int(origen.q, origen.r);

        foreach (HexCelda celda in mapa.Values)
        {
            if (celda == null || celda == origen) continue;
            if (celda.costeMovimiento >= 999) continue;
            if (Unidad.EnCelda(celda) != null) continue;

            int distancia = HexGridUtils.CalcularDistanciaHex(coordOrigen, new Vector2Int(celda.q, celda.r));
            if (distancia <= rango)
            {
                libres.Add(celda);
            }
        }

        return libres;
    }
}