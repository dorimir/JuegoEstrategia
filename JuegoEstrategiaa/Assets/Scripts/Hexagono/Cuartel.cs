using UnityEngine;

// Se pone en una celda de tipo Cuartel para decir de quien es (aliado o enemigo).
// - Copia el propietario a la HexCelda, que es lo que leen el resto de scripts (por ejemplo el mapa de influencia).
// - Pone el sprite de aliado o de enemigo segun el propietario.
// - Si es del jugador, al hacer clic abre el menu para crear tropas.
[RequireComponent(typeof(HexCelda))]
public class Cuartel : MonoBehaviour
{
    [SerializeField] private Propietario propietario = Propietario.Jugador;

    [Header("Sprites")]
    [SerializeField] private SpriteRenderer dibujo;   // el hijo con el dibujo del cuartel
    [SerializeField] private Sprite spriteAliado;
    [SerializeField] private Sprite spriteEnemigo;

    public Propietario Duenio => propietario;

    private void Reset()
    {
        HexCelda celda = GetComponent<HexCelda>();
        celda.ConfigurarCelda(celda.q, celda.r, TipoTerreno.Cuartel);
        AplicarPropietario();
    }

    // Al cambiar algo en el Inspector
    private void OnValidate()
    {
        AplicarPropietario();
    }

    // Al empezar la partida
    private void Awake()
    {
        AplicarPropietario();
    }

    private void AplicarPropietario()
    {
        HexCelda celda = GetComponent<HexCelda>();
        celda.propietario = propietario;

        if (dibujo == null) return; // todavia no tiene dibujo

        if (propietario == Propietario.Jugador && spriteAliado != null) dibujo.sprite = spriteAliado;
        if (propietario == Propietario.IA && spriteEnemigo != null) dibujo.sprite = spriteEnemigo;
    }

    // Clic en el cuartel: solo los del jugador abren el menu de tropas
    private void OnMouseDown()
    {
        if (propietario != Propietario.Jugador) return;
        if (TurnManager.Instancia != null && TurnManager.Instancia.TurnoDeIA) return;

        MenuSpawn.Instancia.Abrir(this);
    }
}
