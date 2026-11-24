using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Fases del ciclo de turno del juego de estrategia.
/// </summary>
public enum FaseTurno
{
    Recursos,          // 1. Automática: se otorgan recursos a todos según casillas controladas
    Reclutamiento,     // 2. Solo reclutar unidades en cuarteles
    Movimiento,        // 3. Mover unidades hasta agotar puntos de movimiento
    Ataque,            // 4. Decidir si atacar o no a enemigos en rango
    ResolucionCombate  // 5. Automática: resolución y cálculo de daños de combates
}

/// <summary>
/// Representa una declaración de ataque entre dos unidades.
/// </summary>
[Serializable]
public class DeclaracionAtaque
{
    public Unidad Atacante;
    public Unidad Objetivo;

    public DeclaracionAtaque(Unidad atacante, Unidad objetivo)
    {
        Atacante = atacante;
        Objetivo = objetivo;
    }
}

/// <summary>
/// Gestor modular de turnos y fases de juego.
/// Soporta 1 jugador humano y al menos 2 IAs rivales sin hardcodear lógica.
/// </summary>
public class TurnManager : MonoBehaviour
{
    public static TurnManager Instancia { get; private set; }

    [Header("Configuración de Jugadores")]
    [Tooltip("Número total de participantes (1 Humano + mínimo 2 IAs).")]
    [SerializeField] private int numeroJugadores = 3;

    [Tooltip("Índices de los jugadores controlados por la IA (0 = Jugador 1 Humano, 1 = IA 1, 2 = IA 2...).")]
    [SerializeField] private List<int> jugadoresIA = new List<int> { 1, 2 };

    [Header("Tiempos de Fases")]
    [SerializeField] private float pausaFaseAutomatica = 0.8f;
    [SerializeField] private float pausaAccionIA = 0.4f;

    [Header("Economía y Recursos")]
    [SerializeField] private int monedasBasePorRonda = 2;
    [SerializeField] private int monedasPorEstructura = 2;

    // Estado del juego
    public int JugadorActual { get; private set; } = 0;
    public int NumeroTurno { get; private set; } = 1;
    public FaseTurno FaseActual { get; private set; } = FaseTurno.Recursos;
    public bool TurnoDeIA => jugadoresIA.Contains(JugadorActual);

    // Eventos
    public event Action<int, int> OnTurnoIniciado;
    public event Action<FaseTurno, int> OnFaseIniciada;
    public event Action<List<string>> OnCombatesResueltos;

    // Registro de combate y movimiento
    private readonly List<DeclaracionAtaque> ataquesDeclarados = new List<DeclaracionAtaque>();
    private readonly Dictionary<Unidad, int> puntosMovimientoRestantes = new Dictionary<Unidad, int>();
    private readonly Dictionary<Unidad, HexCelda> ultimaCeldaRegistrada = new Dictionary<Unidad, HexCelda>();
    private readonly List<string> ultimosReportesCombate = new List<string>();

    // Inventarios de recursos por jugador: [jugadorId -> [TipoRecurso -> cantidad]]
    private readonly Dictionary<int, Dictionary<TipoRecurso, int>> recursosPorJugador = new Dictionary<int, Dictionary<TipoRecurso, int>>();
    private readonly Dictionary<int, int> monedasPorJugador = new Dictionary<int, int>();

    // Acceso reflection seguro y cacheado para campos privados de Unidad
    private static readonly FieldInfo campoPuntosAtaque = typeof(Unidad).GetField("puntosAtaque", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo campoAlcanceAtaque = typeof(Unidad).GetField("alcanceAtaque", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo campoTerrenoFavorito = typeof(Unidad).GetField("terrenoFavorito", BindingFlags.NonPublic | BindingFlags.Instance);

    private Coroutine corrutinaFaseActiva;
    private bool procesandoFaseAutomatica = false;

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        ValidarConfiguracionJugadores();
        InicializarEconomia();
    }

    private void Start()
    {
        IniciarNuevaRonda();
    }

    private void ValidarConfiguracionJugadores()
    {
        // Garantizar mínimo 1 humano y al menos 2 IAs (mínimo 3 jugadores)
        if (numeroJugadores < 3)
        {
            numeroJugadores = 3;
        }

        if (jugadoresIA == null || jugadoresIA.Count < 2)
        {
            jugadoresIA = new List<int>();
            for (int i = 1; i < numeroJugadores; i++)
            {
                jugadoresIA.Add(i);
            }
        }
    }

    private void InicializarEconomia()
    {
        for (int i = 0; i < numeroJugadores; i++)
        {
            recursosPorJugador[i] = new Dictionary<TipoRecurso, int>
            {
                { TipoRecurso.Madera, 0 },
                { TipoRecurso.Oro, 0 },
                { TipoRecurso.Comida, 0 }
            };
            monedasPorJugador[i] = 5; // Saldo inicial
        }
    }

    private void Update()
    {
        // Actualizar seguimiento de movimiento de unidades en la fase de movimiento
        if (FaseActual == FaseTurno.Movimiento)
        {
            ActualizarSeguimientoMovimiento();
        }

        // Si NO estamos en fase de movimiento, evitar que se mueva cualquier unidad
        if (FaseActual != FaseTurno.Movimiento)
        {
            if (FaseActual == FaseTurno.Reclutamiento && Unidad.Seleccionada != null)
            {
                Unidad.Deseleccionar();
            }
        }

        // En fase de ataque, resaltar rango de la unidad seleccionada
        if (FaseActual == FaseTurno.Ataque && !TurnoDeIA)
        {
            if (Unidad.Seleccionada != null && Unidad.Seleccionada.jugadorId == JugadorActual)
            {
                Unidad.Seleccionada.MostrarRangoAtaque();
            }

            // Detectar clic en unidad enemiga para declarar ataque
            if (Input.GetMouseButtonDown(0))
            {
                DetectarClicAtaque();
            }
        }

        // Control por teclado para el jugador humano
        if (!TurnoDeIA && !procesandoFaseAutomatica && Input.GetKeyDown(KeyCode.Space))
        {
            AvanzarFase();
        }
    }

    // -------------------------------------------------------------
    // GESTIÓN DEL FLUJO DE RONDAS Y FASES
    // -------------------------------------------------------------

    public void IniciarNuevaRonda()
    {
        JugadorActual = 0;
        ataquesDeclarados.Clear();

        Debug.Log($"<color=cyan>================ INICIO DE RONDA {NumeroTurno} ================</color>");

        // La ronda siempre comienza con la Fase 1 (Recursos) automática para todos
        CambiarFase(FaseTurno.Recursos);
    }

    private void CambiarFase(FaseTurno nuevaFase)
    {
        FaseActual = nuevaFase;
        Debug.Log($"[TurnManager] Fase cambiada a: <color=yellow>{FaseActual}</color> (Jugador {JugadorActual + 1} - {(TurnoDeIA ? "IA" : "Humano")})");

        OnFaseIniciada?.Invoke(FaseActual, JugadorActual);

        if (corrutinaFaseActiva != null)
        {
            StopCoroutine(corrutinaFaseActiva);
            corrutinaFaseActiva = null;
        }

        switch (FaseActual)
        {
            case FaseTurno.Recursos:
                corrutinaFaseActiva = StartCoroutine(RutinaFaseRecursos());
                break;

            case FaseTurno.Reclutamiento:
                PrepararFaseReclutamiento();
                if (TurnoDeIA)
                {
                    corrutinaFaseActiva = StartCoroutine(RutinaReclutamientoIA());
                }
                break;

            case FaseTurno.Movimiento:
                PrepararFaseMovimiento();
                if (TurnoDeIA)
                {
                    corrutinaFaseActiva = StartCoroutine(RutinaMovimientoIA());
                }
                break;

            case FaseTurno.Ataque:
                PrepararFaseAtaque();
                if (TurnoDeIA)
                {
                    corrutinaFaseActiva = StartCoroutine(RutinaAtaqueIA());
                }
                break;

            case FaseTurno.ResolucionCombate:
                corrutinaFaseActiva = StartCoroutine(RutinaResolucionCombate());
                break;
        }
    }

    public void AvanzarFase()
    {
        if (procesandoFaseAutomatica) return;

        // No permitir avanzar si alguna unidad sigue en movimiento físico
        if (Unidad.HayAlguienMoviendose())
        {
            Debug.LogWarning("[TurnManager] No se puede cambiar de fase mientras una unidad se está desplazando.");
            return;
        }

        switch (FaseActual)
        {
            case FaseTurno.Recursos:
                CambiarFase(FaseTurno.Reclutamiento);
                break;

            case FaseTurno.Reclutamiento:
                CambiarFase(FaseTurno.Movimiento);
                break;

            case FaseTurno.Movimiento:
                CambiarFase(FaseTurno.Ataque);
                break;

            case FaseTurno.Ataque:
                CambiarFase(FaseTurno.ResolucionCombate);
                break;

            case FaseTurno.ResolucionCombate:
                FinalizarTurnoJugador();
                break;
        }
    }

    /// <summary>
    /// Compatibilidad hacia atrás: pasar turno o avanzar fase según el contexto.
    /// </summary>
    public void FinalizarTurno()
    {
        AvanzarFase();
    }

    private void FinalizarTurnoJugador()
    {
        Unidad.Deseleccionar();

        int anterior = JugadorActual;
        JugadorActual = (JugadorActual + 1) % numeroJugadores;

        Debug.Log($"[TurnManager] Fin del turno del Jugador {anterior + 1}. Pasa el turno al Jugador {JugadorActual + 1}.");

        // Si volvemos al Jugador 0, significa que todos los participantes (humano e IAs) han jugado: nueva ronda
        if (JugadorActual == 0)
        {
            NumeroTurno++;
            IniciarNuevaRonda();
        }
        else
        {
            // El siguiente jugador empieza en Reclutamiento (los recursos ya se dieron en la Fase 1 de la ronda)
            OnTurnoIniciado?.Invoke(JugadorActual, NumeroTurno);
            CambiarFase(FaseTurno.Reclutamiento);
        }
    }

    // -------------------------------------------------------------
    // FASE 1: RECURSOS (AUTOMÁTICA PARA HUMANO Y TODAS LAS IAS)
    // -------------------------------------------------------------

    private IEnumerator RutinaFaseRecursos()
    {
        procesandoFaseAutomatica = true;
        Debug.Log("<color=green>--- FASE 1: RECURSOS (AUTOMÁTICA) ---</color>");
        Debug.Log("Otorgando recursos de casillas controladas tanto al jugador como a las IAs rivales...");

        DistribuirRecursosACasillasControladas();

        yield return new WaitForSeconds(pausaFaseAutomatica);

        procesandoFaseAutomatica = false;

        // Disparar evento de turno iniciado para el jugador 0
        OnTurnoIniciado?.Invoke(JugadorActual, NumeroTurno);

        // Avanzar automáticamente a la siguiente fase
        CambiarFase(FaseTurno.Reclutamiento);
    }

    private void DistribuirRecursosACasillasControladas()
    {
        HexGridGenerator grid = FindFirstObjectByType<HexGridGenerator>();
        Dictionary<Vector2Int, HexCelda> mapa = grid != null ? grid.ObtenerMapaCeldas() : new Dictionary<Vector2Int, HexCelda>();

        // Contadores temporales para registrar lo obtenido
        Dictionary<int, int> monedasGanadas = new Dictionary<int, int>();
        Dictionary<int, int> casillasContadas = new Dictionary<int, int>();

        for (int i = 0; i < numeroJugadores; i++)
        {
            monedasGanadas[i] = monedasBasePorRonda;
            casillasContadas[i] = 0;
        }

        // Evaluar casillas del mapa
        foreach (var kvp in mapa)
        {
            HexCelda celda = kvp.Value;
            if (celda == null) continue;

            int duenio = DeterminarDuenioCelda(celda);
            if (duenio >= 0 && duenio < numeroJugadores)
            {
                casillasContadas[duenio]++;

                // Recursos naturales
                if (celda.recurso != TipoRecurso.Ninguno)
                {
                    SumarRecurso(duenio, celda.recurso, 1);
                }

                // Monedas por banderas o cuarteles
                if (celda.terreno == TipoTerreno.Bandera || celda.terreno == TipoTerreno.BanderaCentral || celda.terreno == TipoTerreno.Cuartel)
                {
                    monedasGanadas[duenio] += monedasPorEstructura;
                }
                else if (celda.recurso == TipoRecurso.Oro)
                {
                    monedasGanadas[duenio] += 1;
                }
            }
        }

        // Asignar monedas a cada jugador y actualizar el sistema global
        for (int i = 0; i < numeroJugadores; i++)
        {
            AgregarMonedas(i, monedasGanadas[i]);
            string rol = i == 0 ? "Humano" : $"IA {i}";
            Debug.Log($"[Recursos] Jugador {i + 1} ({rol}) controla {casillasContadas[i]} casillas y recibe {monedasGanadas[i]} monedas.");
        }
    }

    private int DeterminarDuenioCelda(HexCelda celda)
    {
        // 1. Unidad presente en la casilla
        Unidad unidadPresente = Unidad.EnCelda(celda);
        if (unidadPresente != null)
        {
            return unidadPresente.jugadorId;
        }

        // 2. Propietario de la celda
        if (celda.propietario == Propietario.Jugador)
        {
            return 0; // Humano
        }
        else if (celda.propietario == Propietario.IA)
        {
            // Asignar a la IA más cercana si hay varias
            return AsignarIACercana(celda);
        }

        return -1;
    }

    private int AsignarIACercana(HexCelda celda)
    {
        if (jugadoresIA.Count == 1) return jugadoresIA[0];

        int mejorIA = jugadoresIA[0];
        int menorDist = int.MaxValue;
        Vector2Int posCelda = new Vector2Int(celda.q, celda.r);

        foreach (Unidad u in Unidad.Todas)
        {
            if (u != null && jugadoresIA.Contains(u.jugadorId) && u.CeldaActual != null)
            {
                int dist = HexGridUtils.CalcularDistanciaHex(posCelda, new Vector2Int(u.CeldaActual.q, u.CeldaActual.r));
                if (dist < menorDist)
                {
                    menorDist = dist;
                    mejorIA = u.jugadorId;
                }
            }
        }

        return mejorIA;
    }

    public void AgregarMonedas(int jugadorId, int cantidad)
    {
        if (!monedasPorJugador.ContainsKey(jugadorId)) monedasPorJugador[jugadorId] = 0;
        monedasPorJugador[jugadorId] += cantidad;

        // Sincronizar con el sistema MonedasYBanderas si existe
        if (MonedasYBanderas.Instancia != null)
        {
            MonedasYBanderas.Instancia.Gastar(jugadorId, -cantidad);
        }
    }

    public void SumarRecurso(int jugadorId, TipoRecurso recurso, int cantidad)
    {
        if (!recursosPorJugador.ContainsKey(jugadorId))
        {
            recursosPorJugador[jugadorId] = new Dictionary<TipoRecurso, int>();
        }

        if (!recursosPorJugador[jugadorId].ContainsKey(recurso))
        {
            recursosPorJugador[jugadorId][recurso] = 0;
        }

        recursosPorJugador[jugadorId][recurso] += cantidad;
    }

    public int ObtenerMonedas(int jugadorId)
    {
        if (MonedasYBanderas.Instancia != null)
        {
            return MonedasYBanderas.Instancia.GetMonedas(jugadorId);
        }
        return monedasPorJugador.ContainsKey(jugadorId) ? monedasPorJugador[jugadorId] : 0;
    }

    public int ObtenerRecurso(int jugadorId, TipoRecurso recurso)
    {
        if (recursosPorJugador.ContainsKey(jugadorId) && recursosPorJugador[jugadorId].ContainsKey(recurso))
        {
            return recursosPorJugador[jugadorId][recurso];
        }
        return 0;
    }

    // -------------------------------------------------------------
    // FASE 2: RECLUTAMIENTO
    // -------------------------------------------------------------

    private void PrepararFaseReclutamiento()
    {
        Unidad.Deseleccionar();
        Debug.Log($"<color=lightblue>--- FASE 2: RECLUTAMIENTO (Jugador {JugadorActual + 1}) ---</color>");
        Debug.Log("Solo se permite reclutar unidades en cuarteles. El movimiento está bloqueado.");
    }

    private IEnumerator RutinaReclutamientoIA()
    {
        yield return new WaitForSeconds(pausaAccionIA);

        // Si la IA tiene cuarteles y dinero, intentará spawnear
        SpawnManager spawn = SpawnManager.Instancia != null ? SpawnManager.Instancia : FindFirstObjectByType<SpawnManager>();
        HexGridGenerator grid = FindFirstObjectByType<HexGridGenerator>();

        if (grid != null && spawn != null)
        {
            List<HexCelda> cuartelesIA = new List<HexCelda>();
            foreach (HexCelda c in grid.ObtenerMapaCeldas().Values)
            {
                if (c.terreno == TipoTerreno.Cuartel && DeterminarDuenioCelda(c) == JugadorActual)
                {
                    cuartelesIA.Add(c);
                }
            }

            Debug.Log($"[IA Reclutamiento] IA {JugadorActual + 1} revisa {cuartelesIA.Count} cuartel(es).");
        }

        yield return new WaitForSeconds(pausaAccionIA);

        // Pasar a la siguiente fase
        AvanzarFase();
    }

    // -------------------------------------------------------------
    // FASE 3: MOVIMIENTO
    // -------------------------------------------------------------

    private void PrepararFaseMovimiento()
    {
        Debug.Log($"<color=yellow>--- FASE 3: MOVIMIENTO (Jugador {JugadorActual + 1}) ---</color>");
        Debug.Log("Mueve a tus unidades solo hasta que se agote su movimiento.");

        // Reiniciar y registrar puntos de movimiento para las unidades del jugador activo
        puntosMovimientoRestantes.Clear();
        ultimaCeldaRegistrada.Clear();

        foreach (Unidad u in Unidad.Todas)
        {
            if (u == null) continue;

            if (u.jugadorId == JugadorActual)
            {
                u.NuevoTurno();

                LimiteMovimiento limite = u.GetComponent<LimiteMovimiento>();
                int puntos = limite != null ? limite.PuntosRestantes : u.rangoMovimientoCasillas;
                puntosMovimientoRestantes[u] = puntos;
                ultimaCeldaRegistrada[u] = u.CeldaActual;
            }
        }
    }

    private void ActualizarSeguimientoMovimiento()
    {
        foreach (Unidad u in Unidad.Todas)
        {
            if (u == null || u.jugadorId != JugadorActual || u.CeldaActual == null) continue;

            if (!ultimaCeldaRegistrada.ContainsKey(u))
            {
                ultimaCeldaRegistrada[u] = u.CeldaActual;
                continue;
            }

            HexCelda celdaPrevia = ultimaCeldaRegistrada[u];
            if (u.CeldaActual != celdaPrevia && !u.EnMovimiento)
            {
                // La unidad completó un desplazamiento: calcular coste y descontar
                int distancia = HexGridUtils.CalcularDistanciaHex(
                    new Vector2Int(celdaPrevia.q, celdaPrevia.r),
                    new Vector2Int(u.CeldaActual.q, u.CeldaActual.r)
                );

                if (!puntosMovimientoRestantes.ContainsKey(u))
                {
                    puntosMovimientoRestantes[u] = u.rangoMovimientoCasillas;
                }

                puntosMovimientoRestantes[u] = Mathf.Max(0, puntosMovimientoRestantes[u] - distancia);
                ultimaCeldaRegistrada[u] = u.CeldaActual;

                // Descontar en LimiteMovimiento si existe
                LimiteMovimiento limite = u.GetComponent<LimiteMovimiento>();
                if (limite != null)
                {
                    List<HexCelda> caminoDummy = new List<HexCelda> { celdaPrevia, u.CeldaActual };
                    limite.Gastar(caminoDummy);
                }

                Debug.Log($"[Movimiento] {u.name} se desplazó {distancia} casillas. Puntos restantes: {puntosMovimientoRestantes[u]}.");

                // Si se agotó el movimiento, deseleccionar
                if (puntosMovimientoRestantes[u] <= 0)
                {
                    if (Unidad.Seleccionada == u)
                    {
                        Unidad.Deseleccionar();
                    }
                    Debug.Log($"[Movimiento] {u.name} ha agotado su movimiento para este turno.");
                }
            }
        }
    }

    public bool PuedeMoverseUnidad(Unidad u)
    {
        if (u == null) return false;
        if (puntosMovimientoRestantes.TryGetValue(u, out int pts))
        {
            return pts > 0;
        }
        return true;
    }

    private IEnumerator RutinaMovimientoIA()
    {
        yield return new WaitForSeconds(pausaAccionIA);

        // Mover cada unidad de la IA hacia el enemigo más cercano hasta agotar movimiento
        HexGridGenerator grid = FindFirstObjectByType<HexGridGenerator>();
        var mapa = grid != null ? grid.ObtenerMapaCeldas() : null;

        if (mapa != null)
        {
            foreach (Unidad unidad in new List<Unidad>(Unidad.Todas))
            {
                if (unidad == null || unidad.jugadorId != JugadorActual || unidad.CeldaActual == null) continue;

                Unidad objetivo = BuscarObjetivoMasCercano(unidad);
                if (objetivo == null || objetivo.CeldaActual == null) continue;

                List<HexCelda> camino = AEstrella.BuscarCamino(unidad.CeldaActual, objetivo.CeldaActual, mapa);
                if (camino == null || camino.Count < 2) continue;

                camino.RemoveAt(camino.Count - 1); // No pisar la celda del enemigo
                if (camino.Count < 2) continue;

                // Limitar por puntos restantes
                int puntos = puntosMovimientoRestantes.ContainsKey(unidad) ? puntosMovimientoRestantes[unidad] : unidad.rangoMovimientoCasillas;
                if (puntos <= 0) continue;

                List<HexCelda> caminoRecortado = new List<HexCelda> { camino[0] };
                for (int i = 1; i < camino.Count && i <= puntos; i++)
                {
                    if (Unidad.EnCelda(camino[i]) == null)
                    {
                        caminoRecortado.Add(camino[i]);
                    }
                    else break;
                }

                if (unidad.MoverPorCamino(caminoRecortado))
                {
                    while (unidad.EnMovimiento) yield return null;
                    if (puntosMovimientoRestantes.ContainsKey(unidad))
                    {
                        puntosMovimientoRestantes[unidad] = Mathf.Max(0, puntosMovimientoRestantes[unidad] - (caminoRecortado.Count - 1));
                    }
                }
            }
        }

        yield return new WaitForSeconds(pausaAccionIA);

        // Avanzar a la fase de ataque
        AvanzarFase();
    }

    private Unidad BuscarObjetivoMasCercano(Unidad origen)
    {
        Unidad mejor = null;
        int mejorDist = int.MaxValue;
        Vector2Int posOrigen = new Vector2Int(origen.CeldaActual.q, origen.CeldaActual.r);

        foreach (Unidad u in Unidad.Todas)
        {
            if (u == null || u.jugadorId == origen.jugadorId || u.CeldaActual == null) continue;
            int dist = HexGridUtils.CalcularDistanciaHex(posOrigen, new Vector2Int(u.CeldaActual.q, u.CeldaActual.r));
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejor = u;
            }
        }
        return mejor;
    }

    // -------------------------------------------------------------
    // FASE 4: ATAQUE (DECISIÓN DE ATAQUE)
    // -------------------------------------------------------------

    private void PrepararFaseAtaque()
    {
        Unidad.Deseleccionar();
        Debug.Log($"<color=red>--- FASE 4: ATAQUE (Jugador {JugadorActual + 1}) ---</color>");
        Debug.Log("Decide si atacar o no a las unidades enemigas que estén a rango de tus unidades.");
    }

    private void DetectarClicAtaque()
    {
        if (SelectionManager.Instancia == null) return;

        HexCelda celdaSeleccionada = SelectionManager.Instancia.celdaSeleccionada;
        if (celdaSeleccionada == null) return;

        Unidad enemigo = Unidad.EnCelda(celdaSeleccionada);
        if (enemigo != null && enemigo.jugadorId != JugadorActual && Unidad.Seleccionada != null)
        {
            Unidad atacante = Unidad.Seleccionada;
            if (atacante.jugadorId == JugadorActual && EstaEnRangoAtaque(atacante, enemigo))
            {
                DeclararAtaque(atacante, enemigo);
            }
        }
    }

    public bool EstaEnRangoAtaque(Unidad atacante, Unidad objetivo)
    {
        if (atacante == null || objetivo == null || atacante.CeldaActual == null || objetivo.CeldaActual == null)
            return false;

        int alcance = ObtenerAlcanceAtaque(atacante);
        int dist = HexGridUtils.CalcularDistanciaHex(
            new Vector2Int(atacante.CeldaActual.q, atacante.CeldaActual.r),
            new Vector2Int(objetivo.CeldaActual.q, objetivo.CeldaActual.r)
        );

        return dist <= alcance;
    }

    public void DeclararAtaque(Unidad atacante, Unidad objetivo)
    {
        if (atacante == null || objetivo == null) return;

        // Si ya tenía un ataque declarado, lo reemplazamos
        CancelarAtaque(atacante);

        ataquesDeclarados.Add(new DeclaracionAtaque(atacante, objetivo));
        Debug.Log($"<color=orange>[Ataque Declarado]</color> {atacante.name} (Jugador {atacante.jugadorId + 1}) -> {objetivo.name} (Jugador {objetivo.jugadorId + 1}).");
    }

    public void CancelarAtaque(Unidad atacante)
    {
        ataquesDeclarados.RemoveAll(d => d.Atacante == atacante);
    }

    public bool TieneAtaqueDeclarado(Unidad atacante)
    {
        return ataquesDeclarados.Exists(d => d.Atacante == atacante);
    }

    public Unidad ObtenerObjetivoDeclarado(Unidad atacante)
    {
        var decl = ataquesDeclarados.Find(d => d.Atacante == atacante);
        return decl != null ? decl.Objetivo : null;
    }

    private IEnumerator RutinaAtaqueIA()
    {
        yield return new WaitForSeconds(pausaAccionIA);

        // La IA decide si atacar o no a los enemigos que tenga a rango
        foreach (Unidad atacante in Unidad.Todas)
        {
            if (atacante == null || atacante.jugadorId != JugadorActual || atacante.CeldaActual == null) continue;

            Unidad mejorObjetivo = null;
            int menorDefensa = int.MaxValue;

            foreach (Unidad enemigo in Unidad.Todas)
            {
                if (enemigo == null || enemigo.jugadorId == JugadorActual || enemigo.CeldaActual == null) continue;

                if (EstaEnRangoAtaque(atacante, enemigo))
                {
                    // Decisión táctica: priorizar el enemigo más debilitado para asegurar la baja
                    if (enemigo.puntosDefensa < menorDefensa)
                    {
                        menorDefensa = enemigo.puntosDefensa;
                        mejorObjetivo = enemigo;
                    }
                }
            }

            if (mejorObjetivo != null)
            {
                DeclararAtaque(atacante, mejorObjetivo);
            }
        }

        yield return new WaitForSeconds(pausaAccionIA);

        // Avanzar a resolución automática de combates
        AvanzarFase();
    }

    // -------------------------------------------------------------
    // FASE 5: RESOLUCIÓN DE COMBATE (AUTOMÁTICA)
    // -------------------------------------------------------------

    private IEnumerator RutinaResolucionCombate()
    {
        procesandoFaseAutomatica = true;
        Debug.Log("<color=red>--- FASE 5: RESOLUCIÓN DE COMBATES (AUTOMÁTICA) ---</color>");
        ultimosReportesCombate.Clear();

        if (ataquesDeclarados.Count == 0)
        {
            string sinCombates = "No se declararon ataques en esta fase.";
            Debug.Log(sinCombates);
            ultimosReportesCombate.Add(sinCombates);
        }
        else
        {
            Debug.Log($"Resolviendo {ataquesDeclarados.Count} combate(s) de manera automática...");

            foreach (DeclaracionAtaque decl in new List<DeclaracionAtaque>(ataquesDeclarados))
            {
                Unidad atacante = decl.Atacante;
                Unidad objetivo = decl.Objetivo;

                if (atacante == null || objetivo == null) continue;
                if (atacante.CeldaActual == null || objetivo.CeldaActual == null) continue;

                // 1. Calcular ataque con bonificación de terreno
                int poderAtaque = ObtenerPuntosAtaque(atacante);
                TipoTerreno terrenoFav = ObtenerTerrenoFavorito(atacante);
                if (atacante.CeldaActual.terreno == terrenoFav)
                {
                    poderAtaque += 1;
                }

                // 2. Calcular defensa con bonificación de terreno
                int bonusDefensa = objetivo.CeldaActual.bonusDefensa;
                int danio = Mathf.Max(1, poderAtaque - (bonusDefensa > 0 ? 1 : 0));

                objetivo.puntosDefensa -= danio;

                string reporte = $"⚔️ {atacante.name} inflige {danio} de daño a {objetivo.name} (Defensa restante: {Mathf.Max(0, objetivo.puntosDefensa)}).";
                Debug.Log(reporte);
                ultimosReportesCombate.Add(reporte);

                // 3. Comprobar baja o contraataque
                if (objetivo.puntosDefensa <= 0)
                {
                    string muerte = $"💀 ¡{objetivo.name} ha sido derrotada!";
                    Debug.Log(muerte);
                    ultimosReportesCombate.Add(muerte);
                    Destroy(objetivo.gameObject);
                }
                else
                {
                    // Si sobrevive y está a rango, contraataca
                    if (EstaEnRangoAtaque(objetivo, atacante))
                    {
                        int contraAtaque = Mathf.Max(1, ObtenerPuntosAtaque(objetivo) / 2);
                        atacante.puntosDefensa -= contraAtaque;

                        string contra = $"🛡️ {objetivo.name} contraataca infligiendo {contraAtaque} de daño a {atacante.name}!";
                        Debug.Log(contra);
                        ultimosReportesCombate.Add(contra);

                        if (atacante.puntosDefensa <= 0)
                        {
                            string muerteAtacante = $"💀 ¡{atacante.name} cayó por contraataque!";
                            Debug.Log(muerteAtacante);
                            ultimosReportesCombate.Add(muerteAtacante);
                            Destroy(atacante.gameObject);
                        }
                    }
                }

                yield return new WaitForSeconds(0.3f);
            }
        }

        ataquesDeclarados.Clear();
        OnCombatesResueltos?.Invoke(new List<string>(ultimosReportesCombate));

        yield return new WaitForSeconds(pausaFaseAutomatica);

        procesandoFaseAutomatica = false;

        // Pasar al siguiente jugador o siguiente ronda
        FinalizarTurnoJugador();
    }

    // -------------------------------------------------------------
    // HELPERS DE REFLECTION PARA UNIDAD
    // -------------------------------------------------------------

    public static int ObtenerPuntosAtaque(Unidad u)
    {
        if (u == null) return 1;
        if (campoPuntosAtaque != null)
        {
            return (int)campoPuntosAtaque.GetValue(u);
        }
        return 1;
    }

    public static int ObtenerAlcanceAtaque(Unidad u)
    {
        if (u == null) return 1;
        if (campoAlcanceAtaque != null)
        {
            return (int)campoAlcanceAtaque.GetValue(u);
        }
        return 1;
    }

    public static TipoTerreno ObtenerTerrenoFavorito(Unidad u)
    {
        if (u == null) return TipoTerreno.Llanura;
        if (campoTerrenoFavorito != null)
        {
            return (TipoTerreno)campoTerrenoFavorito.GetValue(u);
        }
        return TipoTerreno.Llanura;
    }

    // -------------------------------------------------------------
    // INTERFAZ DE USUARIO INTEGRADA (OnGUI)
    // -------------------------------------------------------------

    private void OnGUI()
    {
        // Panel Principal de Turnos y Fases
        GUILayout.BeginArea(new Rect(10, 10, 360, 290), GUI.skin.box);

        string rolTexto = TurnoDeIA ? $"<color=red>IA {JugadorActual}</color>" : "<color=cyan>Tú (Humano)</color>";
        GUILayout.Label($"<b>Ronda {NumeroTurno}</b> | Turno: <b>Jugador {JugadorActual + 1}</b> ({rolTexto})");

        // Indicador visual de la fase
        Color colorFase = Color.white;
        string nombreFase = "";
        switch (FaseActual)
        {
            case FaseTurno.Recursos:
                colorFase = Color.green;
                nombreFase = "1. RECURSOS (Automática)";
                break;
            case FaseTurno.Reclutamiento:
                colorFase = new Color(0.4f, 0.8f, 1f);
                nombreFase = "2. RECLUTAMIENTO";
                break;
            case FaseTurno.Movimiento:
                colorFase = Color.yellow;
                nombreFase = "3. MOVIMIENTO";
                break;
            case FaseTurno.Ataque:
                colorFase = new Color(1f, 0.4f, 0.4f);
                nombreFase = "4. ATAQUE (Decisión)";
                break;
            case FaseTurno.ResolucionCombate:
                colorFase = new Color(1f, 0.6f, 0.1f);
                nombreFase = "5. RESOLUCIÓN DE COMBATE (Automática)";
                break;
        }

        GUI.color = colorFase;
        GUILayout.Label($"<b>Fase: {nombreFase}</b>");
        GUI.color = Color.white;

        // Recursos del jugador
        int monedas = ObtenerMonedas(JugadorActual);
        int madera = ObtenerRecurso(JugadorActual, TipoRecurso.Madera);
        int oro = ObtenerRecurso(JugadorActual, TipoRecurso.Oro);
        int comida = ObtenerRecurso(JugadorActual, TipoRecurso.Comida);
        GUILayout.Label($"💰 Monedas: {monedas} | 🌲 Madera: {madera} | ⛏️ Oro: {oro} | 🍞 Comida: {comida}");

        GUILayout.Space(6);

        // Instrucciones según la fase activa
        switch (FaseActual)
        {
            case FaseTurno.Recursos:
                GUILayout.Label("<i>Distribuyendo recursos a todos los participantes...</i>");
                break;

            case FaseTurno.Reclutamiento:
                GUILayout.Label("<i>Recluta tropas en tu cuartel. Movimiento desactivado.</i>");
                break;

            case FaseTurno.Movimiento:
                GUILayout.Label("<i>Mueve tus unidades hasta agotar sus puntos.</i>");
                if (Unidad.Seleccionada != null && Unidad.Seleccionada.jugadorId == JugadorActual)
                {
                    int pts = puntosMovimientoRestantes.ContainsKey(Unidad.Seleccionada) ? puntosMovimientoRestantes[Unidad.Seleccionada] : 0;
                    GUILayout.Label($"Unidad: <b>{Unidad.Seleccionada.name}</b> (Movimiento: {pts})");
                }
                break;

            case FaseTurno.Ataque:
                GUILayout.Label("<i>Decide si atacar o no a enemigos a rango.</i>");
                DibujarSeccionAtaqueGUI();
                break;

            case FaseTurno.ResolucionCombate:
                GUILayout.Label("<i>Resolviendo combates automáticamente...</i>");
                break;
        }

        GUILayout.Space(6);

        // Botón de avanzar fase
        GUI.enabled = !TurnoDeIA && !procesandoFaseAutomatica;
        string textoBoton = FaseActual switch
        {
            FaseTurno.Reclutamiento => "Avanzar a Movimiento (Espacio)",
            FaseTurno.Movimiento => "Avanzar a Ataque (Espacio)",
            FaseTurno.Ataque => "Resolver Combates (Espacio)",
            _ => "Siguiente Fase (Espacio)"
        };

        if (GUILayout.Button(textoBoton, GUILayout.Height(32)))
        {
            AvanzarFase();
        }
        GUI.enabled = true;

        GUILayout.EndArea();
    }

    private void DibujarSeccionAtaqueGUI()
    {
        if (Unidad.Seleccionada != null && Unidad.Seleccionada.jugadorId == JugadorActual)
        {
            Unidad atacante = Unidad.Seleccionada;
            int alcance = ObtenerAlcanceAtaque(atacante);
            int poder = ObtenerPuntosAtaque(atacante);

            GUILayout.Label($"Atacante: <b>{atacante.name}</b> (Poder: {poder}, Alcance: {alcance})");

            // Buscar enemigos a rango
            List<Unidad> enemigosEnRango = new List<Unidad>();
            foreach (Unidad u in Unidad.Todas)
            {
                if (u != null && u.jugadorId != JugadorActual && EstaEnRangoAtaque(atacante, u))
                {
                    enemigosEnRango.Add(u);
                }
            }

            if (enemigosEnRango.Count == 0)
            {
                GUILayout.Label("<i>No hay enemigos a rango de esta unidad.</i>");
            }
            else
            {
                foreach (Unidad enemigo in enemigosEnRango)
                {
                    bool yaDeclarado = TieneAtaqueDeclarado(atacante) && ObtenerObjetivoDeclarado(atacante) == enemigo;
                    string texto = yaDeclarado ? $"Atacando a {enemigo.name} (Def: {enemigo.puntosDefensa}) [Declarado]" : $"Atacar a {enemigo.name} (Def: {enemigo.puntosDefensa})";

                    if (GUILayout.Button(texto))
                    {
                        if (yaDeclarado)
                        {
                            CancelarAtaque(atacante);
                        }
                        else
                        {
                            DeclararAtaque(atacante, enemigo);
                        }
                    }
                }
            }
        }
        else
        {
            GUILayout.Label("<i>Selecciona una unidad aliada para ver a quién puede atacar.</i>");
        }

        if (ataquesDeclarados.Count > 0)
        {
            GUILayout.Label($"Ataques programados: {ataquesDeclarados.Count}");
        }
    }
}
