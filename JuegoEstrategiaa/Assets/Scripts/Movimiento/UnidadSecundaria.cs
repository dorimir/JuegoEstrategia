using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnidadSecundaria : Unidad
{
    [Header("Configuración de Manada (Reno)")]
    [SerializeField] private float radioDeOido = 6f;
    [SerializeField] private int defensaBase = 10;

    private int defensaExtraTurno = 0;
    private Unidad unidadEnAlerta = null;

    public int DefensaTotal => defensaBase + defensaExtraTurno; 

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    public void MoverA(List<HexCelda> camino)
    {
        StartCoroutine(RecorrerCamino(camino));
    }

    public static void ActivarCampanaPeligro(Unidad unidadQueAlerta)
    {
        foreach (Unidad unidad in Todas)
        {
            if (unidad is UnidadSecundaria reno && reno.jugadorId == unidadQueAlerta.jugadorId)
            {
                reno.AccionDefensaGrupal(unidadQueAlerta);
            }
        }
    }

    public void AccionDefensaGrupal(Unidad unidadAlerta)
    {
        if (unidadAlerta == null || CeldaActual == null || unidadAlerta.CeldaActual == null) Debug.Log($"Something is NULL"); ;

        float distanciaMundo = Vector3.Distance(transform.position, unidadAlerta.transform.position);
        if (distanciaMundo > radioDeOido) return;

        int distanciaCasillas = Mathf.Abs(CeldaActual.q - unidadAlerta.CeldaActual.q) +
                               Mathf.Abs(CeldaActual.r - unidadAlerta.CeldaActual.r);

        if (distanciaCasillas <= rangoMovimientoCasillas)
        {
            unidadEnAlerta = unidadAlerta;

            defensaExtraTurno += defensaBase;
            StartCoroutine(EjecutarMovimientoReaccion(unidadAlerta.CeldaActual));
        }
        }

    private IEnumerator EjecutarMovimientoReaccion(HexCelda celdaDestino)
    {
        HexCelda celdaSanta = unidadEnAlerta.CeldaActual;
        Debug.Log($"{gameObject.name} reacciona a la alarma y corre a proteger a su aliado.");

        if (tablero == null) yield break;
        Dictionary<Vector2Int, HexCelda> mapa = tablero.ObtenerMapaCeldas();
        HashSet<HexCelda> celdasVecinas = HexGridUtils.ObtenerCasillasEnRango(celdaSanta, 1, mapa);

        HexCelda mejorCeldaDestino = null;
        float menorDistancia = float.MaxValue;

        foreach (HexCelda celdaVecina in celdasVecinas)
        {
            if (Unidad.EnCelda(celdaVecina) != null) continue;
            float distancia = Vector3.Distance(transform.position, celdaVecina.transform.position);
            if (distancia < menorDistancia)
            {
                menorDistancia = distancia;
                mejorCeldaDestino = celdaVecina;
            }
        }

        if (mejorCeldaDestino == null)
        {
            Debug.LogWarning($"{name}: No hay celdas libres alrededor de Santa para protegerlo.");
            yield break;
        }
        HashSet<HexCelda> casillasValidasReno = HexGridUtils.ObtenerCasillasEnRango(celdaActual, rangoMovimientoCasillas, mapa);
        if (!casillasValidasReno.Contains(mejorCeldaDestino))
        {
            Debug.Log($"{name}: La celda junto a Santa está fuera de mi rango de movimiento ({rangoMovimientoCasillas}).");
            yield break;
        }

        List<HexCelda> camino = AEstrella.BuscarCamino(celdaActual, mejorCeldaDestino, mapa);
        if (camino == null || camino.Count < 2) yield break;

        MoverPorCamino(camino);

        yield return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radioDeOido);
    }

    }
