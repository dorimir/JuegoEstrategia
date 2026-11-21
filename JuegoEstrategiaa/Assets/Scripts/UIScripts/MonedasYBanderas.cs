using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Sistema de monedas y banderas.
// 1. Cuando una unidad toca una celda con bandera, esa bandera queda conquistada (y se pone azul).
// 2. Cada X segundos se cobran monedas por cada bandera conquistada.
public class MonedasYBanderas : MonoBehaviour
{
    public static MonedasYBanderas Instancia;

    [SerializeField] private TextMeshProUGUI textoMonedas;
    [SerializeField] private TextMeshProUGUI textoBanderas;
    [SerializeField] private Sprite spriteBanderaAzul;
    [SerializeField] private int jugadorId = 0;           // solo cuentan las unidades de este jugador
    [SerializeField] private int monedasPorBandera = 1;
    [SerializeField] private float segundosEntrePagos = 5f;

    private Dictionary<int, int> monedasPorJugador = new Dictionary<int, int>();
    private List<HexCelda> banderasConquistadas = new List<HexCelda>();
    private float temporizador = 0f;

    public int GetMonedas(int jugadorId) 
    {
        if (!monedasPorJugador.ContainsKey(jugadorId)) monedasPorJugador[jugadorId] = 0;
        return monedasPorJugador[jugadorId];
    }

    private void Awake()
    {
        Instancia = this;
        // Inicializar carteras para Jugador (0) e IA (1)
        monedasPorJugador[0] = 0;
        monedasPorJugador[1] = 0;
    }

    private void Start()
    {
        ActualizarTextos();
    }

    private void Update()
    {
        ConquistarBanderas();

        temporizador += Time.deltaTime;
        if (temporizador >= segundosEntrePagos)
        {
            temporizador = 0f;
            ProcesarPagos();
            ActualizarTextos();
        }
    }

    private void ProcesarPagos()
    {
        foreach (HexCelda celda in banderasConquistadas)
        {
            // Aquí asumimos que las banderas guardan quién las conquistó.
            // Por ahora, el script original solo manejaba jugadorId=0.
            // Si la celda tiene un propietario, le pagamos a él.
            int idDuenio = (celda.propietario == Propietario.Jugador) ? 0 : 1;
            if (!monedasPorJugador.ContainsKey(idDuenio)) monedasPorJugador[idDuenio] = 0;
            monedasPorJugador[idDuenio] += monedasPorBandera;
        }
    }

    // Si una unidad esta sobre una bandera que aun no tiene dueño, la anadimos a la lista
    private void ConquistarBanderas()
    {
        foreach (Unidad unidad in Unidad.Todas)
        {
            HexCelda celda = unidad.CeldaActual;
            if (celda == null) continue;

            bool esBandera = celda.terreno == TipoTerreno.Bandera || celda.terreno == TipoTerreno.BanderaCentral;

            if (esBandera && celda.propietario == Propietario.Nadie)
            {
                // Conquistar para el bando de la unidad
                celda.propietario = (unidad.jugadorId == 0) ? Propietario.Jugador : Propietario.IA;
                banderasConquistadas.Add(celda);
                
                if (unidad.jugadorId == 0) PonerBanderaAzul(celda);
                else PonerBanderaRoja(celda); // Necesitaremos un sprite para la IA

                ActualizarTextos();
            }
        }
    }

    // Cambia el dibujo de la bandera (hijo "SpriteBandera" de la celda) por el azul
    private void PonerBanderaAzul(HexCelda celda)
    {
        Transform dibujo = celda.transform.Find("SpriteBandera");
        if (dibujo == null) return;
        dibujo.GetComponent<SpriteRenderer>().sprite = spriteBanderaAzul;
    }

    private void PonerBanderaRoja(HexCelda celda)
    {
        // Por ahora opcional si no tenemos el sprite, pero prepara el código
        Transform dibujo = celda.transform.Find("SpriteBandera");
        if (dibujo == null) return;
        // dibujo.GetComponent<SpriteRenderer>().color = Color.red; // Fallback visual
    }

    // Resta monedas si hay suficientes. Devuelve true si se ha podido pagar.
    public bool Gastar(int jugadorId, int cantidad)
    {
        int saldo = GetMonedas(jugadorId);
        if (saldo < cantidad) return false;

        monedasPorJugador[jugadorId] -= cantidad;
        ActualizarTextos();
        return true;
    }

    private void ActualizarTextos()
    {
        if (textoMonedas != null) textoMonedas.text = GetMonedas(0).ToString();
        if (textoBanderas != null) textoBanderas.text = banderasConquistadas.FindAll(c => c.propietario == Propietario.Jugador).Count.ToString();
    }
}
