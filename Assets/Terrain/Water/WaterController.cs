using UnityEngine;

public class WaterController : MonoBehaviour
{
    [SerializeField] private Transform Target;
    [SerializeField] private float WaterLevel = 10f;
    [Range(0f, 100f)]
    [SerializeField] private float CoveragePercent = 90f;

    private float diameterMul;

    private void Start()
    {
        float angle = Mathf.PI / 2f * (CoveragePercent / 100f);
        diameterMul = 2f * Mathf.Tan(angle);
    }

    private void Update()
    {
        UpdatePosition();
        UpdateScale();
    }

    private void UpdateScale()
    {
        float height = Target.position.y - WaterLevel;
        float radius = diameterMul * height;
        transform.localScale = new Vector3(radius, radius, 0f);
    }

    private void UpdatePosition()
    {
        transform.position = new Vector3(Target.position.x, WaterLevel, Target.position.z);
    }

    private void Reset()
    {
        Target = Camera.main.transform;
    }
}
