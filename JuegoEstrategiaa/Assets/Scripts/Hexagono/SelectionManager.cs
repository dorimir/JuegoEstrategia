using UnityEngine;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instancia { get; private set; }

    [Header("Estado de Selección")]
    public HexCelda celdaSeleccionada;

    private void Awake()
    {
        if (Instancia == null)
        {
            Instancia = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SeleccionarCelda(HexCelda nuevaCelda)
    {
        // Si clicamos la misma casilla que ya está seleccionada, la deseleccionamos
        if (celdaSeleccionada == nuevaCelda)
        {
            celdaSeleccionada.EstablecerSeleccionada(false);
            celdaSeleccionada = null;
            Debug.Log("Casilla deseleccionada.");
            return;
        }

        // Deseleccionar la casilla previa si existía
        if (celdaSeleccionada != null)
        {
            celdaSeleccionada.EstablecerSeleccionada(false);
        }

        // Seleccionar la nueva casilla
        celdaSeleccionada = nuevaCelda;
        celdaSeleccionada.EstablecerSeleccionada(true);

        // Imprimir información para depuración o uso por la IA/HUD[cite: 1]
        Debug.Log($"Celda Seleccionada: ({celdaSeleccionada.q}, {celdaSeleccionada.r}) | " +
                  $"Terreno: {celdaSeleccionada.terreno} | " +
                  $"Coste Movimiento: {celdaSeleccionada.costeMovimiento} | " +
                  $"Bonus Defensa: {celdaSeleccionada.bonusDefensa}");
    }
}