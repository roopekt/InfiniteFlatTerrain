using UnityEngine;
using UnityEngine.Assertions;

public class TerrainSystem : MonoBehaviour
{
    [Tooltip("Width of one square (two triangles) on screen in pixels.")]
    [SerializeField] private float SquareWidth = 5f;
    [Tooltip("Number of subsectors in the sector mesh.")]
    [SerializeField] private int SubsectorCount = 5;
    [SerializeField] private GameObject SectorPrefab;

    private Transform[] SectorPool;//pool of gameobjects with sector mesh

    private Vector2Int lastWindowSize = Vector2Int.zero;

    private void Update()
    {
        if (IsWindowSizeChanged())
            Init();
    }

    bool IsWindowSizeChanged()
    {
        Vector2Int newWindowSize = new Vector2Int(Screen.width, Screen.height);
        bool changed = lastWindowSize != newWindowSize;
        lastWindowSize = newWindowSize;

        return changed;
    }

    void Init()
    {
        #region sector mesh
        //get FOVs
        Camera cam = Camera.current;
        if (cam == null) cam = Camera.main;
        float FOVy = Mathf.Deg2Rad * cam.fieldOfView;//vertical FOV
        float FOVx = 2 * Mathf.Atan(Mathf.Tan(FOVy / 2) * cam.aspect);//horizontal FOV

        float pixelCircleX = Utility.tau / FOVx * Screen.width;
        float pixelCircleY = Utility.tau / FOVy * Screen.height;

        int bigSectorCount = (int)(pixelCircleX / SquareWidth) / SubsectorCount;//number of sector meshes
        int radius = (int)((pixelCircleY / 4f) / SquareWidth);

        float sectorAngle = Utility.tau / bigSectorCount;

        Mesh mesh = GetSectorMesh(radius, SubsectorCount, sectorAngle);
        
        #endregion

        #region sector pool
        Transform sectorParent = new GameObject("Sectors").transform;
        sectorParent.parent = transform;

        SectorPool = new Transform[bigSectorCount];
        for (int i = 0; i < bigSectorCount; i++)
        {
            var sector = Instantiate(
                    SectorPrefab,
                    Vector3.zero,
                    Quaternion.AngleAxis(Mathf.Rad2Deg * i * sectorAngle, Vector3.up),
                    sectorParent
                ).transform;
            SectorPool[i] = sector;
            sector.GetComponent<MeshFilter>().mesh = mesh;//set mesh
        }
        #endregion
    }

    Mesh GetSectorMesh(int radius, int subsectorCount, float angle)
    {
        //number of straight lines shooting from the centre
        int rayCount = subsectorCount + 1;

        //vertex and index array allocation
        var vertices = new Vector3[rayCount * (radius - 1)//other vertices
            + 1];//centre vertex
        var indeces = new int[2 * 3 * subsectorCount * (radius - 2)//other triangles
            + 3 * subsectorCount];//centre triangles

        //calculate all vertices and triangles except the ones in centre
        int vtxI = 0;//vertex index
        int idxI = 0;//index index
        for (int r = 1; r < radius; r++)
            for (int ang = 0; ang < rayCount; ang++)
            {
                //triangles
                if (r < radius - 1 && ang < rayCount - 1)
                {
                    indeces[idxI++] = vtxI;
                    indeces[idxI++] = vtxI + 1;
                    indeces[idxI++] = vtxI + (rayCount + 1);
                    
                    indeces[idxI++] = vtxI;
                    indeces[idxI++] = vtxI + (rayCount + 1);
                    indeces[idxI++] = vtxI + rayCount;
                }
                
                //vertex
                float trueAng = (float)ang / subsectorCount * angle;
                vertices[vtxI++] = new Vector3(
                    r * Mathf.Cos(trueAng),
                    0f,
                    r * Mathf.Sin(trueAng)
                );
            }
        
        //place the centre triangles
        for (int i = 0; i < subsectorCount; i++)
        {
            indeces[idxI++] = vtxI;
            indeces[idxI++] = i + 1;
            indeces[idxI++] = i;
        }
        Assert.AreEqual(idxI, indeces.Length);

        //place the centre vertex
        vertices[vtxI++] = Vector3.zero;
        Assert.AreEqual(vtxI, vertices.Length);

        //create Mesh object
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = indeces;

        return mesh;
    }
}
