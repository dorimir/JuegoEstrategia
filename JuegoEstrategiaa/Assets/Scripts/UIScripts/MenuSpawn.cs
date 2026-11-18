using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuSpawn : MonoBehaviour
{
    public static MenuSpawn Instancia;

    [SerializeField] private GameObject panel;
    [SerializeField] private MonedasYBanderas monedas;
    [SerializeField] private HexGridGenerator tablero;

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

    // Mientras hay celdas iluminadas, miramos que celda esta seleccionada:
    // - una iluminada  -> se muestra el panel
    // - el cuartel     -> se oculta el panel, pero las celdas siguen iluminadas
    // - otra o ninguna -> se apaga todo
    private void Update()
    {
        if (celdasDisponibles.Count == 0) return;
        if (EventSystem.current.IsPointerOverGameObject()) return; // el raton esta sobre la UI

        HexCelda seleccionada = SelectionManager.Instancia.celdaSeleccionada;

        if (celdasDisponibles.Contains(seleccionada))
        {
            celdaElegida = seleccionada;
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
        if (!monedas.Gastar(prefab.Coste))
        {
            Debug.Log("No tienes monedas suficientes (" + prefab.Coste + ").");
            return;
        }

        Unidad nueva = Instantiate(prefab, celdaElegida.transform.position + Vector3.back, Quaternion.identity);
        nueva.EstablecerCoordenadaInicial(new Vector2Int(celdaElegida.q, celdaElegida.r));

        Cerrar();
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
}
