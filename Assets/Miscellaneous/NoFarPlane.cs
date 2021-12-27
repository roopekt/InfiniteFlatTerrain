using UnityEngine;

//gets the camera component and moves the far plane infinitely far
[RequireComponent(typeof(Camera))]
public class NoFarPlane : MonoBehaviour
{
    void Start()
    {
        Camera cam = GetComponent<Camera>();

        Matrix4x4 matrix = cam.projectionMatrix;

        //see http://www.terathon.com/gdc07_lengyel.pdf
        float epsilon = (float)System.Math.Pow(2, -21);//according to above site, 2^-22 is the minimum value
        matrix[2, 2] = epsilon - 1;
        matrix[2, 3] = (epsilon - 2) * cam.nearClipPlane;
        matrix[3, 2] = -1;

        cam.projectionMatrix = matrix;
    }
}