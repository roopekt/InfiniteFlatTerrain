using UnityEngine.InputSystem;
using UnityEngine;

public class ExitOnEsc : MonoBehaviour
{
    private void Update()
    {
        if (Keyboard.current.escapeKey.isPressed)
            Quit();
    }

    private static void Quit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
