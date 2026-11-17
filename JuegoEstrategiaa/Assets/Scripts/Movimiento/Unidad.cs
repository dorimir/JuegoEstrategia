using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Unidad : MonoBehaviour
{
    public static Unidad Instancia;

    [SerializeField] private HexGridGenerator tablero;
    [SerializeField] private float velocidad = 3f;

    private HexCelda celdaActual;
    private bool enMovimiento;

    private void Awake()
    {
        Instancia = this;
    }

    private IEnumerator Start()
    {
        yield return null; // esperamos un frame a que se genere el tablero
        celdaActual = tablero.ObtenerMapaCeldas()[Vector2Int.zero];
        transform.position = celdaActual.transform.position + Vector3.back;
    }

    public void IrHacia(HexCelda destino)  
    // Se llama al hacer clic en una casilla
    {
        if (enMovimiento) return;

        List<HexCelda> camino = AEstrella.BuscarCamino(celdaActual, destino, tablero.ObtenerMapaCeldas());
        if (camino == null) return;

        // Si la unidad tiene limite y el camino es demasiado largo, no se mueve
        LimiteMovimiento limite = GetComponent<LimiteMovimiento>();
        if (limite != null && !limite.PuedeRecorrer(camino)) return;

        StartCoroutine(RecorrerCamino(camino));
    }

    private IEnumerator RecorrerCamino(List<HexCelda> camino)     // Avanza casilla a casilla
    {
        enMovimiento = true;

        foreach (HexCelda celda in camino)
        {
            Vector3 objetivo = celda.transform.position + Vector3.back;
            while (transform.position != objetivo)
            {
                transform.position = Vector3.MoveTowards(transform.position, objetivo, velocidad * Time.deltaTime);
                yield return null;
            }
            celdaActual = celda;
        }
        enMovimiento = false;
    }
}
