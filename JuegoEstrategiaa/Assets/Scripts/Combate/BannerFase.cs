using UnityEngine;

/// <summary>
/// Muestra un título grande en la parte alta de la pantalla cada vez que cambia la fase
/// durante el turno del jugador humano: "RECLUTAMIENTO", "MOVIMIENTO", "ATAQUE"...
/// En los turnos de las IAs NO se muestra nada.
/// Se crea solo al arrancar la escena y solo LEE el estado del TurnManager,
/// así que no hace falta modificar ni conectar nada más.
/// </summary>
public class BannerFase : MonoBehaviour
{
    [Header("Tiempos (segundos)")]
    public float duracion = 1.8f;
    public float fundidoEntrada = 0.25f;
    public float fundidoSalida = 0.4f;

    [Header("Opciones")]
    [Tooltip("Altura del banner: 0 = borde superior de la pantalla, 0.5 = centro, 1 = borde inferior.")]
    [Range(0.05f, 0.95f)]
    public float posicionVertical = 0.15f;
    public int tamanoTitulo = 64;
    public int tamanoSubtitulo = 24;

    // Estado observado del TurnManager
    private bool inicializado;
    private FaseTurno faseVista;
    private int jugadorVisto = -1;
    private int turnoVisto = -1;

    // Estado del banner actual
    private string titulo = "";
    private string subtitulo = "";
    private Color color = Color.white;
    private float inicio = -999f;

    private GUIStyle estiloTitulo;
    private GUIStyle estiloSubtitulo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CrearAutomaticamente()
    {
        if (FindFirstObjectByType<BannerFase>() == null)
        {
            new GameObject("BannerFase").AddComponent<BannerFase>();
        }
    }

    private void Update()
    {
        TurnManager tm = TurnManager.Instancia;
        if (tm == null) return;

        bool cambio = !inicializado
                      || tm.FaseActual != faseVista
                      || tm.JugadorActual != jugadorVisto
                      || tm.NumeroTurno != turnoVisto;
        if (!cambio) return;

        inicializado = true;
        faseVista = tm.FaseActual;
        jugadorVisto = tm.JugadorActual;
        turnoVisto = tm.NumeroTurno;

        // Solo el turno del jugador humano: las IAs no muestran banner
        if (tm.TurnoDeIA) return;

        Mostrar(tm);
    }

    private void Mostrar(TurnManager tm)
    {
        switch (tm.FaseActual)
        {
            case FaseTurno.Recursos:
                titulo = "RECURSOS";
                color = new Color(0.35f, 0.9f, 0.4f);
                break;
            case FaseTurno.Reclutamiento:
                titulo = "RECLUTAMIENTO";
                color = new Color(0.4f, 0.8f, 1f);
                break;
            case FaseTurno.Movimiento:
                titulo = "MOVIMIENTO";
                color = new Color(1f, 0.9f, 0.3f);
                break;
            case FaseTurno.Ataque:
                titulo = "ATAQUE";
                color = new Color(1f, 0.4f, 0.4f);
                break;
            case FaseTurno.ResolucionCombate:
                titulo = "COMBATE";
                color = new Color(1f, 0.6f, 0.15f);
                break;
            default:
                titulo = tm.FaseActual.ToString().ToUpper();
                color = Color.white;
                break;
        }

        subtitulo = $"Ronda {tm.NumeroTurno}  -  Tu turno";

        inicio = Time.unscaledTime;
    }

    private void PrepararEstilos()
    {
        if (estiloTitulo == null)
        {
            estiloTitulo = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            estiloSubtitulo = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Normal
            };
        }
        estiloTitulo.fontSize = tamanoTitulo;
        estiloSubtitulo.fontSize = tamanoSubtitulo;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        float t = Time.unscaledTime - inicio;
        if (t < 0f || t > duracion) return;

        PrepararEstilos();
        GUI.depth = -100; // por encima del resto de la interfaz

        float entrada = Mathf.Clamp01(t / Mathf.Max(0.01f, fundidoEntrada));
        float salida = Mathf.Clamp01((duracion - t) / Mathf.Max(0.01f, fundidoSalida));
        float alpha = entrada * salida;
        float escala = Mathf.Lerp(0.85f, 1f, entrada);

        float cx = Screen.width * 0.5f;
        float cy = Screen.height * posicionVertical;

        Matrix4x4 matrizPrevia = GUI.matrix;
        Color colorPrevio = GUI.color;

        GUIUtility.ScaleAroundPivot(new Vector2(escala, escala), new Vector2(cx, cy));

        // Banda oscura a todo el ancho con filetes del color de la fase
        float altoBanda = tamanoTitulo * 2.4f;
        Rect banda = new Rect(0f, cy - altoBanda * 0.5f, Screen.width, altoBanda);

        GUI.color = new Color(0f, 0f, 0f, 0.65f * alpha);
        GUI.DrawTexture(banda, Texture2D.whiteTexture);

        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.DrawTexture(new Rect(0f, banda.y, Screen.width, 3f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, banda.yMax - 3f, Screen.width, 3f), Texture2D.whiteTexture);

        // Título (con sombra) y subtítulo
        Rect rTitulo = new Rect(0f, cy - tamanoTitulo * 0.95f, Screen.width, tamanoTitulo * 1.4f);
        Rect rSub = new Rect(0f, cy + tamanoTitulo * 0.40f, Screen.width, tamanoSubtitulo * 1.8f);

        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.Label(new Rect(rTitulo.x + 3f, rTitulo.y + 3f, rTitulo.width, rTitulo.height), titulo, estiloTitulo);

        GUI.color = new Color(color.r, color.g, color.b, alpha);
        GUI.Label(rTitulo, titulo, estiloTitulo);

        GUI.color = new Color(1f, 1f, 1f, 0.9f * alpha);
        GUI.Label(rSub, subtitulo, estiloSubtitulo);

        GUI.matrix = matrizPrevia;
        GUI.color = colorPrevio;
    }
}