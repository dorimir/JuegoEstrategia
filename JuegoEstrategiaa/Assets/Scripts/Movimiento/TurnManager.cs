using System;
using System.Collections.Generic;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Instancia { get; private set; }

    [SerializeField] private int numeroJugadores = 2;
    [Tooltip("Indices de los jugadores controlados por la IA (0 = Jugador 1, 1 = Jugador 2...)")]
    [SerializeField] private List<int> jugadoresIA = new List<int> { 1 };

    public int JugadorActual { get; private set; } = 0;
    public int NumeroTurno { get; private set; } = 1;
    public bool TurnoDeIA => jugadoresIA.Contains(JugadorActual);

    // (jugador que empieza, numero de turno)
    public event Action<int, int> OnTurnoIniciado;

    private void Awake()
    {
        if (Instancia == null) Instancia = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        IniciarTurno();
    }

    private void Update()
    {
        // El humano solo puede pasar turno en SU turno
        if (!TurnoDeIA && Input.GetKeyDown(KeyCode.Space)) FinalizarTurno();
    }

    public void FinalizarTurno()
    {
        // No se puede pasar turno mientras una unidad se esta moviendo
        if (Unidad.HayAlguienMoviendose()) return;

        JugadorActual = (JugadorActual + 1) % numeroJugadores;
        if (JugadorActual == 0) NumeroTurno++;

        IniciarTurno();
    }

    private void IniciarTurno()
    {
        Unidad.Seleccionada = null;
        if (SelectionManager.Instancia != null && SelectionManager.Instancia.celdaSeleccionada != null)
        {
            SelectionManager.Instancia.celdaSeleccionada.EstablecerSeleccionada(false);
            SelectionManager.Instancia.celdaSeleccionada = null;
        }

        foreach (Unidad u in Unidad.Todas)
        {
            if (u.jugadorId == JugadorActual) u.NuevoTurno();
        }

        Debug.Log($"--- Turno {NumeroTurno} | Jugador {JugadorActual + 1} {(TurnoDeIA ? "(IA)" : "")} ---");
        OnTurnoIniciado?.Invoke(JugadorActual, NumeroTurno);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 300, 25), $"Turno {NumeroTurno} - Jugador {JugadorActual + 1}{(TurnoDeIA ? " (IA pensando...)" : "")}");

        GUI.enabled = !TurnoDeIA;
        if (GUI.Button(new Rect(10, 40, 160, 30), "Fin de turno (Espacio)"))
        {
            FinalizarTurno();
        }
        GUI.enabled = true;
    }
}
