using System;
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

    // Para no recalcular el resaltado de ataque cada frame
    private static Unidad unidadConRangoAtaque;
    private static HexCelda celdaConRangoAtaque;

    [Header("Propietario")]
    public int jugadorId = 0;
    public Propietario propietario = Propietario.Jugador;

    [Header("Pesos de Ruta / Influencia")]
    public float pesoMovimiento = 1f;
    public float pesoTropas = 4f;
    public float pesoEstructuras = 2f;

    [Header("Configuración")]
    [SerializeField] protected HexGridGenerator tablero;
    [SerializeField] private float velocidad = 3f;
    [SerializeField] private Vector2Int coordenadaInicial = Vector2Int.zero;

    [Header("Límite de Desplazamiento")]
    [Tooltip("Número de casillas/pasos máximos que puede recorrer por turno")]
    public int rangoMovimientoCasillas = 2;

    protected HexCelda celdaActual;
    protected bool enMovimiento;

    public HexCelda CeldaActual => celdaActual;
    public bool EnMovimiento => enMovimiento;
    public HexGridGenerator Tablero => tablero;
    public int Coste => coste;

    [Header("Terreno favorito)")]
    [SerializeField] private TipoTerreno terrenoFavorito = TipoTerreno.Llanura;

    [Header("Terreno penalizado")]
    [SerializeField] private TipoTerreno terrenoPenalizado = TipoTerreno.Montana;

    [Header("Coste de la unidad")]
    [SerializeField] private int coste = 0;

    // =====================================================================
    //  COMBATE ESTILO MAGIC
    //  puntosAtaque / puntosDefensa = Fuerza / Resistencia (el "3/4" de una carta)
    //  DanioMarcado = daño acumulado este turno. Muere si DanioMarcado >= Resistencia.
    // =====================================================================

    [Header("Fuerza (Ataque)")]
    [SerializeField] private int puntosAtaque = 2;

    [Header("Resistencia (Defensa)")]
    [SerializeField] public int puntosDefensa = 3;

    [Header("Influencia Táctica")]
    [Tooltip("Peso de influencia que proyecta esta tropa en el mapa de influencia")]
    public int influenciaTropa = 5;

    [Header("Debug / Temporal")]
    public bool esAtaque = false;

    public enum TipoAtaque : int
    {
        Rectas = 1,
        Diagonales = 2,
        Largo = 3
    }

    [Header("Alcance de Ataque (en casillas)")]
    [SerializeField] private int alcanceAtaque = 1;
    [Header("Tipo de Ataque (aún sin usar en el combate)")]
    [SerializeField] private TipoAtaque tipoAtaque = TipoAtaque.Rectas;

    // --- Acceso público a los datos de combate ---
    public int PuntosAtaque => puntosAtaque;
    public int AlcanceAtaque => alcanceAtaque;
    public TipoAtaque TipoAtaqueUnidad => tipoAtaque;
    public TipoTerreno TerrenoFavorito => terrenoFavorito;

    /// <summary>Fuerza real ahora mismo (incluye bono de terreno favorito).</summary>
    public int AtaqueEfectivo => CombateManager.CalcularAtaque(this);
    /// <summary>Resistencia real ahora mismo (incluye bono de defensa del terreno).</summary>
    public int ResistenciaEfectiva => CombateManager.CalcularResistencia(this);

    /// <summary>Daño marcado este turno (se borra al final del turno, como en Magic).</summary>
    public int DanioMarcado { get; private set; }
    public bool HaAtacado { get; private set; }
    public bool EstaMuerta { get; private set; }
    public bool EnAnimacion { get; private set; }

    // Eventos para que UI/efectos se enganchen sin acoplarse
    public event Action<int> OnDanioRecibido;
    public event Action OnMuerte;

    // Visual
    private SpriteRenderer[] renderers;
    private Color[] coloresOriginales;
    private Coroutine corrutinaFlash;

    //------------------FUNCIONES ------------------------------

    protected virtual void Awake()
    {
        Todas.Add(this);
        propietario = (jugadorId == 0) ? Propietario.Jugador : Propietario.IA;

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        coloresOriginales = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            coloresOriginales[i] = renderers[i].color;
        }
    }

    // Para elegir donde aparece una unidad creada desde un cuartel.
    // Hay que llamarla justo despues de crearla (antes de que se ejecute Start).
    public void EstablecerCoordenadaInicial(Vector2Int coordenada)
    {
        coordenadaInicial = coordenada;
    }

    protected virtual void OnDestroy()
    {
        Todas.Remove(this);
        if (Seleccionada == this) Deseleccionar();
        enMovimiento = false;
    }

    protected IEnumerator Start()
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

        if (unidad == null) return;

        // Según la fase se enseña el rango de movimiento o el de ataque
        if (TurnManager.Instancia == null || TurnManager.Instancia.FaseActual == FaseTurno.Movimiento)
        {
            unidad.MostrarRangoMovimiento();
        }
        else if (TurnManager.Instancia.FaseActual == FaseTurno.Ataque)
        {
            unidad.MostrarRangoAtaque();
        }
    }

    public static void Deseleccionar()
    {
        LimpiarResaltadoRango();
        Seleccionada = null;
    }

    public void MostrarRangoMovimiento()
    {
        if (tablero == null || celdaActual == null) return;

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

    /// <summary>
    /// Resalta todas las casillas a las que llega el ataque (distancia hexagonal <= alcance).
    /// Se puede llamar cada frame: solo recalcula si cambia la unidad o su casilla.
    /// </summary>
    public void MostrarRangoAtaque()
    {
        if (tablero == null || celdaActual == null) return;

        if (unidadConRangoAtaque == this && celdaConRangoAtaque == celdaActual && casillasResaltadas.Count > 0)
        {
            return;
        }

        LimpiarResaltadoRango();

        foreach (HexCelda celda in tablero.ObtenerMapaCeldas().Values)
        {
            if (celda == null || celda == celdaActual) continue;
            if (!EstaEnAlcance(celda)) continue;

            casillasResaltadas.Add(celda);
            celda.EstablecerEnRango(true);
        }

        unidadConRangoAtaque = this;
        celdaConRangoAtaque = celdaActual;
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
        unidadConRangoAtaque = null;
        celdaConRangoAtaque = null;
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

    /// <summary>Borra el daño marcado de TODAS las unidades (final de turno, como en Magic).</summary>
    public static void LimpiarDanioTodas()
    {
        foreach (Unidad u in Todas)
        {
            if (u != null) u.DanioMarcado = 0;
        }
    }

    public virtual void NuevoTurno()
    {
        HaAtacado = false;

        LimiteMovimiento limite = GetComponent<LimiteMovimiento>();
        if (limite != null) limite.Reiniciar();
    }

    protected bool EsMiTurno()
    {
        return TurnManager.Instancia == null || TurnManager.Instancia.JugadorActual == jugadorId;
    }

    // ---------- Combate ----------

    /// <summary>¿Está la casilla dentro del alcance de ataque de esta unidad?</summary>
    public bool EstaEnAlcance(HexCelda destino)
    {
        if (celdaActual == null || destino == null) return false;

        int distancia = HexGridUtils.CalcularDistanciaHex(
            new Vector2Int(celdaActual.q, celdaActual.r),
            new Vector2Int(destino.q, destino.r)
        );
        return distancia >= 1 && distancia <= alcanceAtaque;
    }

    /// <summary>Enemigo, vivo, en alcance y esta unidad aún no ha atacado este turno.</summary>
    public bool PuedeAtacar(Unidad objetivo)
    {
        if (objetivo == null || objetivo == this) return false;
        if (EstaMuerta || objetivo.EstaMuerta) return false;
        if (HaAtacado) return false;
        if (objetivo.jugadorId == jugadorId) return false;
        return EstaEnAlcance(objetivo.celdaActual);
    }

    public void MarcarComoAtacado()
    {
        HaAtacado = true;
    }

    /// <summary>
    /// Marca daño. NO comprueba la muerte aquí: se hace después con DebeMorir()/Morir()
    /// para que el daño de un combate sea simultáneo.
    /// </summary>
    public void RecibirDanio(int cantidad)
    {
        if (EstaMuerta || cantidad <= 0) return;

        DanioMarcado += cantidad;
        OnDanioRecibido?.Invoke(cantidad);

        if (gameObject.activeInHierarchy)
        {
            if (corrutinaFlash != null) StopCoroutine(corrutinaFlash);
            corrutinaFlash = StartCoroutine(RutinaFlash());
        }
    }

    public bool DebeMorir()
    {
        return !EstaMuerta && DanioMarcado > 0 && DanioMarcado >= ResistenciaEfectiva;
    }

    /// <summary>
    /// La unidad sale del juego inmediatamente (libera casilla, sale de Todas,
    /// actualiza influencia) y se desvanece antes de destruirse.
    /// </summary>
    public void Morir()
    {
        if (EstaMuerta) return;
        EstaMuerta = true;
        enMovimiento = false;

        Todas.Remove(this);
        if (Seleccionada == this) Deseleccionar();

        OnMuerte?.Invoke();

        if (InfluenceMapManager.Instancia != null)
        {
            InfluenceMapManager.Instancia.ActualizarTodosLosMapas();
        }

        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(RutinaMuerte());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ---------- Animaciones de combate ----------

    /// <summary>Se lanza hacia el objetivo, llama a alImpactar y vuelve a su sitio.</summary>
    public IEnumerator AnimarEmbestida(Unidad objetivo, Action alImpactar)
    {
        EnAnimacion = true;

        Vector3 inicio = transform.position;

        if (objetivo != null)
        {
            Vector3 destino = Vector3.Lerp(inicio, objetivo.transform.position, 0.6f);
            destino.z = inicio.z;

            const float duracionIda = 0.15f;
            float t = 0f;
            while (t < duracionIda)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(inicio, destino, t / duracionIda);
                yield return null;
            }

            alImpactar?.Invoke();

            const float duracionVuelta = 0.2f;
            t = 0f;
            while (t < duracionVuelta)
            {
                t += Time.deltaTime;
                transform.position = Vector3.Lerp(destino, inicio, t / duracionVuelta);
                yield return null;
            }
        }
        else
        {
            alImpactar?.Invoke();
        }

        transform.position = inicio;
        EnAnimacion = false;
    }

    private IEnumerator RutinaFlash()
    {
        const float duracion = 0.35f;
        float t = 0f;

        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = t / duracion;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color o = coloresOriginales[i];
                Color c = Color.Lerp(new Color(1f, 0.15f, 0.15f, o.a), o, k);
                renderers[i].color = c;
            }
            yield return null;
        }

        RestaurarColores();
        corrutinaFlash = null;
    }

    private void RestaurarColores()
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].color = coloresOriginales[i];
        }
    }

    private IEnumerator RutinaMuerte()
    {
        if (corrutinaFlash != null)
        {
            StopCoroutine(corrutinaFlash);
            corrutinaFlash = null;
        }

        const float duracion = 0.6f;
        float t = 0f;
        Vector3 escalaInicial = transform.localScale;

        while (t < duracion)
        {
            t += Time.deltaTime;
            float k = t / duracion;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                Color o = coloresOriginales[i];
                renderers[i].color = new Color(o.r, o.g * (1f - k), o.b * (1f - k), o.a * (1f - k));
            }
            transform.localScale = escalaInicial * (1f - 0.3f * k);
            yield return null;
        }

        Destroy(gameObject);
    }

    // ---------- Movimiento ----------

    public void IrHacia(HexCelda destino)
    {
        if (enMovimiento) return;
        if (EstaMuerta) return;
        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA) return;
        // Solo se puede mover en la fase de movimiento
        if (TurnManager.Instancia != null && TurnManager.Instancia.FaseActual != FaseTurno.Movimiento) return;
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

        List<HexCelda> camino = AEstrella.BuscarCamino(celdaActual, destino, tablero.ObtenerMapaCeldas(), jugadorId, esAtaque: esAtaque, pesoEstructuras: pesoEstructuras, pesoMovimiento: pesoMovimiento, pesoTropas: pesoTropas);
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

    protected IEnumerator RecorrerCamino(List<HexCelda> camino)
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

        if (InfluenceMapManager.Instancia != null)
        {
            InfluenceMapManager.Instancia.ActualizarTodosLosMapas();
        }
    }
}