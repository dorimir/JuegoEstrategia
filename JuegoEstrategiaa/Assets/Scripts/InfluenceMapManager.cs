using System.Collections.Generic;
using UnityEngine;

public enum TipoMapaInfluencia
{
    Ninguno,
    TropasJugador,
    TropasIA,
    TropasFusionado,
    EstructurasJugador,
    EstructurasIA,
    EstructurasFusionado
}

public class InfluenceMapManager : MonoBehaviour
{
    public static InfluenceMapManager Instancia { get; private set; }

    [Header("Referencias")]
    [SerializeField] private HexGridGenerator tablero;

    [Header("Configuración Unificada")]
    [Tooltip("Radio único en casillas al que se propaga TODA influencia")]
    public int radioInfluencia = 3;

    [Header("Depuración Visual")]
    public TipoMapaInfluencia capaVisualActiva = TipoMapaInfluencia.Ninguno;

    // --- CAPAS DE TROPAS ---
    private Dictionary<HexCelda, float> mapaTropasJugador = new Dictionary<HexCelda, float>();
    private Dictionary<HexCelda, float> mapaTropasIA = new Dictionary<HexCelda, float>();
    private Dictionary<HexCelda, float> mapaTropasFusionado = new Dictionary<HexCelda, float>();

    // --- CAPAS DE ESTRUCTURAS / TERRENO ---
    private Dictionary<HexCelda, float> mapaEstructurasJugador = new Dictionary<HexCelda, float>();
    private Dictionary<HexCelda, float> mapaEstructurasIA = new Dictionary<HexCelda, float>();
    private Dictionary<HexCelda, float> mapaEstructurasFusionado = new Dictionary<HexCelda, float>();

    private void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        if (tablero == null) tablero = FindObjectOfType<HexGridGenerator>();
    }

    [Header("Control Rápido")]
    public KeyCode teclaAlternarMapa = KeyCode.M;

    private void Update()
    {
        if (Input.GetKeyDown(teclaAlternarMapa))
        {
            // 1. Recalcula todos los valores con las posiciones actuales de tropas y estructuras
            ActualizarTodosLosMapas();

            // 2. Cambia a la siguiente capa visual (o vuelve a Ninguno al dar la vuelta)
            RotarCapaVisual();
        }
    }

    private void OnGUI()
    {
        if (capaVisualActiva != TipoMapaInfluencia.Ninguno)
        {
            GUI.Label(new Rect(10, 120, 300, 25), $"[Tecla {teclaAlternarMapa}] Capa activa: {capaVisualActiva}");
        }
    }
    // =========================================================================
    // 1. FUNCIONES DE CÁLCULO INDEPENDIENTES (Para llamar desde fuera)
    // =========================================================================

    /// <summary>
    /// Recalcula todas las capas de tropas y estructuras de una vez.
    /// </summary>
    public void ActualizarTodosLosMapas()
    {
        CalcularMapaTropasFusionado();
        CalcularMapaEstructurasFusionado();
        ActualizarVisualizacionDebug();
    }

    /// <summary>
    /// Calcula únicamente la influencia proyectada por las tropas del Jugador (jugadorId == 0).
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaTropasJugador()
    {
        InicializarDiccionario(mapaTropasJugador);
        Dictionary<Vector2Int, HexCelda> celdas = tablero.ObtenerMapaCeldas();

        foreach (Unidad u in Unidad.Todas)
        {
            if (u.jugadorId == 0 && u.CeldaActual != null && u.influenciaTropa != 0)
            {
                PropagarEnCapa(mapaTropasJugador, u.CeldaActual, u.influenciaTropa, celdas);
            }
        }
        return mapaTropasJugador;
    }

    /// <summary>
    /// Calcula únicamente la influencia proyectada por las tropas de la IA (jugadorId == 1).
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaTropasIA()
    {
        InicializarDiccionario(mapaTropasIA);
        Dictionary<Vector2Int, HexCelda> celdas = tablero.ObtenerMapaCeldas();

        foreach (Unidad u in Unidad.Todas)
        {
            if (u.jugadorId == 1 && u.CeldaActual != null && u.influenciaTropa != 0)
            {
                PropagarEnCapa(mapaTropasIA, u.CeldaActual, u.influenciaTropa, celdas);
            }
        }
        return mapaTropasIA;
    }

    /// <summary>
    /// Fusiona Tropas Jugador (+) y Tropas IA (-).
    /// Positivo = Superioridad Jugador | Negativo = Superioridad IA | 0 = Equilibrio o vacío.
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaTropasFusionado()
    {
        CalcularMapaTropasJugador();
        CalcularMapaTropasIA();
        InicializarDiccionario(mapaTropasFusionado);

        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            mapaTropasFusionado[celda] = mapaTropasJugador[celda] - mapaTropasIA[celda];
        }
        return mapaTropasFusionado;
    }

    /// <summary>
    /// Calcula únicamente la influencia de las casillas/estructuras propiedad del Jugador.
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaEstructurasJugador()
    {
        InicializarDiccionario(mapaEstructurasJugador);
        Dictionary<Vector2Int, HexCelda> celdas = tablero.ObtenerMapaCeldas();

        foreach (HexCelda celda in celdas.Values)
        {
            if (celda.propietario == Propietario.Jugador && celda.influenciaTerreno != 0)
            {
                PropagarEnCapa(mapaEstructurasJugador, celda, celda.influenciaTerreno, celdas);
            }
        }
        return mapaEstructurasJugador;
    }

    /// <summary>
    /// Calcula únicamente la influencia de las casillas/estructuras propiedad de la IA.
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaEstructurasIA()
    {
        InicializarDiccionario(mapaEstructurasIA);
        Dictionary<Vector2Int, HexCelda> celdas = tablero.ObtenerMapaCeldas();

        foreach (HexCelda celda in celdas.Values)
        {
            if (celda.propietario == Propietario.IA && celda.influenciaTerreno != 0)
            {
                PropagarEnCapa(mapaEstructurasIA, celda, celda.influenciaTerreno, celdas);
            }
        }
        return mapaEstructurasIA;
    }

    /// <summary>
    /// Fusiona Estructuras Jugador (+) y Estructuras IA (-).
    /// </summary>
    public Dictionary<HexCelda, float> CalcularMapaEstructurasFusionado()
    {
        CalcularMapaEstructurasJugador();
        CalcularMapaEstructurasIA();
        InicializarDiccionario(mapaEstructurasFusionado);

        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            mapaEstructurasFusionado[celda] = mapaEstructurasJugador[celda] - mapaEstructurasIA[celda];
        }
        return mapaEstructurasFusionado;
    }

    // =========================================================================
    // 2. CONSULTAS PUNTUALES POR CASILLA (Para evaluación rápida de la IA)
    // =========================================================================

    public float ObtenerValor(TipoMapaInfluencia tipo, HexCelda celda)
    {
        Dictionary<HexCelda, float> mapa = ObtenerDiccionarioPorTipo(tipo);
        if (mapa != null && mapa.TryGetValue(celda, out float valor))
        {
            return valor;
        }
        return 0f;
    }

    private Dictionary<HexCelda, float> ObtenerDiccionarioPorTipo(TipoMapaInfluencia tipo)
    {
        switch (tipo)
        {
            case TipoMapaInfluencia.TropasJugador: return mapaTropasJugador;
            case TipoMapaInfluencia.TropasIA: return mapaTropasIA;
            case TipoMapaInfluencia.TropasFusionado: return mapaTropasFusionado;
            case TipoMapaInfluencia.EstructurasJugador: return mapaEstructurasJugador;
            case TipoMapaInfluencia.EstructurasIA: return mapaEstructurasIA;
            case TipoMapaInfluencia.EstructurasFusionado: return mapaEstructurasFusionado;
            default: return null;
        }
    }

    // =========================================================================
    // 3. LÓGICA INTERNA DE PROPAGACIÓN
    // =========================================================================

    private void InicializarDiccionario(Dictionary<HexCelda, float> diccionario)
    {
        diccionario.Clear();
        if (tablero == null) return;

        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            diccionario[celda] = 0f;
        }
    }

    private void PropagarEnCapa(Dictionary<HexCelda, float> capa, HexCelda origen, float fuerzaMaxima, Dictionary<Vector2Int, HexCelda> mapa)
    {
        HashSet<HexCelda> casillasAfectadas = HexGridUtils.ObtenerCasillasEnRango(origen, radioInfluencia, mapa);

        foreach (HexCelda celda in casillasAfectadas)
        {
            int distancia = HexGridUtils.CalcularDistanciaHex(
                new Vector2Int(origen.q, origen.r),
                new Vector2Int(celda.q, celda.r)
            );

            float factor = Mathf.Max(0.1f, 1f - ((float)distancia / (radioInfluencia + 1)));
            capa[celda] += fuerzaMaxima * factor;
        }
    }

    // =========================================================================
    // 4. DEPURACIÓN VISUAL POR CAPA
    // =========================================================================

    public void CambiarCapaVisual(TipoMapaInfluencia nuevaCapa)
    {
        capaVisualActiva = nuevaCapa;
        ActualizarVisualizacionDebug();
    }

    public void RotarCapaVisual()
    {
        int siguiente = ((int)capaVisualActiva + 1) % System.Enum.GetValues(typeof(TipoMapaInfluencia)).Length;
        capaVisualActiva = (TipoMapaInfluencia)siguiente;
        ActualizarVisualizacionDebug();
    }

    private void ActualizarVisualizacionDebug()
    {
        if (tablero == null) return;
        Dictionary<HexCelda, float> capaActual = ObtenerDiccionarioPorTipo(capaVisualActiva);

        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            if (capaVisualActiva == TipoMapaInfluencia.Ninguno || capaActual == null)
            {
                celda.LimpiarColorInfluencia();
                continue;
            }

            float valor = capaActual.TryGetValue(celda, out float v) ? v : 0f;
            float t = Mathf.Clamp(valor / 15f, -1f, 1f);
            Color colorDebug;

            if (t > 0.05f)
                colorDebug = Color.Lerp(Color.white, Color.blue, t);
            else if (t < -0.05f)
                colorDebug = Color.Lerp(Color.white, Color.red, -t);
            else
                colorDebug = Color.gray;

            celda.EstablecerColorInfluencia(colorDebug);
        }
    }
}