using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class MenuSpawn : MonoBehaviour
{
    public static MenuSpawn Instancia;

    [SerializeField] private GameObject panel;
    [SerializeField] private Unidad[] tropas;              // prefabs: el boton 0 crea tropas[0], el 1 tropas[1]...
    [SerializeField] private MonedasYBanderas monedas;
    [SerializeField] private HexGridGenerator tablero;

    private List<HexCelda> celdasDisponibles = new List<HexCelda>();
    private HexCelda celdaElegida;

    private void Awake()
    {
        Instancia = this;
        panel.SetActive(false);
    }

    public void Abrir(Cuartel cuartel)
    {
        Cerrar();               // por si ya habia uno abierto
        Unidad.Deseleccionar(); // que ninguna unidad se mueva mientras elegimos

        HexCelda celdaCuartel = cuartel.GetComponent<HexCelda>();
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
    // si es una de las iluminadas se muestra el panel; si no (o se deselecciona), se oculta
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
        else
        {
            celdaElegida = null;
            panel.SetActive(false);
        }
    }

    public void Spawnear(int indiceTropa)
    {
        Unidad prefab = tropas[indiceTropa];

        if (!monedas.Gastar(prefab.Coste))
        {
            Debug.Log("No tienes monedas suficientes (" + prefab.Coste + ").");
            return;
        }

        Unidad nueva = Instantiate(prefab, celdaElegida.transform.position + Vector3.back, Quaternion.identity);
        nueva.EstablecerCoordenadaInicial(new Vector2Int(celdaElegida.q, celdaElegida.r));

        Cerrar();
    }

    // Lo llama el boton Cancelar, y tambien se usa al terminar
    public void Cerrar()
    {
        foreach (HexCelda celda in celdasDisponibles)
        {
            celda.EstablecerEnRango(false);
        }
        celdasDisponibles.Clear();
        celdaElegida = null;

        // Quitamos tambien el blanco de la celda seleccionada
        if (SelectionManager.Instancia != null && SelectionManager.Instancia.celdaSeleccionada != null)
        {
            SelectionManager.Instancia.celdaSeleccionada.EstablecerSeleccionada(false);
            SelectionManager.Instancia.celdaSeleccionada = null;
        }

        panel.SetActive(false);
    }
}
