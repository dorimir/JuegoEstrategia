using UnityEngine;

public class GameMaster : MonoBehaviour
{
    [Header("Audio de la Alerta")]
    [SerializeField] private Unidad unidadSanta;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sonidoCampana;
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))  //temporalmente para probar la funcion
        {
            PresionarCampanaPeligro();
        }
    }
        public void PresionarCampanaPeligro()
    {
        if (unidadSanta == null) return;
        if (audioSource != null && sonidoCampana != null)
        {
            audioSource.PlayOneShot(sonidoCampana);
        }
        else
        {
            AudioSource.PlayClipAtPoint(sonidoCampana, unidadSanta.transform.position);
        }
        UnidadSecundaria.ActivarCampanaPeligro(unidadSanta);
        Debug.Log("It's ringning!"); 
    }
 
}
