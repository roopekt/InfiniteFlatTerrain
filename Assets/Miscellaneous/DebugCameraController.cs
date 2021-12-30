using UnityEngine;
using UnityEngine.InputSystem;

//simple flying camera controller
public class DebugCameraController : MonoBehaviour
{
    [SerializeField] private float FlyingSpeed = 5f;
    [SerializeField] private float MouseSensitivity = 10f;
    [SerializeField] private float ScrollSensitivity = .3f;

    private float angX = 0f;
    private float angY = 0f;

    private void Update()
    {
        Fly();
        TurnCamera();
        AdjustSpeed();
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

        //move
        transform.position += input * FlyingSpeed * Time.deltaTime;
    }

    void TurnCamera()
    {
        //read input
        Vector2 delta = Mouse.current.leftButton.isPressed ? Mouse.current.delta.ReadValue() : Vector2.zero;

        //update angles
        angX -= delta.x * MouseSensitivity * Time.deltaTime;
        angY += delta.y * MouseSensitivity * Time.deltaTime;
        angY = Mathf.Clamp(angY, -90f, 90f);

        //update transform with new angles
        transform.rotation = Quaternion.AngleAxis(angX, Vector3.up) * Quaternion.AngleAxis(angY, Vector3.right);
    }

    void AdjustSpeed()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;

        float speedUpPerSecond = Mathf.Pow(2f, scroll * ScrollSensitivity);
        float speedUp = Mathf.Pow(speedUpPerSecond, Time.deltaTime);//speedUp ^ (1 / deltaTime) == speedUpPerSecond

        FlyingSpeed *= speedUp;
    }
}