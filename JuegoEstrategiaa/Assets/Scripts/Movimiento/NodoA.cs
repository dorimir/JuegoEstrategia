// Datos que el A* guarda de cada casilla mientras busca
public class NodoA
{
    public HexCelda celda;
    public NodoA padre; // casilla de la q llegamos (para reconstruir el camino)
    public int g;       // coste real desde el inicio
    public int h;       // estimacion hasta el destino
    public int f;       // g + h

    public NodoA(HexCelda celda, NodoA padre, int g, int h)
    {
        this.celda = celda;
        this.padre = padre;
        this.g = g;
        this.h = h;
        f = g + h;
    }
}
