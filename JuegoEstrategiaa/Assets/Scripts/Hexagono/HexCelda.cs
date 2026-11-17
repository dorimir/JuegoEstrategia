using UnityEngine;

// Tipos de terreno definidos en la especificacion
public enum TipoTerreno
{
    Llanura,
    Bosque,
    Montana,
    Agua,
    Desierto,
    Cuartel,
    Bandera,
    BanderaCentral
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

    [Header("Propiedades Tacticas")]
    public TipoTerreno terreno;
    public TipoRecurso recurso = TipoRecurso.Ninguno;
    public int costeMovimiento = 1;
    public int bonusDefensa = 0;
    public int puntosVictoria = 0;
    public bool esEstructuraProduccion = false;

    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;
    private bool estaSeleccionada = false;
    private bool estaSobreVolando = false;
    private bool estaEnRango = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnValidate()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        ConfigurarCelda(q, r, terreno);
    }

    public void ConfigurarCelda(int q, int r, TipoTerreno terreno)
    {
        this.q = q;
        this.r = r;
        this.terreno = terreno;

        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        switch (terreno)
        {
            case TipoTerreno.Bosque:
                costeMovimiento = 2;
                bonusDefensa = 1;
                colorOriginal = new Color(0.2f, 0.7f, 0.2f);
                break;
            case TipoTerreno.Montana:
                costeMovimiento = 3;
                bonusDefensa = 1;
                colorOriginal = new Color(0.5f, 0.5f, 0.5f);
                break;
            case TipoTerreno.Agua:
                costeMovimiento = 999;
                bonusDefensa = 0;
                colorOriginal = new Color(0.2f, 0.4f, 0.8f);
                break;
            case TipoTerreno.Cuartel:
                costeMovimiento = 1;
                bonusDefensa = 1;
                colorOriginal = new Color(0.0f, 0.0f, 0.0f);
                break;
            case TipoTerreno.Bandera:
                costeMovimiento = 0;
                bonusDefensa = 2;
                puntosVictoria = 1;
                colorOriginal = new Color(1.0f, 0.0f, 0.0f);
                break;
            case TipoTerreno.BanderaCentral:
                costeMovimiento = 0;
                bonusDefensa = 4;
                puntosVictoria = 2;
                colorOriginal = new Color(1.0f, 0.0f, 0.2f);
                break;
            default: // Llanura / Desierto
                costeMovimiento = 1;
                bonusDefensa = 0;
                colorOriginal = new Color(0.8f, 0.8f, 0.4f);
                break;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = colorOriginal;
        }
    }

    private void OnMouseEnter()
    {
        estaSobreVolando = true;
        ActualizarVisualizacion();
    }

    private void OnMouseExit()
    {
        estaSobreVolando = false;
        ActualizarVisualizacion();
    }

    private void OnMouseDown()
    {
        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA) return;

        if (SelectionManager.Instancia != null)
        {
            SelectionManager.Instancia.SeleccionarCelda(this);
        }

        Unidad unidadAqui = Unidad.EnCelda(this);
        if (unidadAqui != null)
        {
            if (TurnManager.Instancia == null || unidadAqui.jugadorId == TurnManager.Instancia.JugadorActual)
            {
                Unidad.SeleccionarUnidad(unidadAqui);
            }
            return;
        }

        if (Unidad.Seleccionada != null)
        {
            Unidad.Seleccionada.IrHacia(this);
        }
    }

    public void EstablecerSeleccionada(bool seleccionada)
    {
        estaSeleccionada = seleccionada;
        ActualizarVisualizacion();
    }

    public void EstablecerEnRango(bool enRango)
    {
        estaEnRango = enRango;
        ActualizarVisualizacion();
    }

    /// <summary>
    /// Prioridad visual: Selección > Hover > En Rango > Normal
    /// </summary>
    private void ActualizarVisualizacion()
    {
        if (spriteRenderer == null) return;

        if (estaSeleccionada)
        {
            spriteRenderer.color = Color.white;
        }
        else if (estaSobreVolando)
        {
            spriteRenderer.color = colorOriginal * 1.35f;
        }
        else if (estaEnRango)
        {
            // Color para casillas alcanzables (tinte cian/brillante)
            spriteRenderer.color = Color.Lerp(colorOriginal, Color.cyan, 0.5f);
        }
        else
        {
            spriteRenderer.color = colorOriginal;
        }
    }
}