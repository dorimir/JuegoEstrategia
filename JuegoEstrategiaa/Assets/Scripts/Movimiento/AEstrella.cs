using System.Collections.Generic;
using UnityEngine;

public static class AEstrella
{
    public static List<HexCelda> BuscarCamino(HexCelda inicio, HexCelda destino, Dictionary<Vector2Int, HexCelda> mapa)
    {
        List<NodoA> abierta = new List<NodoA>();     // casillas por revisar
        List<HexCelda> cerrada = new List<HexCelda>(); // casillas ya revisadas
        abierta.Add(new NodoA(inicio, null, 0, Distancia(inicio, destino)));

        while (abierta.Count > 0)
        {
            // 1. Elegimos el nodo con menor f
            NodoA actual = abierta[0];
            foreach (NodoA nodo in abierta)
            {
                if (nodo.f < actual.f) actual = nodo;
            }

            // 2. Si es el destino, reconstruimos el camino siguiendo los padres
            if (actual.celda == destino)
            {
                List<HexCelda> camino = new List<HexCelda>();
                for (NodoA n = actual; n != null; n = n.padre)
                {
                    camino.Insert(0, n.celda);
                }
                return camino;
            }

            // 3. Lo pasamos de abierta a cerrada
            abierta.Remove(actual);
            cerrada.Add(actual.celda);

            // 4. Revisamos sus 6 vecinos
            foreach (HexCelda vecino in HexGridUtils.ObtenerVecinos(new Vector2Int(actual.celda.q, actual.celda.r), mapa))
            {
                // Saltamos el agua (coste 999) y las casillas ya revisadas
                if (vecino.costeMovimiento >= 999 || cerrada.Contains(vecino)) continue;

                int nuevoG = actual.g + vecino.costeMovimiento;

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
                    // Hemos encontrado un camino mas barato hasta esa casilla
                    existente.g = nuevoG;
                    existente.f = nuevoG + existente.h;
                    existente.padre = actual;
                }
            }
        }

        return null; // no hay camino
    }

    // Heuristica: distancia hexagonal en pasos
    private static int Distancia(HexCelda a, HexCelda b)
    {
        return HexGridUtils.CalcularDistanciaHex(new Vector2Int(a.q, a.r), new Vector2Int(b.q, b.r));
    }
}
