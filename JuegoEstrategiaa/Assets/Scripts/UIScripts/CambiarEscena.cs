using UnityEngine;
using UnityEngine.SceneManagement;

// Script para los botones del menu.
// Se asigna en el OnClick() del boton y se escribe el nombre de la escena.
public class CambiarEscena : MonoBehaviour
{
    // Carga la escena con el nombre indicado (debe estar en Build Settings)
    public void CargarEscena(string nombreEscena)
    {
        SceneManager.LoadScene(nombreEscena);
    }

    // Cierra el juego
    public void Salir()
    {
        Application.Quit();

#if UNITY_EDITOR
        // En el editor Application.Quit no hace nada, asi que paramos el Play
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
