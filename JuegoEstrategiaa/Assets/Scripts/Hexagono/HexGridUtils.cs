using System.Collections.Generic;
using UnityEngine;

public static class HexGridUtils
{
    // Las 6 direcciones adyacentes en coordenadas axiales //es esto correto? nos dejamos una diagonal y cogemos la vertical pura
    private static readonly Vector2Int[] DireccionesHex = new Vector2Int[]
    {
        new Vector2Int(+1, 0),  new Vector2Int(+1, -1), new Vector2Int(0, -1),
        new Vector2Int(-1, 0),  new Vector2Int(-1, +1), new Vector2Int(0, +1)
    };

     // Las 4 direcciones adyacentes en coordenadas axiales
    private static readonly Vector2Int[] FourDireccionesHex = new Vector2Int[]
    {
        new Vector2Int(+1, 0), new Vector2Int(0, -1),
        new Vector2Int(-1, 0),  new Vector2Int(0, +1)
    };

    // Las diagonales
    private static readonly Vector2Int[] DiagonalesDireccionesHex = new Vector2Int[]
    {
        new Vector2Int(+1, -1), new Vector2Int(-1, +1), new Vector2Int(+1, +1), new Vector2Int(-1, -1)
    };


    /// <summary>
    /// Obtiene las celdas adyacentes v�lidas a partir de una coordenada.
    /// </summary>
    public static List<HexCelda> ObtenerVecinos(Vector2Int coord, Dictionary<Vector2Int, HexCelda> mapa, bool attacking = false, Unidad.TipoAtaque? tipoAtaque = null)
    {
        List<HexCelda> vecinos = new List<HexCelda>();
        Vector2Int[] Directions = null;
        
        if(!attacking)
        {
            Directions = DireccionesHex;
        }
        else
        {
              // Use the value on the left if it is not null; otherwise use the value on the right.

             Unidad.TipoAtaque switchValue = tipoAtaque ?? Unidad.TipoAtaque.Largo; // Default to Largo if null

            switch (switchValue)
            {
                case Unidad.TipoAtaque.Rectas:
                    Directions = FourDireccionesHex;
                    break;

                case Unidad.TipoAtaque.Diagonales:
                    Directions = DiagonalesDireccionesHex;
                    break;

                case Unidad.TipoAtaque.Largo:
                    Directions = DireccionesHex;
                    break;
            }
        }
        foreach (var dir in Directions)
            {
                Vector2Int claveVecino = coord + dir;
                if (mapa.TryGetValue(claveVecino, out HexCelda celdaEncontrada))
                {
                    vecinos.Add(celdaEncontrada);
                }
            }

            return vecinos;
    }

    public static HashSet<HexCelda> ObtenerCasillasEnRango(HexCelda origen, int rangoPasos, Dictionary<Vector2Int, HexCelda> mapa, bool attacking = false, Unidad.TipoAtaque? tipoAtaque = null)
    {
        HashSet<HexCelda> alcanzables = new HashSet<HexCelda>();
        Dictionary<HexCelda, int> distancias = new Dictionary<HexCelda, int>();
        Queue<HexCelda> frontera = new Queue<HexCelda>();

        frontera.Enqueue(origen);
        distancias[origen] = 0;
        alcanzables.Add(origen);

        while (frontera.Count > 0)
        {
            HexCelda actual = frontera.Dequeue();
            int pasosActuales = distancias[actual];

            // Si ya alcanzamos el l�mite de pasos, no seguimos expandiendo desde este nodo
            if (pasosActuales >= rangoPasos) continue;

            foreach (HexCelda vecino in ObtenerVecinos(new Vector2Int(actual.q, actual.r), mapa, attacking, tipoAtaque)) //default to false if null
            {
                // Infranqueable (Agua)
                if (vecino.costeMovimiento >= 999) continue;

                int nuevaDistancia = pasosActuales + 1;

                // Si no ha sido visitada o encontramos un camino con menos pasos
                if (!distancias.ContainsKey(vecino) || nuevaDistancia < distancias[vecino])
                {
                    distancias[vecino] = nuevaDistancia;
                    alcanzables.Add(vecino);
                    frontera.Enqueue(vecino);
                }
            }
        }

        return alcanzables;
    }

    /// <summary>
    /// Distancia matem�tica directa entre dos coordenadas axiales (en casillas)
    /// </summary>
    public static int CalcularDistanciaHex(Vector2Int a, Vector2Int b)
    {
        int dq = a.x - b.x;
        int dr = a.y - b.y;
        return (Mathf.Abs(dq) + Mathf.Abs(dr) + Mathf.Abs(dq + dr)) / 2;
    }
}