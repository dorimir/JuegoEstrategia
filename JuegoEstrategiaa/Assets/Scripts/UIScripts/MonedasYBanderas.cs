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

    private List<HexCelda> banderasConquistadas = new List<HexCelda>();
    private int monedas = 0;
    private float temporizador = 0f;

    public int Monedas => monedas;

    private void Awake()
    {
        Instancia = this;
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
            monedas += banderasConquistadas.Count * monedasPorBandera;
            ActualizarTextos();
        }
    }

    // Si una unidad esta sobre una bandera que aun no tenemos, la anadimos a la lista
    private void ConquistarBanderas()
    {
        foreach (Unidad unidad in Unidad.Todas)
        {
            HexCelda celda = unidad.CeldaActual;
            if (unidad.jugadorId != jugadorId || celda == null) continue;

            bool esBandera = celda.terreno == TipoTerreno.Bandera || celda.terreno == TipoTerreno.BanderaCentral;

            if (esBandera && !banderasConquistadas.Contains(celda))
            {
                banderasConquistadas.Add(celda);
                PonerBanderaAzul(celda);
                ActualizarTextos();
            }
        }
    }

    // Cambia el dibujo de la bandera (hijo "SpriteBandera" de la celda) por el azul
    private void PonerBanderaAzul(HexCelda celda)
    {
        Transform dibujo = celda.transform.Find("SpriteBandera");
        if (dibujo == null) return; // esta celda no tiene dibujo de bandera

        dibujo.GetComponent<SpriteRenderer>().sprite = spriteBanderaAzul;
    }

    // Resta monedas si hay suficientes. Devuelve true si se ha podido pagar.
    public bool Gastar(int cantidad)
    {
        if (monedas < cantidad) return false;

        monedas -= cantidad;
        ActualizarTextos();
        return true;
    }

    private void ActualizarTextos()
    {
        textoMonedas.text = monedas.ToString();
        textoBanderas.text = banderasConquistadas.Count.ToString();
    }
}
