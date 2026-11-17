using System.Collections.Generic;
using UnityEngine;

public class HexGridGenerator : MonoBehaviour
{
    [Header("Configuración del Tablero 2D (Estilo Catan)")]
    [Tooltip("Radio del mapa. Radio 2 = 19 casillas en forma de hexágono concéntrico.")]
    [Range(1, 10)]
    public int radioMapa = 2;

    [Tooltip("Distancia desde el centro del hexágono a uno de sus vértices")]
    public float tamanoHexagono = 1.0f;

    [Tooltip("Prefab 2D del Hexágono (debe tener SpriteRenderer y HexCelda)")]
    public GameObject hexPrefab;

    [Header("Opciones de Generación")]
    public bool autoGenerarAlIniciar = true;

    private Dictionary<Vector2Int, HexCelda> mapaCeldas = new Dictionary<Vector2Int, HexCelda>();

    private void Start()
    {
        if (autoGenerarAlIniciar)
        {
            GenerarTablero();
        }
    }

    [ContextMenu("Generar Tablero 2D")]
    public void GenerarTablero()
    {
        LimpiarTablero();

        // Generación concéntrica tipo Catan
        for (int q = -radioMapa; q <= radioMapa; q++)
        {
            int r1 = Mathf.Max(-radioMapa, -q - radioMapa);
            int r2 = Mathf.Min(radioMapa, -q + radioMapa);

            for (int r = r1; r <= r2; r++)
            {
                CrearHexagono2D(q, r);
            }
        }
    }

    private void CrearHexagono2D(int q, int r)
    {
        // Conversión a coordenadas de mundo 2D (X, Y) para orientación Pointy-topped
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

        // Asignación aleatoria de terreno y visualización básica
        TipoTerreno terrenoAleatorio = (TipoTerreno)Random.Range(0, 4);
        celda.ConfigurarCelda(q, r, terrenoAleatorio);

        mapaCeldas.Add(new Vector2Int(q, r), celda);
    }

    public void LimpiarTablero()
    {
        foreach (Transform child in transform)
        {
            DestroyImmediate(child.gameObject);
        }
        mapaCeldas.Clear();
    }

    public Dictionary<Vector2Int, HexCelda> ObtenerMapaCeldas()
    {
        return mapaCeldas;
    }
}