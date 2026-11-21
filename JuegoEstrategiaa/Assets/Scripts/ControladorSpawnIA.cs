using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ControladorSpawnIA : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private HexGridGenerator tablero;
    [SerializeField] private MenuSpawn menuSpawn;
    [SerializeField] private SpawnManager spawnManager;

    [Header("Configuración IA")]
    [SerializeField] private int aiJugadorId = 1;
    [SerializeField] private Vector2Int coordenadaPuntoRojo = new Vector2Int(7, 0);
    [SerializeField] private List<Unidad> unidadesDisponiblesIA = new List<Unidad>();
    [SerializeField] private float retrasoVisualMenu = 0.4f;
    [SerializeField] private bool cobrarMonedasEnPruebas = false;

    [Header("Transición a GOAP")]
    [Tooltip("True mientras se prueban decisiones aleatorias; cambiar a false cuando GOAP decida celda y unidad.")]
    [SerializeField] private bool usarDecisionAleatoria = true;

    private readonly List<HexCelda> celdasIluminadas = new List<HexCelda>();
    private bool suscrito = false;

    private void Awake()
    {
        if (tablero == null) tablero = FindFirstObjectByType<HexGridGenerator>();
        if (menuSpawn == null) menuSpawn = MenuSpawn.Instancia != null ? MenuSpawn.Instancia : FindFirstObjectByType<MenuSpawn>();
        if (spawnManager == null) spawnManager = SpawnManager.Instancia != null ? SpawnManager.Instancia : FindFirstObjectByType<SpawnManager>();
    }

    private void OnEnable()
    {
        IntentarSuscribir();
    }

    private void Start()
    {
        IntentarSuscribir();
    }

    private void OnDisable()
    {
        if (TurnManager.Instancia != null && suscrito)
        {
            TurnManager.Instancia.OnTurnoIniciado -= AlIniciarTurno;
            suscrito = false;
        }
        LimpiarCeldasIluminadas();
    }

    private void IntentarSuscribir()
    {
        if (!suscrito && TurnManager.Instancia != null)
        {
            TurnManager.Instancia.OnTurnoIniciado += AlIniciarTurno;
            suscrito = true;
        }
    }

    private void AlIniciarTurno(int jugadorActual, int numeroTurno)
    {
        if (jugadorActual != aiJugadorId) return;
        StopAllCoroutines();
        StartCoroutine(EjecutarFlujoSpawnIA());
    }

    public IEnumerator EjecutarFlujoSpawnIA()
    {
        LimpiarCeldasIluminadas();

        if (tablero == null || spawnManager == null)
        {
            FinalizarTurnoIA();
            yield break;
        }

        Dictionary<Vector2Int, HexCelda> mapa = tablero.ObtenerMapaCeldas();
        if (mapa == null || mapa.Count == 0)
        {
            FinalizarTurnoIA();
            yield break;
        }

        List<HexCelda> celdasCandidatas = ObtenerCeldasCandidatasAlrededorDelRojo(mapa);
        if (celdasCandidatas.Count == 0 || unidadesDisponiblesIA == null || unidadesDisponiblesIA.Count == 0)
        {
            Debug.LogWarning("[ControladorSpawnIA] No hay celdas candidatas libres alrededor del punto rojo o faltan unidadesDisponiblesIA.");
            FinalizarTurnoIA();
            yield break;
        }

        // 1. Iluminar celdas candidatas alrededor del punto rojo (7, 0)
        IluminarCeldasCandidatas(celdasCandidatas);

        // 2. Decidir celda (aleatoria temporalmente -> GOAP después)
        HexCelda celdaElegida = ElegirCeldaSpawn(celdasCandidatas);
        if (celdaElegida == null)
        {
            FinalizarTurnoIA();
            yield break;
        }

        // 3. Marcar la celda seleccionada y abrir el menú con los botones de IA
        if (SelectionManager.Instancia != null)
        {
            if (SelectionManager.Instancia.celdaSeleccionada != null)
            {
                SelectionManager.Instancia.celdaSeleccionada.EstablecerSeleccionada(false);
            }
            SelectionManager.Instancia.celdaSeleccionada = celdaElegida;
            celdaElegida.EstablecerSeleccionada(true);
        }

        if (menuSpawn != null)
        {
            menuSpawn.AbrirParaIA(celdasCandidatas, celdaElegida);
        }

        if (retrasoVisualMenu > 0f)
        {
            yield return new WaitForSeconds(retrasoVisualMenu);
        }

        // 4. Decidir unidad (aleatoria temporalmente -> GOAP después)
        Unidad unidadElegida = ElegirUnidadSpawn(unidadesDisponiblesIA, celdaElegida);
        if (unidadElegida == null)
        {
            FinalizarTurnoIA();
            yield break;
        }

        // 5. Ejecutar spawn reutilizando SpawnManager y cerrar el menú
        spawnManager.SpawnearUnidad(unidadElegida, celdaElegida, aiJugadorId, cobrarMonedasEnPruebas);

        FinalizarTurnoIA();
    }

    private void FinalizarTurnoIA()
    {
        LimpiarCeldasIluminadas();

        if (menuSpawn != null)
        {
            menuSpawn.Cerrar();
        }

        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA)
        {
            TurnManager.Instancia.FinalizarTurno();
        }
    }


    private void IluminarCeldasCandidatas(List<HexCelda> candidatas)
    {
        LimpiarCeldasIluminadas();

        foreach (HexCelda celda in candidatas)
        {
            if (celda == null) continue;
            celda.EstablecerEnRango(true);
            celdasIluminadas.Add(celda);
        }
    }

    private void LimpiarCeldasIluminadas()
    {
        foreach (HexCelda celda in celdasIluminadas)
        {
            if (celda != null)
            {
                celda.EstablecerEnRango(false);
            }
        }
        celdasIluminadas.Clear();
    }

    // --- PUNTO DE INTEGRACIÓN PARA GOAP ---
    private HexCelda ElegirCeldaSpawn(List<HexCelda> candidatos)
    {
        if (usarDecisionAleatoria)
        {
            return candidatos[Random.Range(0, candidatos.Count)];
        }

        // TODO (GOAP): Reemplazar por la celda elegida por el planificador GOAP
        return candidatos.FirstOrDefault();
    }

    // --- PUNTO DE INTEGRACIÓN PARA GOAP ---
    private Unidad ElegirUnidadSpawn(List<Unidad> opcionesFallback, HexCelda celdaDestino)
    {
        if (usarDecisionAleatoria)
        {
            if (menuSpawn != null)
            {
                Unidad desdeMenu = menuSpawn.ObtenerUnidadAleatoriaIA();
                if (desdeMenu != null) return desdeMenu;
            }

            if (opcionesFallback != null && opcionesFallback.Count > 0)
            {
                return opcionesFallback[UnityEngine.Random.Range(0, opcionesFallback.Count)];
            }
        }

        // TODO (GOAP): Reemplazar por la unidad elegida por GOAP según objetivo/estado
        return opcionesFallback != null ? opcionesFallback.FirstOrDefault() : null;
    }

    private List<HexCelda> ObtenerCeldasCandidatasAlrededorDelRojo(Dictionary<Vector2Int, HexCelda> mapa)
    {
        List<HexCelda> candidatas = new List<HexCelda>();

        if (mapa.TryGetValue(coordenadaPuntoRojo, out HexCelda puntoRojo) &&
            puntoRojo != null &&
            puntoRojo.costeMovimiento < 999 &&
            Unidad.EnCelda(puntoRojo) == null)
        {
            candidatas.Add(puntoRojo);
        }

        foreach (HexCelda vecina in HexGridUtils.ObtenerVecinos(coordenadaPuntoRojo, mapa))
        {
            if (vecina != null && vecina.costeMovimiento < 999 && Unidad.EnCelda(vecina) == null)
            {
                candidatas.Add(vecina);
            }
        }

        return candidatas;
    }

}