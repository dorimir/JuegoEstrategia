using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    [Header("Configuración del Tablero 2D")]
    [Range(1, 10)]
    public int radioMapa = 2;
    public float tamanoHexagono = 1.0f;
    public GameObject hexPrefab;

    [Header("Opciones de Generación")]
    public bool autoGenerarAlIniciar = true;

    private Dictionary<Vector2Int, HexCelda> mapaCeldas = new Dictionary<Vector2Int, HexCelda>();

    private void Awake()
    {
        // 1. Reconstruir el diccionario con las casillas que ya estén en la escena
        ReconstruirDiccionarioExistente();

        // 2. Solo genera un mapa aleatorio si la escena está completamente vacía
        if (autoGenerarAlIniciar && mapaCeldas.Count == 0)
        {
            GenerarTablero();
        }
    }

    [ContextMenu("Generar Tablero Nuevamente")]
    public void GenerarTablero()
    {
        // Guardar los terrenos editados a mano en la escena antes de regenerar
        Dictionary<Vector2Int, TipoTerreno> terrenosPrevios = new Dictionary<Vector2Int, TipoTerreno>();
        foreach (Transform child in transform)
        {
            HexCelda celda = child.GetComponent<HexCelda>();
            if (celda != null)
            {
                terrenosPrevios[new Vector2Int(celda.q, celda.r)] = celda.terreno;
            }
        }

        // Limpiar exhaustivamente para evitar mapas superpuestos
        LimpiarTablero();

        for (int q = -radioMapa; q <= radioMapa; q++)
        {
            int r1 = Mathf.Max(-radioMapa, -q - radioMapa);
            int r2 = Mathf.Min(radioMapa, -q + radioMapa);

            for (int r = r1; r <= r2; r++)
            {
                Vector2Int coord = new Vector2Int(q, r);
                TipoTerreno terrenoAsignado = terrenosPrevios.ContainsKey(coord)
                    ? terrenosPrevios[coord]
                    : (TipoTerreno)Random.Range(0, 4);

                CrearHexagono2D(q, r, terrenoAsignado);
            }
        }
    }

    private void CrearHexagono2D(int q, int r, TipoTerreno terreno)
    {
        float xPos = tamanoHexagono * (Mathf.Sqrt(3f) * q + Mathf.Sqrt(3f) / 2f * r);
        float yPos = tamanoHexagono * (3f / 2f * r);
        Vector3 posicionMundo = transform.position + new Vector3(xPos, yPos, 0f);

        GameObject nuevoObj = Instantiate(hexPrefab, posicionMundo, Quaternion.identity, transform);
        nuevoObj.name = $"Hex ({q}, {r})";

        HexCelda celda = nuevoObj.GetComponent<HexCelda>();
        if (celda == null)
        {
            celda = nuevoObj.AddComponent<HexCelda>();
        }

        celda.ConfigurarCelda(q, r, terreno);
        mapaCeldas[new Vector2Int(q, r)] = celda;
    }

    public void ReconstruirDiccionarioExistente()
    {
        mapaCeldas.Clear();
        foreach (Transform child in transform)
        {
            HexCelda celda = child.GetComponent<HexCelda>();
            if (celda != null)
            {
                mapaCeldas[new Vector2Int(celda.q, celda.r)] = celda;
            }
        }
    }

    /// <summary>
    /// Elimina de manera segura todos los objetos hijos en el Editor o en Play mode
    /// para evitar duplicados o tableros superpuestos.
    /// </summary>
    public void LimpiarTablero()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject obj = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(obj);
            }
            else
            {
                DestroyImmediate(obj);
            }
        }
        mapaCeldas.Clear();
    }

    public Dictionary<Vector2Int, HexCelda> ObtenerMapaCeldas()
    {
        if (mapaCeldas.Count == 0)
        {
            ReconstruirDiccionarioExistente();
        }
        return mapaCeldas;
    }
}