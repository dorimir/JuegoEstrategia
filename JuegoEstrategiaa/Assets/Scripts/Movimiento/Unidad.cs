using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unidad : MonoBehaviour
{
    //Todas las unidades
    public static List<Unidad> Todas = new List<Unidad>();

    //unidad seleccionada actualmente
    public static Unidad Seleccionada { get; private set; }

    private static HashSet<HexCelda> casillasResaltadas = new HashSet<HexCelda>();

    [Header("Propietario")]
    public int jugadorId = 0;

    [Header("Configuraci�n")]
    [SerializeField] private HexGridGenerator tablero;

    private HexCelda celdaActual;
    private bool enMovimiento;

    public HexCelda CeldaActual => celdaActual;
    public bool EnMovimiento => enMovimiento;
    public HexGridGenerator Tablero => tablero;

    [SerializeField] private float velocidad = 3f;

    //esto deberia ser solo para las tropas iniciales, las demás deberían parecer en un cuartel
    [SerializeField] private Vector2Int coordenadaInicial = Vector2Int.zero; 

    [Header("L�mite de Desplazamiento")]
    [Tooltip("N�mero de casillas/pasos m�ximos que puede recorrer por turno")]
    [SerializeField] private int rangoMovimientoCasillas = 1; //es dos por alguna razón en concreto o se puede cambiar? Puede ser privado?

    [Header("Puntos de Ataque")]
    [SerializeField] private int puntosAtaque = 1;

    [Header("Puntos de Defensa")]
    [SerializeField] private int puntosDefensa = 1;

    private enum TipoAtaque : int
    {
        Rectas = 1,
        Diagonales = 2,
        Largo = 3
    }

    [Header("Alcance de Ataque")]
    [SerializeField] private int alcanceAtaque = 1;
    [Header("Tipo de Ataque")]
    [SerializeField] private TipoAtaque tipoAtaque = TipoAtaque.Rectas;


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

//construimos un diccionario llamado mapa
        Dictionary<Vector2Int, HexCelda> mapa = tablero.ObtenerMapaCeldas();

//obtenemos la celda correspondiente a la coordenada inicial y nos colocamos en ella
        if (mapa.TryGetValue(coordenadaInicial, out celdaActual))
        {
            transform.position = celdaActual.transform.position + Vector3.back;
        }
//?? esto nos pone siempre en la primera celda, por que se hace esto?
        else
        {
            //possible bucle infinito?
            foreach (var kvp in mapa)
            {
                celdaActual = kvp.Value;
                coordenadaInicial = kvp.Key;
                break;
            }
            //si tenemos una celda actual, nos colocamos en ella
            if (celdaActual != null) transform.position = celdaActual.transform.position + Vector3.back;
        }
    }

    // ---------- Gesti�n de Selecci�n y Resaltado ----------

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

        public void MostrarRangoAtaque()
    {
        if (tablero == null) return;

        switch (tipoAtaque)
        {
            case TipoAtaque.Rectas:
                casillasResaltadas = HexGridUtils.FourObtenerCasillasEnRango
                (
                celdaActual,
                alcanceAtaque,
                tablero.ObtenerMapaCeldas()
                );

                foreach (HexCelda celda in casillasResaltadas)
                {
                    if (celda != celdaActual)
                    {
                        celda.EstablecerEnRango(true);
                    }
                }
                break;

            case TipoAtaque.Diagonales:
                   casillasResaltadas = HexGridUtils.DiagonalObtenerCasillasEnRango
                (
                celdaActual,
                alcanceAtaque,
                tablero.ObtenerMapaCeldas()
                );

                foreach (HexCelda celda in casillasResaltadas)
                {
                    if (celda != celdaActual)
                    {
                        celda.EstablecerEnRango(true);
                    }
                }
                break;

            case TipoAtaque.Largo:
                Debug.Log("Alcance largo");
                break;
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

    // ---------- Utilidades Est�ticas ----------

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
            Debug.LogWarning($"[Unidad] La casilla ({destino.q}, {destino.r}) est� fuera del rango.");
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