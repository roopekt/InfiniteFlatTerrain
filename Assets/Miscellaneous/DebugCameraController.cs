using UnityEngine;
using UnityEngine.InputSystem;

//simple flying camera controller
public class DebugCameraController : MonoBehaviour
{
    [SerializeField] private float FlyingSpeed = 5f;
    [SerializeField] private float MouseSensitivity = 10f;

    private float angX = 0f;
    private float angY = 0f;

    private void Update()
    {
        Fly();
        TurnCamera();
    }

    void Fly()
    {
        //read input
        Vector3 input = Vector3.zero;
        if (Keyboard.current.aKey.isPressed) input += Vector3.left;
        if (Keyboard.current.dKey.isPressed) input += Vector3.right;
        if (Keyboard.current.sKey.isPressed) input += Vector3.back;
        if (Keyboard.current.wKey.isPressed) input += Vector3.forward;
        if (Keyboard.current.shiftKey.isPressed) input += Vector3.down;
        if (Keyboard.current.spaceKey.isPressed) input += Vector3.up;

        //normalize
        if (input.sqrMagnitude > .1f)
            input = input.normalized;

        //rotate
        input = Quaternion.AngleAxis(angX, Vector3.up) * input;

        if (Keyboard.current.ctrlKey.isPressed)
            input *= 3;

        //move
        transform.position += input * FlyingSpeed * Time.deltaTime;
    }

    void TurnCamera()
    {
        //read input
        Vector2 input = Mouse.current.leftButton.isPressed ? Mouse.current.delta.ReadValue() : Vector2.zero;

        //update angles
        angX -= input.x * MouseSensitivity * Time.deltaTime;
        angY += input.y * MouseSensitivity * Time.deltaTime;
        angY = Mathf.Clamp(angY, -90f, 90f);

        //update transform with new angles
        transform.rotation = Quaternion.AngleAxis(angX, Vector3.up) * Quaternion.AngleAxis(angY, Vector3.right);
    }
}