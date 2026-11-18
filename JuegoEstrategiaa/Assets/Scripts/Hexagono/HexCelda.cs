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

public enum Propietario
{
    Jugador,
    IA,
    Nadie
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

    public Propietario propietario = Propietario.Nadie;
    public int influenciaTerreno = 0;

    private SpriteRenderer spriteRenderer;
    private Color colorOriginal;
    private bool estaSeleccionada = false;
    private bool estaSobreVolando = false;
    private bool estaEnRango = false;
    private bool mostrarInfluencia = false;
    private Color colorInfluencia;

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
                influenciaTerreno = 0;
                colorOriginal = DesdeHex("#96A159");
                break;
            case TipoTerreno.Montana:
                costeMovimiento = 3;
                bonusDefensa = 1;
                influenciaTerreno = 0;
                colorOriginal = DesdeHex("#716C6E");
                break;
            case TipoTerreno.Agua:
                costeMovimiento = 999;
                bonusDefensa = 0;
                influenciaTerreno = 0;
                colorOriginal = DesdeHex("#5ABB99");
                break;
            case TipoTerreno.Cuartel:
                costeMovimiento = 1;
                bonusDefensa = 1;
                influenciaTerreno = 5;
                colorOriginal = DesdeHex("#F8EDCA");
                break;
            case TipoTerreno.Bandera:
                costeMovimiento = 0;
                bonusDefensa = 2;
                puntosVictoria = 1;
                influenciaTerreno = 8;
                colorOriginal = DesdeHex("#F8EDCA");
                break;
            case TipoTerreno.BanderaCentral:
                costeMovimiento = 0;
                bonusDefensa = 4;
                puntosVictoria = 2;
                influenciaTerreno = 10;
                colorOriginal = DesdeHex("#F8EDCA");
                break;
            default: // Llanura / Desierto
                costeMovimiento = 1;
                bonusDefensa = 0;
                influenciaTerreno = 0;
                colorOriginal = DesdeHex("#E6C378");
                break;
        }

        ActualizarVisualizacion();
    }

    // Convierte un color escrito en hexadecimal ("#RRGGBB") en un Color de Unity
    private static Color DesdeHex(string hex)
    {
        Color color;
        ColorUtility.TryParseHtmlString(hex, out color);
        return color;
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

    public void EstablecerColorInfluencia(Color color)
    {
        mostrarInfluencia = true;
        colorInfluencia = color;
        ActualizarVisualizacion();
    }

    public void LimpiarColorInfluencia()
    {
        mostrarInfluencia = false;
        ActualizarVisualizacion();
    }

    /// <summary>
    /// Prioridad visual: Selecci�n > Hover > En Rango > Mapa de Influencia > Normal
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
            spriteRenderer.color = Color.Lerp(colorOriginal, Color.cyan, 0.5f);
        }
        else if (mostrarInfluencia)
        {
            spriteRenderer.color = colorInfluencia;
        }
        else
        {
            spriteRenderer.color = colorOriginal;
        }
    }
}