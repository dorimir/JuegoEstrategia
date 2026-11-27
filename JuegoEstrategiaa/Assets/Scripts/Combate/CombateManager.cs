using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resultado de simular un combate sin modificar nada (sirve para el preview y para la IA/GOAP).
/// </summary>
public struct ResultadoCombate
{
    public int ataqueAtacante;        // daño que el atacante inflige al defensor
    public int ataqueDefensor;        // daño que el defensor devuelve (0 si no contraataca)
    public bool hayContraataque;

    public int resistenciaDefensor;
    public int resistenciaAtacante;

    public int danioTotalDefensor;    // daño marcado en el defensor tras el combate
    public int danioTotalAtacante;    // daño marcado en el atacante tras el combate

    public bool defensorMuere;
    public bool atacanteMuere;
}

/// <summary>
/// Combate estilo Magic: Fuerza/Resistencia, daño marcado, daño simultáneo,
/// contraataque y muertes comprobadas todas juntas al final.
/// Se crea solo si no existe en la escena (no hace falta ponerlo a mano).
/// </summary>
public class CombateManager : MonoBehaviour
{
    // ------------------------------------------------------------------
    // Singleton con autocreación
    // ------------------------------------------------------------------
    private static CombateManager instancia;
    public static CombateManager Instancia
    {
        get
        {
            if (instancia == null)
            {
                instancia = FindFirstObjectByType<CombateManager>();
                if (instancia == null)
                {
                    GameObject go = new GameObject("CombateManager");
                    instancia = go.AddComponent<CombateManager>();
                }
            }
            return instancia;
        }
    }

    // ------------------------------------------------------------------
    // Configuración
    // ------------------------------------------------------------------
    [Header("Reglas")]
    [Tooltip("Si el defensor tiene al atacante dentro de SU alcance, le devuelve daño a la vez.")]
    public bool contraataqueActivo = true;

    [Tooltip("Fuerza extra cuando la unidad está sobre su terreno favorito.")]
    public int bonoAtaqueTerrenoFavorito = 1;

    [Tooltip("Máximo bono de Resistencia que da el terreno (BanderaCentral da +4: demasiado con números pequeños).")]
    public int topeBonoDefensaTerreno = 2;

    [Header("Tiempos de la resolución (segundos)")]
    public float pausaEntreCombates = 0.5f;
    public float pausaAntesDelVeredicto = 0.6f;
    public float pausaAntesDeMorir = 0.5f;
    public float pausaFinal = 0.9f;

    [Header("Interfaz")]
    public bool mostrarStatsSobreUnidades = true;
    public bool mostrarPrevisualizacion = true;
    public float duracionReporte = 6f;

    // ------------------------------------------------------------------
    // Estado interno de la UI
    // ------------------------------------------------------------------
    private class TextoFlotante
    {
        public Vector3 pos;
        public string texto;
        public Color color;
        public float inicio;
        public float duracion;
    }

    private readonly List<TextoFlotante> textos = new List<TextoFlotante>();
    private readonly List<string> logVisible = new List<string>();
    private float ultimoLog = -999f;

    private GUIStyle estiloStats;
    private GUIStyle estiloTextoFlotante;
    private GUIStyle estiloPanel;
    private HexGridGenerator tableroCache;

    private void Awake()
    {
        if (instancia == null) instancia = this;
        else if (instancia != this) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (instancia == this) instancia = null;
    }

    private void Update()
    {
        textos.RemoveAll(t => Time.time - t.inicio > t.duracion);
    }

    // =====================================================================
    // 1. FÓRMULAS (puras: no modifican nada)
    // =====================================================================

    /// <summary>Fuerza = puntosAtaque + bono si está en su terreno favorito.</summary>
    public static int CalcularAtaque(Unidad u)
    {
        if (u == null) return 0;

        int ataque = u.PuntosAtaque;
        HexCelda celda = u.CeldaActual;
        if (celda != null && celda.terreno == u.TerrenoFavorito)
        {
            ataque += Instancia.bonoAtaqueTerrenoFavorito;
        }
        return Mathf.Max(0, ataque);
    }

    /// <summary>Resistencia = puntosDefensa + bono de defensa de la casilla (con tope).</summary>
    public static int CalcularResistencia(Unidad u)
    {
        if (u == null) return 0;

        int resistencia = u.puntosDefensa;
        HexCelda celda = u.CeldaActual;
        if (celda != null)
        {
            resistencia += Mathf.Min(celda.bonusDefensa, Instancia.topeBonoDefensaTerreno);
        }
        return resistencia;
    }

    /// <summary>
    /// Simula un combate SIN modificar nada. Tiene en cuenta el daño ya marcado y,
    /// opcionalmente, daño ya planeado por otros atacantes (para la IA).
    /// </summary>
    public static ResultadoCombate Simular(Unidad atacante, Unidad defensor, int danioPrevistoDefensor = 0, int danioPrevistoAtacante = 0)
    {
        ResultadoCombate r = new ResultadoCombate();
        if (atacante == null || defensor == null) return r;

        r.ataqueAtacante = CalcularAtaque(atacante);
        r.hayContraataque = Instancia.contraataqueActivo && defensor.EstaEnAlcance(atacante.CeldaActual);
        r.ataqueDefensor = r.hayContraataque ? CalcularAtaque(defensor) : 0;

        r.resistenciaDefensor = CalcularResistencia(defensor);
        r.resistenciaAtacante = CalcularResistencia(atacante);

        r.danioTotalDefensor = defensor.DanioMarcado + danioPrevistoDefensor + r.ataqueAtacante;
        r.danioTotalAtacante = atacante.DanioMarcado + danioPrevistoAtacante + r.ataqueDefensor;

        r.defensorMuere = r.danioTotalDefensor > 0 && r.danioTotalDefensor >= r.resistenciaDefensor;
        r.atacanteMuere = r.danioTotalAtacante > 0 && r.danioTotalAtacante >= r.resistenciaAtacante;
        return r;
    }

    // =====================================================================
    // 2. RESOLUCIÓN (daño simultáneo, muertes al final)
    // =====================================================================

    public Coroutine Resolver(List<DeclaracionAtaque> declaraciones, List<string> reportes)
    {
        return StartCoroutine(ResolverCombates(declaraciones, reportes));
    }

    private IEnumerator ResolverCombates(List<DeclaracionAtaque> declaraciones, List<string> reportes)
    {
        logVisible.Clear();

        List<Unidad> involucradas = new List<Unidad>();
        List<DeclaracionAtaque> resueltas = new List<DeclaracionAtaque>();

        // --- FASE A: cada ataque marca daño (atacante y defensor a la vez). Aún NO muere nadie ---
        foreach (DeclaracionAtaque decl in declaraciones)
        {
            Unidad atq = decl.Atacante;
            Unidad def = decl.Objetivo;

            if (atq == null || def == null || atq.EstaMuerta || def.EstaMuerta) continue;
            if (atq.CeldaActual == null || def.CeldaActual == null) continue;

            int ataque = CalcularAtaque(atq);
            bool contra = contraataqueActivo && def.EstaEnAlcance(atq.CeldaActual);
            int contraAtaque = contra ? CalcularAtaque(def) : 0;

            Action impacto = () =>
            {
                def.RecibirDanio(ataque);
                if (contraAtaque > 0) atq.RecibirDanio(contraAtaque);

                MostrarTextoFlotante(def.transform.position, "-" + ataque, new Color(1f, 0.25f, 0.25f));
                if (contraAtaque > 0)
                {
                    MostrarTextoFlotante(atq.transform.position, "-" + contraAtaque, new Color(1f, 0.65f, 0.15f));
                }
            };

            yield return StartCoroutine(atq.AnimarEmbestida(def, impacto));

            atq.MarcarComoAtacado();
            resueltas.Add(decl);
            if (!involucradas.Contains(atq)) involucradas.Add(atq);
            if (!involucradas.Contains(def)) involucradas.Add(def);

            string linea = $"{Nombre(atq)} ({ataque}) golpea a {Nombre(def)}: " +
                           $"{def.DanioMarcado}/{CalcularResistencia(def)} de daño";
            linea += contra
                ? $" | contraataque {contraAtaque} -> {Nombre(atq)} {atq.DanioMarcado}/{CalcularResistencia(atq)}"
                : " | sin contraataque";
            Anadir(reportes, linea);

            yield return new WaitForSeconds(pausaEntreCombates);
        }

        if (resueltas.Count == 0) yield break;

        yield return new WaitForSeconds(pausaAntesDelVeredicto);

        // --- FASE B: ahora sí, se comprueba quién cae (todo a la vez) ---
        HashSet<Unidad> caen = new HashSet<Unidad>();
        foreach (Unidad u in involucradas)
        {
            if (u != null && u.DebeMorir()) caen.Add(u);
        }

        // --- Veredicto visual: quién gana y quién pierde ---
        foreach (DeclaracionAtaque decl in resueltas)
        {
            Unidad atq = decl.Atacante;
            Unidad def = decl.Objetivo;
            if (atq == null || def == null) continue;

            bool defCae = caen.Contains(def);
            bool atqCae = caen.Contains(atq);

            if (defCae && !atqCae)
            {
                Anadir(reportes, $"VICTORIA: {Nombre(atq)} derrota a {Nombre(def)}");
                MostrarTextoFlotante(atq.transform.position + Vector3.up * 0.3f, "¡VENCE!", new Color(0.3f, 1f, 0.4f));
            }
            else if (!defCae && atqCae)
            {
                Anadir(reportes, $"DERROTA: {Nombre(def)} resiste y derrota a {Nombre(atq)}");
                MostrarTextoFlotante(def.transform.position + Vector3.up * 0.3f, "¡VENCE!", new Color(0.3f, 1f, 0.4f));
            }
            else if (defCae && atqCae)
            {
                Anadir(reportes, $"EMPATE: {Nombre(atq)} y {Nombre(def)} caen juntos");
                MostrarTextoFlotante(atq.transform.position + Vector3.up * 0.3f, "EMPATE", new Color(1f, 0.9f, 0.3f));
                MostrarTextoFlotante(def.transform.position + Vector3.up * 0.3f, "EMPATE", new Color(1f, 0.9f, 0.3f));
            }
            else
            {
                int restante = Mathf.Max(0, CalcularResistencia(def) - def.DanioMarcado);
                Anadir(reportes, $"SIN BAJAS: {Nombre(def)} sobrevive (le quedan {restante} de resistencia)");
            }
        }

        yield return new WaitForSeconds(pausaAntesDeMorir);

        foreach (Unidad u in caen)
        {
            if (u == null) continue;
            MostrarTextoFlotante(u.transform.position + Vector3.up * 0.3f, "¡MUERE!", new Color(1f, 0.2f, 0.2f));
            Anadir(reportes, $"{Nombre(u)} cae en combate.");
            u.Morir();
        }

        yield return new WaitForSeconds(pausaFinal);
    }

    private void Anadir(List<string> reportes, string linea)
    {
        Debug.Log("[Combate] " + linea);
        if (reportes != null) reportes.Add(linea);

        logVisible.Add(linea);
        if (logVisible.Count > 14) logVisible.RemoveAt(0);
        ultimoLog = Time.time;
    }

    private static string Nombre(Unidad u)
    {
        return u == null ? "?" : u.name.Replace("(Clone)", "").Trim();
    }

    public void MostrarTextoFlotante(Vector3 posMundo, string texto, Color color)
    {
        textos.Add(new TextoFlotante
        {
            pos = posMundo,
            texto = texto,
            color = color,
            inicio = Time.time,
            duracion = 1.4f
        });
    }

    // =====================================================================
    // 3. UTILIDAD: unidad bajo el ratón
    // =====================================================================

    public static Unidad UnidadBajoRaton()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        CombateManager cm = Instancia;
        if (cm.tableroCache == null) cm.tableroCache = FindFirstObjectByType<HexGridGenerator>();
        float tamano = cm.tableroCache != null ? cm.tableroCache.tamanoHexagono : 1f;
        float radioMax = 0.8f * tamano;

        Vector3 mundo = cam.ScreenToWorldPoint(Input.mousePosition);
        Vector2 punto = new Vector2(mundo.x, mundo.y);

        Unidad mejor = null;
        float mejorDist = radioMax;
        foreach (Unidad u in Unidad.Todas)
        {
            if (u == null || u.EstaMuerta) continue;
            float d = Vector2.Distance(punto, new Vector2(u.transform.position.x, u.transform.position.y));
            if (d < mejorDist)
            {
                mejorDist = d;
                mejor = u;
            }
        }
        return mejor;
    }

    // =====================================================================
    // 4. INTERFAZ (OnGUI, sin Canvas ni prefabs)
    // =====================================================================

    private void PrepararEstilos()
    {
        if (estiloStats != null) return;

        estiloStats = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            richText = true
        };
        estiloStats.normal.textColor = Color.white;

        estiloTextoFlotante = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            richText = false
        };

        estiloPanel = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 13,
            richText = true,
            wordWrap = true,
            padding = new RectOffset(8, 8, 6, 6)
        };
        estiloPanel.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        PrepararEstilos();

        if (mostrarStatsSobreUnidades)
        {
            foreach (Unidad u in Unidad.Todas)
            {
                if (u != null && !u.EstaMuerta) DibujarStats(cam, u);
            }
        }

        DibujarAtaquesDeclarados(cam);
        DibujarObjetivosPosibles(cam);
        if (mostrarPrevisualizacion) DibujarPrevisualizacion();
        DibujarTextosFlotantes(cam);
        DibujarReporte();
    }

    private static Vector2 AGui(Camera cam, Vector3 mundo)
    {
        Vector3 s = cam.WorldToScreenPoint(mundo);
        return new Vector2(s.x, Screen.height - s.y);
    }

    private static Color ColorEquipo(int jugadorId)
    {
        switch (jugadorId)
        {
            case 0: return new Color(0.15f, 0.35f, 0.85f, 0.9f);
            case 1: return new Color(0.75f, 0.15f, 0.15f, 0.9f);
            case 2: return new Color(0.55f, 0.3f, 0.75f, 0.9f);
            default: return new Color(0.3f, 0.3f, 0.3f, 0.9f);
        }
    }

    /// <summary>Muestra "Fuerza/Resistencia restante" sobre cada unidad, como una carta (3/4).</summary>
    private void DibujarStats(Camera cam, Unidad u)
    {
        int ataque = CalcularAtaque(u);
        int resistencia = CalcularResistencia(u);
        int restante = Mathf.Max(0, resistencia - u.DanioMarcado);

        string resTexto = u.DanioMarcado > 0
            ? $"<color=#FF7070>{restante}</color>"
            : restante.ToString();

        Vector2 p = AGui(cam, u.transform.position + Vector3.down * 0.55f);
        Rect rect = new Rect(p.x - 28, p.y - 12, 56, 24);

        Color previo = GUI.color;
        GUI.color = ColorEquipo(u.jugadorId);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previo;

        GUI.Label(rect, $"{ataque}/{resTexto}", estiloStats);
    }

    private void DibujarAtaquesDeclarados(Camera cam)
    {
        if (TurnManager.Instancia == null) return;

        foreach (DeclaracionAtaque d in TurnManager.Instancia.AtaquesDeclarados)
        {
            if (d == null || d.Atacante == null || d.Objetivo == null) continue;
            if (d.Atacante.EstaMuerta || d.Objetivo.EstaMuerta) continue;

            Vector2 a = AGui(cam, d.Atacante.transform.position);
            Vector2 b = AGui(cam, d.Objetivo.transform.position);
            DibujarLinea(a, b, new Color(1f, 0.6f, 0.1f, 0.9f), 4f);
            DibujarMarcoUnidad(cam, d.Objetivo, new Color(1f, 0.6f, 0.1f), 4f);
        }
    }

    /// <summary>Enmarca en rojo los enemigos que la unidad seleccionada puede atacar.</summary>
    private void DibujarObjetivosPosibles(Camera cam)
    {
        if (TurnManager.Instancia == null) return;
        if (TurnManager.Instancia.FaseActual != FaseTurno.Ataque || TurnManager.Instancia.TurnoDeIA) return;

        Unidad sel = Unidad.Seleccionada;
        if (sel == null || sel.jugadorId != TurnManager.Instancia.JugadorActual) return;

        foreach (Unidad u in Unidad.Todas)
        {
            if (u != null && sel.PuedeAtacar(u))
            {
                DibujarMarcoUnidad(cam, u, new Color(1f, 0.15f, 0.15f), 3f);
            }
        }
    }

    /// <summary>Al pasar el ratón por un enemigo atacable: quién gana y quién pierde ANTES de comprometerse.</summary>
    private void DibujarPrevisualizacion()
    {
        TurnManager tm = TurnManager.Instancia;
        if (tm == null || tm.FaseActual != FaseTurno.Ataque || tm.TurnoDeIA) return;

        Unidad atq = Unidad.Seleccionada;
        if (atq == null || atq.jugadorId != tm.JugadorActual) return;

        Unidad def = UnidadBajoRaton();
        if (def == null || !atq.PuedeAtacar(def)) return;

        // Daño que otros aliados ya han declarado contra este mismo objetivo
        int danioPlaneado = 0;
        foreach (DeclaracionAtaque d in tm.AtaquesDeclarados)
        {
            if (d.Objetivo == def && d.Atacante != atq) danioPlaneado += CalcularAtaque(d.Atacante);
        }

        ResultadoCombate r = Simular(atq, def, danioPlaneado, 0);

        string nAtq = Nombre(atq);
        string nDef = Nombre(def);

        string l1 = $"<b>{nAtq}  vs  {nDef}</b>";
        string l2 = $"Infliges <b>{r.ataqueAtacante}</b>: {nDef} {r.danioTotalDefensor}/{r.resistenciaDefensor} " +
                    (r.defensorMuere ? "<color=#7CFF8A>MUERE</color>" : "sobrevive");
        if (danioPlaneado > 0) l2 += $" <i>(incl. {danioPlaneado} de otros ataques)</i>";

        string l3 = r.hayContraataque
            ? $"Contraataque <b>{r.ataqueDefensor}</b>: {nAtq} {r.danioTotalAtacante}/{r.resistenciaAtacante} " +
              (r.atacanteMuere ? "<color=#FF7070>MUERES</color>" : "sobrevives")
            : "Sin contraataque (estás fuera de su alcance)";

        string veredicto;
        if (r.defensorMuere && !r.atacanteMuere) veredicto = "<color=#7CFF8A><b>GANAS el combate</b></color>";
        else if (!r.defensorMuere && r.atacanteMuere) veredicto = "<color=#FF7070><b>PIERDES el combate</b></color>";
        else if (r.defensorMuere && r.atacanteMuere) veredicto = "<color=#FFE066><b>EMPATE: caen los dos</b></color>";
        else veredicto = "<color=#BBBBBB>Nadie muere</color>";

        string texto = l1 + "\n" + l2 + "\n" + l3 + "\n" + veredicto;

        Vector2 m = Event.current.mousePosition;
        float ancho = 330f;
        GUIContent contenido = new GUIContent(texto);
        float alto = estiloPanel.CalcHeight(contenido, ancho);

        float x = Mathf.Min(m.x + 18f, Screen.width - ancho - 5f);
        float y = Mathf.Min(m.y + 18f, Screen.height - alto - 5f);

        GUI.Box(new Rect(x, y, ancho, alto), contenido, estiloPanel);
    }

    private void DibujarTextosFlotantes(Camera cam)
    {
        foreach (TextoFlotante t in textos)
        {
            float k = Mathf.Clamp01((Time.time - t.inicio) / t.duracion);
            Vector2 p = AGui(cam, t.pos);
            p.y -= 20f + k * 55f;

            Rect rect = new Rect(p.x - 90, p.y - 18, 180, 36);
            float alpha = 1f - k * k;

            Color previo = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, alpha);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), t.texto, estiloTextoFlotante);
            GUI.color = new Color(t.color.r, t.color.g, t.color.b, alpha);
            GUI.Label(rect, t.texto, estiloTextoFlotante);
            GUI.color = previo;
        }
    }

    private void DibujarReporte()
    {
        if (logVisible.Count == 0 || Time.time - ultimoLog > duracionReporte) return;

        float ancho = 430f;
        string texto = "<b>COMBATE</b>\n" + string.Join("\n", logVisible);
        GUIContent contenido = new GUIContent(texto);
        float alto = estiloPanel.CalcHeight(contenido, ancho);

        GUI.Box(new Rect(Screen.width - ancho - 10f, 10f, ancho, alto), contenido, estiloPanel);
    }

    // --- Primitivas de dibujo ---

    private void DibujarMarcoUnidad(Camera cam, Unidad u, Color color, float grosor)
    {
        Vector2 centro = AGui(cam, u.transform.position);
        Vector2 arriba = AGui(cam, u.transform.position + Vector3.up);
        float lado = Mathf.Abs(arriba.y - centro.y) * 1.5f;
        if (lado < 20f) lado = 20f;

        Rect r = new Rect(centro.x - lado / 2f, centro.y - lado / 2f, lado, lado);
        DibujarMarco(r, color, grosor);
    }

    private static void DibujarMarco(Rect r, Color color, float g)
    {
        Color previo = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, g), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.yMax - g, r.width, g), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x, r.y, g, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - g, r.y, g, r.height), Texture2D.whiteTexture);
        GUI.color = previo;
    }

    private static void DibujarLinea(Vector2 a, Vector2 b, Color color, float grosor)
    {
        Matrix4x4 matrizPrevia = GUI.matrix;
        Color colorPrevio = GUI.color;

        float angulo = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
        float largo = (b - a).magnitude;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angulo, a);
        GUI.DrawTexture(new Rect(a.x, a.y - grosor / 2f, largo, grosor), Texture2D.whiteTexture);

        GUI.matrix = matrizPrevia;
        GUI.color = colorPrevio;
    }
}
