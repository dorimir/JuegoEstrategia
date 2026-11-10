using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unidad : MonoBehaviour
{
    public static List<Unidad> Todas = new List<Unidad>();
    public static Unidad Seleccionada;

    [Header("Propietario")]
    public int jugadorId = 0; // 0 = Jugador 1, 1 = Jugador 2...

    [Header("Configuracion")]
    [SerializeField] private HexGridGenerator tablero;
    [SerializeField] private float velocidad = 3f;
    [SerializeField] private Vector2Int coordenadaInicial = Vector2Int.zero;

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
        if (Seleccionada == this) Seleccionada = null;
    }

    private IEnumerator Start()
    {
        yield return null; // esperamos un frame a que se genere el tablero
        celdaActual = tablero.ObtenerMapaCeldas()[coordenadaInicial];
        transform.position = celdaActual.transform.position + Vector3.back;
    }

    // ---------- Utilidades estaticas ----------

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

    // ---------- Turnos ----------

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

    // Lo usa el HUMANO al hacer clic en una casilla
    public void IrHacia(HexCelda destino)
    {
        if (enMovimiento) return;
        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA) return; // el humano no mueve en turno de la IA
        if (!EsMiTurno()) return;
        if (destino == celdaActual) return;
        if (EnCelda(destino) != null) return;

        List<HexCelda> camino = AEstrella.BuscarCamino(celdaActual, destino, tablero.ObtenerMapaCeldas());
        MoverPorCamino(camino);
    }

    // Lo usan el humano y la IA: gasta puntos y recorre el camino si le llegan los puntos
    public bool MoverPorCamino(List<HexCelda> camino)
    {
        if (enMovimiento || camino == null || camino.Count < 2) return false;

        LimiteMovimiento limite = GetComponent<LimiteMovimiento>();
        if (limite != null)
        {
            if (!limite.PuedeRecorrer(camino)) return false;
            limite.Gastar(camino);
        }

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
    }
}
