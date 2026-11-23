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

                float influenciaFusionadaTropas = 0f;
                if (InfluenceMapManager.Instancia != null && pesoTropas > 0f)
                {
                    float rawTropas = InfluenceMapManager.Instancia.ObtenerValor(TipoMapaInfluencia.TropasFusionado, vecino);

                    // Si es Jugador (0), valores negativos son ventaja de la IA (riesgo).
                    // Si es IA (1), valores positivos son ventaja del Jugador (riesgo).
                    influenciaFusionadaTropas = (jugadorId == 0) ? -rawTropas : rawTropas;
                }

                // Opcional: usa Mathf.Max(0f, influenciaFusionadaTropas) si solo quieres penalizar
                // cuando el enemigo tiene ventaja neta y no dar bonificación cuando hay ventaja aliada.
                int penalizacionRiesgoTropas = Mathf.RoundToInt(influenciaFusionadaTropas * pesoTropas);

                float influenciaFusionadaEstructuras = 0f;
                if (!esAtaque && InfluenceMapManager.Instancia != null && pesoEstructuras > 0f)
                {
                    float rawEstructuras = InfluenceMapManager.Instancia.ObtenerValor(TipoMapaInfluencia.EstructurasFusionado, vecino);
                    influenciaFusionadaEstructuras = (jugadorId == 0) ? -rawEstructuras : rawEstructuras;
                }

                int penalizacionEstructuras = Mathf.RoundToInt(influenciaFusionadaEstructuras * pesoEstructuras);
                int costeTerreno = Mathf.RoundToInt(vecino.costeMovimiento * pesoMovimiento);

                // Evita costes negativos o cero en un paso para no romper la búsqueda del camino
                int costePaso = Mathf.Max(1, costeTerreno + penalizacionRiesgoTropas + penalizacionEstructuras);
                int nuevoG = actual.g + costePaso;

                if (influenciaFusionadaTropas > 0f)
                {
                    Debug.Log($"[AEstrella] Vecino ({vecino.q}, {vecino.r}) | terreno={vecino.costeMovimiento} (x{pesoMovimiento}={costeTerreno}) | infTropasFus={influenciaFusionadaTropas} -> penTropas={penalizacionRiesgoTropas} | penEst={penalizacionEstructuras} | nuevoG={nuevoG}");
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