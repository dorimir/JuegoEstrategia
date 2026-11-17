using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

// Efecto de rebote para botones. Solo hay que anadir este componente a cualquier boton.
// - Raton encima: crece con rebote
// - Raton fuera: vuelve a su tamano
// - Clic: pequeno "golpe" de rebote
public class BotonRebote : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] private float escalaHover = 1.1f;  // tamano al pasar el raton
    [SerializeField] private float escalaClic = 0.9f;   // tamano al hacer clic
    [SerializeField] private float duracion = 0.25f;    // segundos que dura cada rebote

    private Vector3 escalaOriginal;
    private float escalaActual = 1f;

    private void Awake()
    {
        escalaOriginal = transform.localScale;
    }

    // Si el objeto se desactiva a medio rebote, lo dejamos en su tamano normal
    private void OnDisable()
    {
        transform.localScale = escalaOriginal;
        escalaActual = 1f;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Rebotar(escalaHover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Rebotar(1f);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Se encoge de golpe y rebota de vuelta al tamano hover
        escalaActual = escalaClic;
        Rebotar(escalaHover);
    }

    private void Rebotar(float destino)
    {
        StopAllCoroutines();
        StartCoroutine(AnimarEscala(escalaActual, destino));
    }

    private IEnumerator AnimarEscala(float desde, float hasta)
    {
        float tiempo = 0f;

        while (tiempo < duracion)
        {
            // unscaledDeltaTime: funciona aunque el juego este en pausa (Time.timeScale = 0)
            tiempo += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(tiempo / duracion);

            escalaActual = Mathf.LerpUnclamped(desde, hasta, EaseOutBack(t));
            transform.localScale = escalaOriginal * escalaActual;
            yield return null;
        }

        escalaActual = hasta;
        transform.localScale = escalaOriginal * escalaActual;
    }

    // Curva que se pasa un poco del destino y vuelve: eso es lo que da el efecto rebote
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
