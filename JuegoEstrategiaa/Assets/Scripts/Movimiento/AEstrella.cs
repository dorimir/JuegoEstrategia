using System.Collections.Generic;
using UnityEngine;

public static class AEstrella
{
    public static List<HexCelda> BuscarCamino(HexCelda inicio, HexCelda destino, Dictionary<Vector2Int, HexCelda> mapa, int jugadorId = 0, float pesoEstructuras = 2f, bool esAtaque = false, float pesoMovimiento = 1f, float pesoTropas = 4f)
    {
        List<NodoA> abierta = new List<NodoA>();
        List<HexCelda> cerrada = new List<HexCelda>();
        abierta.Add(new NodoA(inicio, null, 0, Distancia(inicio, destino)));

        while (abierta.Count > 0)
        {
            NodoA actual = abierta[0];
            foreach (NodoA nodo in abierta)
            {
                if (nodo.f < actual.f) actual = nodo;
            }

            if (actual.celda == destino)
            {
                List<HexCelda> camino = new List<HexCelda>();
                for (NodoA n = actual; n != null; n = n.padre)
                {
                    camino.Insert(0, n.celda);
                }
                return camino;
            }

            abierta.Remove(actual);
            cerrada.Add(actual.celda);

            foreach (HexCelda vecino in HexGridUtils.ObtenerVecinos(new Vector2Int(actual.celda.q, actual.celda.r), mapa))
            {
                // Infranqueables (ej. Agua 999) o ya visitadas
                if (vecino.costeMovimiento >= 999 || cerrada.Contains(vecino)) continue;

                //Calculating total costs 

                float influenciaTropasEnemiga = 0f;
                if (InfluenceMapManager.Instancia != null && pesoTropas > 0f)
                {
                    TipoMapaInfluencia capaEnemiga = (jugadorId == 0) ? TipoMapaInfluencia.TropasIA : TipoMapaInfluencia.TropasJugador;
                    influenciaTropasEnemiga = InfluenceMapManager.Instancia.ObtenerValor(capaEnemiga, vecino);
                }

                int penalizacionRiesgoTropas = Mathf.RoundToInt(influenciaTropasEnemiga * pesoTropas);
                float influenciaEstructurasEnemigas = 0f;

                if (!esAtaque && InfluenceMapManager.Instancia != null && pesoEstructuras > 0f)
                {
                    TipoMapaInfluencia capaEstructurasEnemigas = (jugadorId == 0)
                        ? TipoMapaInfluencia.EstructurasIA
                        : TipoMapaInfluencia.EstructurasJugador;
                    influenciaEstructurasEnemigas = InfluenceMapManager.Instancia.ObtenerValor(capaEstructurasEnemigas, vecino);
                }

                int penalizacionEstructuras = Mathf.RoundToInt(influenciaEstructurasEnemigas * pesoEstructuras);
                int costeTerreno = Mathf.RoundToInt(vecino.costeMovimiento * pesoMovimiento);

                int nuevoG = actual.g + costeTerreno + penalizacionRiesgoTropas + penalizacionEstructuras;

                if (influenciaTropasEnemiga > 0f)
                {
                    Debug.Log($"[AEstrella] Vecino ({vecino.q}, {vecino.r}) | terreno={vecino.costeMovimiento} (x{pesoMovimiento}={costeTerreno}) | infTropas={influenciaTropasEnemiga} -> penTropas={penalizacionRiesgoTropas} | penEst={penalizacionEstructuras} | nuevoG={nuevoG}");
                }

                // Mantiene el coste del terreno para elegir la RUTA MÁS ÓPTIMA

                NodoA existente = null;
                foreach (NodoA n in abierta)
                {
                    if (n.celda == vecino) existente = n;
                }

                if (existente == null)
                {
                    abierta.Add(new NodoA(vecino, actual, nuevoG, Distancia(vecino, destino)));
                }
                else if (nuevoG < existente.g)
                {
                    existente.g = nuevoG;
                    existente.f = nuevoG + existente.h;
                    existente.padre = actual;
                }
            }
        }

        return null;
    }

    private static int Distancia(HexCelda a, HexCelda b)
    {
        return HexGridUtils.CalcularDistanciaHex(new Vector2Int(a.q, a.r), new Vector2Int(b.q, b.r));
    }
}