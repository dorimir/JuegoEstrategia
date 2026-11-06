using UnityEngine;

// Tipos de terreno definidos en la especificación[cite: 1]
public enum TipoTerreno
{
    Llanura,
    Bosque,
    Montana,
    Agua,
    Desierto
}

public enum TipoRecurso
{
    Ninguno,
    Madera,
    Oro,
    Comida
}

public class HexCelda : MonoBehaviour
{
    [Header("Coordenadas Axiales")]
    public int q;
    public int r;

    [Header("Propiedades Tácticas")]
    public TipoTerreno terreno;
    public TipoRecurso recurso = TipoRecurso.Ninguno;
    public int costeMovimiento = 1;
    public int bonusDefensa = 0;
    public bool esEstructuraProduccion = false;

    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;
    private bool estaSeleccionada = false;
    private bool estaSobreVolando = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void ConfigurarCelda(int q, int r, TipoTerreno terreno)
    {
        this.q = q;
        this.r = r;
        this.terreno = terreno;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        // Asignación de colores según el terreno[cite: 1]
        switch (terreno)
        {
            case TipoTerreno.Bosque:
                costeMovimiento = 2;
                bonusDefensa = 2;
                colorOriginal = new Color(0.2f, 0.7f, 0.2f); // Verde bosque
                break;
            case TipoTerreno.Montana:
                costeMovimiento = 3;
                bonusDefensa = 4;
                colorOriginal = new Color(0.5f, 0.5f, 0.5f); // Gris montaña
                break;
            case TipoTerreno.Agua:
                costeMovimiento = 999;
                bonusDefensa = 0;
                colorOriginal = new Color(0.2f, 0.4f, 0.8f); // Azul agua
                break;
            default: // Llanura
                costeMovimiento = 1;
                bonusDefensa = 0;
                colorOriginal = new Color(0.8f, 0.8f, 0.4f); // Amarillo/Tierra
                break;
        }

        spriteRenderer.color = colorOriginal;
    }

    // Detecta cuando el ratón ENTRA sobre la casilla
    private void OnMouseEnter()
    {
        estaSobreVolando = true;
        ActualizarVisualizacion();
    }

    // Detecta cuando el ratón SALE de la casilla
    private void OnMouseExit()
    {
        estaSobreVolando = false;
        ActualizarVisualizacion();
    }

    // Detecta el clic del ratón sobre la casilla
    private void OnMouseDown()
    {
        SelectionManager.Instancia.SeleccionarCelda(this);
    }

    public void EstablecerSeleccionada(bool seleccionada)
    {
        estaSeleccionada = seleccionada;
        ActualizarVisualizacion();
    }

    /// <summary>
    /// Gestiona la prioridad visual: Selección > Hover > Color Original
    /// </summary>
    private void ActualizarVisualizacion()
    {
        if (estaSeleccionada)
        {
            // Color de selección: Blanco brillante
            spriteRenderer.color = Color.white;
        }
        else if (estaSobreVolando)
        {
            // Color de Hover: Un todo más claro/brillante que el original
            spriteRenderer.color = colorOriginal * 1.35f;
        }
        else
        {
            // Estado normal: Color del terreno
            spriteRenderer.color = colorOriginal;
        }
    }
}
