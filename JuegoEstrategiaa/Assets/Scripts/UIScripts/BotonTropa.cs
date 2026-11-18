using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Va en cada boton de tropa del menu del cuartel.
// - Dice que tropa compra el boton.
// - Muestra su precio y lo pone en rojo si no tienes monedas suficientes.
[RequireComponent(typeof(Button))]
public class BotonTropa : MonoBehaviour
{
    [SerializeField] private Unidad tropa;               // prefab de la tropa que se compra
    [SerializeField] private TextMeshProUGUI textoPrecio;
    [SerializeField] private Color colorSinSaldo = Color.red;

    private Color colorNormal;

    private void Awake()
    {
        colorNormal = textoPrecio.color;
        GetComponent<Button>().onClick.AddListener(Comprar);
    }

    // Para ver el precio en el editor sin darle a Play
    private void OnValidate()
    {
        if (tropa != null && textoPrecio != null) textoPrecio.text = tropa.Coste.ToString();
    }

    private void Update()
    {
        textoPrecio.text = tropa.Coste.ToString();

        bool tengoSaldo = MonedasYBanderas.Instancia.Monedas >= tropa.Coste;
        textoPrecio.color = tengoSaldo ? colorNormal : colorSinSaldo;
    }

    private void Comprar()
    {
        MenuSpawn.Instancia.Spawnear(tropa);
    }
}
