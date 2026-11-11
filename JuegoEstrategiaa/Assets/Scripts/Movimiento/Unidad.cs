using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unidad : MonoBehaviour
{
    public static List<Unidad> Todas = new List<Unidad>();
    public static Unidad Seleccionada { get; private set; }

    private static HashSet<HexCelda> casillasResaltadas = new HashSet<HexCelda>();

    [Header("Propietario")]
    public int jugadorId = 0;

    [Header("Configuración")]
    [SerializeField] private HexGridGenerator tablero;
    [SerializeField] private float velocidad = 3f;
    [SerializeField] private Vector2Int coordenadaInicial = Vector2Int.zero;

    [Header("Límite de Desplazamiento")]
    [Tooltip("Número de casillas/pasos máximos que puede recorrer por turno")]
    public int rangoMovimientoCasillas = 2;

    private HexCelda celdaActual;
    private bool enMovimiento;

    public HexCelda CeldaActual => celdaActual;
    public bool EnMovimiento => enMovimiento;
    public HexGridGenerator Tablero => tablero;

    private void Awake()
    {
        Todas.Add(this);
    }

    private void OnDestroy()
    {
        Todas.Remove(this);
        if (Seleccionada == this) Deseleccionar();
    }

    private IEnumerator Start()
    {
        if (tablero == null) tablero = FindObjectOfType<HexGridGenerator>();

        while (tablero == null || tablero.ObtenerMapaCeldas().Count == 0)
        {
            yield return null;
        }

        Dictionary<Vector2Int, HexCelda> mapa = tablero.ObtenerMapaCeldas();

        if (mapa.TryGetValue(coordenadaInicial, out celdaActual))
        {
            transform.position = celdaActual.transform.position + Vector3.back;
        }
        else
        {
            foreach (var kvp in mapa)
            {
                celdaActual = kvp.Value;
                coordenadaInicial = kvp.Key;
                break;
            }
            if (celdaActual != null) transform.position = celdaActual.transform.position + Vector3.back;
        }
    }

    // ---------- Gestión de Selección y Resaltado ----------

    public static void SeleccionarUnidad(Unidad unidad)
    {
        LimpiarResaltadoRango();
        Seleccionada = unidad;

        if (unidad != null)
        {
            unidad.MostrarRangoMovimiento();
        }
    }

    public static void Deseleccionar()
    {
        LimpiarResaltadoRango();
        Seleccionada = null;
    }

    public void MostrarRangoMovimiento()
    {
        if (tablero == null) return;

        casillasResaltadas = HexGridUtils.ObtenerCasillasEnRango(
            celdaActual,
            rangoMovimientoCasillas,
            tablero.ObtenerMapaCeldas()
        );

        foreach (HexCelda celda in casillasResaltadas)
        {
            if (celda != celdaActual)
            {
                celda.EstablecerEnRango(true);
            }
        }
    }

    public static void LimpiarResaltadoRango()
    {
        foreach (HexCelda celda in casillasResaltadas)
        {
            if (celda != null)
            {
                celda.EstablecerEnRango(false);
            }
        }
        casillasResaltadas.Clear();
    }

    // ---------- Utilidades Estáticas ----------

    public static Unidad EnCelda(HexCelda celda)
    {
        foreach (Unidad u in Todas)
        {
            if (u.celdaActual == celda) return u;
        }
        return null;
    }

    public static bool HayAlguienMoviendose()
    {
        foreach (Unidad u in Todas)
        {
            if (u.enMovimiento) return true;
        }
        return false;
    }

    public void NuevoTurno()
    {
        LimiteMovimiento limite = GetComponent<LimiteMovimiento>();
        if (limite != null) limite.Reiniciar();
    }

    private bool EsMiTurno()
    {
        return TurnManager.Instancia == null || TurnManager.Instancia.JugadorActual == jugadorId;
    }

    // ---------- Movimiento ----------

    public void IrHacia(HexCelda destino)
    {
        if (enMovimiento) return;
        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA) return;
        if (!EsMiTurno()) return;
        if (destino == celdaActual) return;
        if (EnCelda(destino) != null) return;

        HashSet<HexCelda> casillasValidas = HexGridUtils.ObtenerCasillasEnRango(
            celdaActual,
            rangoMovimientoCasillas,
            tablero.ObtenerMapaCeldas()
        );

        if (!casillasValidas.Contains(destino))
        {
            Debug.LogWarning($"[Unidad] La casilla ({destino.q}, {destino.r}) está fuera del rango.");
            return;
        }

        List<HexCelda> camino = AEstrella.BuscarCamino(celdaActual, destino, tablero.ObtenerMapaCeldas());
        if (camino == null || camino.Count < 2) return;

        LimpiarResaltadoRango();
        MoverPorCamino(camino);
    }

    public bool MoverPorCamino(List<HexCelda> camino)
    {
        if (enMovimiento || camino == null || camino.Count < 2) return false;

        StartCoroutine(RecorrerCamino(camino));
        return true;
    }

    private IEnumerator RecorrerCamino(List<HexCelda> camino)
    {
        enMovimiento = true;

        foreach (HexCelda celda in camino)
        {
            Vector3 objetivo = celda.transform.position + Vector3.back;
            while (transform.position != objetivo)
            {
                transform.position = Vector3.MoveTowards(transform.position, objetivo, velocidad * Time.deltaTime);
                yield return null;
            }
            celdaActual = celda;
        }

        enMovimiento = false;
        Deseleccionar();
    }
}