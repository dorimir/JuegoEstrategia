using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuSpawn : MonoBehaviour
{
    public static MenuSpawn Instancia;

    [SerializeField] private GameObject panel;
    [SerializeField] private MonedasYBanderas monedas;
    [SerializeField] private HexGridGenerator tablero;
    [SerializeField] private GameObject[] botonesJugador;
    [SerializeField] private GameObject[] botonesIA;

    private List<HexCelda> celdasDisponibles = new List<HexCelda>();
    private HexCelda celdaCuartel;   // el cuartel que ha abierto el menu
    private HexCelda celdaElegida;

    private void Awake()
    {
        Instancia = this;
        panel.SetActive(false);
    }

    public void Abrir(Cuartel cuartel)
    {
        LimpiarIluminadas();    // por si ya habia uno abierto
        Unidad.Deseleccionar(); // que ninguna unidad se mueva mientras elegimos

        celdaCuartel = cuartel.GetComponent<HexCelda>();
        Vector2Int coordenada = new Vector2Int(celdaCuartel.q, celdaCuartel.r);

        foreach (HexCelda vecina in HexGridUtils.ObtenerVecinos(coordenada, tablero.ObtenerMapaCeldas()))
        {
            bool libre = vecina.costeMovimiento < 999 && Unidad.EnCelda(vecina) == null;
            if (libre)
            {
                celdasDisponibles.Add(vecina);
                vecina.EstablecerEnRango(true);
            }
        }
    }

    public void AbrirParaIA(List<HexCelda> celdasValidas, HexCelda celdaSeleccionada)
    {
        LimpiarIluminadas();
        Unidad.Deseleccionar();

        if (celdasValidas != null)
        {
            foreach (HexCelda celda in celdasValidas)
            {
                if (celda != null && celda.costeMovimiento < 999 && Unidad.EnCelda(celda) == null)
                {
                    celdasDisponibles.Add(celda);
                    celda.EstablecerEnRango(true);
                }
            }
        }

        celdaElegida = celdaSeleccionada;
        ActualizarBotonesSegunTurno();

        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    // Mientras hay celdas iluminadas, miramos que celda esta seleccionada:
    // - una iluminada  -> se muestra el panel
    // - el cuartel     -> se oculta el panel, pero las celdas siguen iluminadas
    // - otra o ninguna -> se apaga todo
    private void Update()
    {
        if (celdasDisponibles.Count == 0) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return; // el raton esta sobre la UI

        HexCelda seleccionada = SelectionManager.Instancia.celdaSeleccionada;

        if (celdasDisponibles.Contains(seleccionada))
        {
            celdaElegida = seleccionada;
            ActualizarBotonesSegunTurno();
            panel.SetActive(true);
        }
        else if (seleccionada == celdaCuartel)
        {
            celdaElegida = null;
            panel.SetActive(false);
        }
        else
        {
            LimpiarIluminadas();
        }
    }

    // Lo llaman los botones de tropa (BotonTropa) con el prefab que compran
    public void Spawnear(Unidad prefab)
    {
        int idJugadorActual = TurnManager.Instancia != null ? TurnManager.Instancia.JugadorActual : 0;
        if (SpawnManager.Instancia.SpawnearUnidad(prefab, celdaElegida, idJugadorActual))
        {
            Cerrar();
        }
    }

    // Se usa al crear la tropa
    public void Cerrar()
    {
        LimpiarIluminadas();

        // Quitamos tambien el blanco de la celda seleccionada
        if (SelectionManager.Instancia != null && SelectionManager.Instancia.celdaSeleccionada != null)
        {
            SelectionManager.Instancia.celdaSeleccionada.EstablecerSeleccionada(false);
            SelectionManager.Instancia.celdaSeleccionada = null;
        }
    }

    // Apaga las celdas iluminadas y oculta el panel (sin tocar la seleccion)
    private void LimpiarIluminadas()
    {
        foreach (HexCelda celda in celdasDisponibles)
        {
            celda.EstablecerEnRango(false);
        }
        celdasDisponibles.Clear();
        celdaCuartel = null;
        celdaElegida = null;
        panel.SetActive(false);
    }
    public Unidad ObtenerUnidadAleatoriaIA()
    {
        if (botonesIA == null || botonesIA.Length == 0) return null;

        List<Unidad> opciones = new List<Unidad>();
        foreach (GameObject btnObj in botonesIA)
        {
            if (btnObj == null) continue;

            // Ajusta 'BotonTropa' si el script del botón tiene otro nombre o campo para el prefab
            BotonTropa boton = btnObj.GetComponent<BotonTropa>();
            if (boton != null && boton.tropa != null)
            {
                opciones.Add(boton.tropa);
            }
        }

        if (opciones.Count == 0) return null;
        return opciones[UnityEngine.Random.Range(0, opciones.Count)];
    }
    private void ActualizarBotonesSegunTurno()
    {
        bool esTurnoIA = TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA;

        if (botonesJugador != null)
        {
            foreach (GameObject btn in botonesJugador)
            {
                if (btn != null) btn.SetActive(!esTurnoIA);
            }
        }

        if (botonesIA != null)
        {
            foreach (GameObject btn in botonesIA)
            {
                if (btn != null) btn.SetActive(esTurnoIA);
            }
        }
    }
}
