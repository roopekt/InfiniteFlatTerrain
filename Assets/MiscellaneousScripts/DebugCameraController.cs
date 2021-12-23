using UnityEngine;

//simple flying camera controller
public class DebugCameraController : MonoBehaviour
{
    [SerializeField] private float FlyingSpeed = 5f;
    [SerializeField] private float MouseSensitivity = 10f;

    private Vector3 lastMousePos;
    private float angX = 0f;
    private float angY = 0f;

    private void Start()
    {
        lastMousePos = Input.mousePosition;
    }

    private void Update()
    {
        Fly();
        TurnCamera();
    }

    void Fly()
    {
        //read input
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.A)) input += Vector3.left;
        if (Input.GetKey(KeyCode.D)) input += Vector3.right;
        if (Input.GetKey(KeyCode.S)) input += Vector3.back;
        if (Input.GetKey(KeyCode.W)) input += Vector3.forward;
        if (Input.GetKey(KeyCode.LeftShift)) input += Vector3.down;
        if (Input.GetKey(KeyCode.Space)) input += Vector3.up;

        //normalize
        if (input.sqrMagnitude > .1f)
            input = input.normalized;

        //rotate
        input = Quaternion.AngleAxis(angX, Vector3.up) * input;

        if (Input.GetKey(KeyCode.LeftControl))
            input *= 3;

        //move
        transform.position += input * FlyingSpeed * Time.deltaTime;
    }

    void TurnCamera()
    {
        //read input
        Vector3 input = Vector3.zero;
        if (Input.GetMouseButton(0))//is left button down?
            input = Input.mousePosition - lastMousePos;
        lastMousePos = Input.mousePosition;

        //update angles
        angX -= input.x * MouseSensitivity * Time.deltaTime;
        angY += input.y * MouseSensitivity * Time.deltaTime;
        angY = Mathf.Clamp(angY, -90f, 90f);

        //update transform with new angles
        transform.rotation = Quaternion.AngleAxis(angX, Vector3.up) * Quaternion.AngleAxis(angY, Vector3.right);
    }
}