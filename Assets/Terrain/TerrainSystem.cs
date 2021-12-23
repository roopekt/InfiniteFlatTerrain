using UnityEngine;
using UnityEngine.Assertions;

public class TerrainSystem : MonoBehaviour
{
    [SerializeField] private Transform Target;
    [Tooltip("Width of one square (two triangles) on screen in pixels.")]
    [SerializeField] private float SquareWidth = 5f;
    [Tooltip("Number of subsectors in the sector mesh.")]
    [SerializeField] private int SubsectorCount = 5;
    [Tooltip("If set to 90%, horizon will be 9 degrees below it's correct position.\n10% * 90 degrees = 9 degrees")]
    [Range(0f, 100f)]
    [SerializeField] private float CoveragePercent = 95f;
    [SerializeField] private GameObject SectorPrefab;
    [Tooltip("TextureRendering.compute")]
    [SerializeField] private ComputeShader ComputeShaderAsset;

    private Transform[] SectorPool;//pool of gameobjects with the sector mesh
    private Mesh sectorMesh;
    private Vector2Int lastWindowSize = Vector2Int.zero;

    //for shaders
    private int kernelId_renderTextures;
    private RenderTexture vertexTexture;
    //private bool writeTargetSelect = false;
    private Vector3Int dispatch_threadGroupCounts;

    //uniform name ids
    private int uTerrain_targetPos;
    private int uTerrain_writeTargetSelect;

    private void Update()
    {
        if (IsWindowSizeChanged())
            Init();

        StartTextureRender();
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
        //Init will be called again, if window size changes. this ensures that previous data will be freed
        OnDestroy();

        #region sector mesh
        //get FOVs
        Camera cam = Camera.current;
        if (cam == null) cam = Camera.main;
        float FOVy = Mathf.Deg2Rad * cam.fieldOfView;//vertical FOV
        float FOVx = 2 * Mathf.Atan(Mathf.Tan(FOVy / 2) * cam.aspect);//horizontal FOV

        float pixelCircleX = Utility.tau / FOVx * Screen.width;
        float pixelCircleY = Utility.tau / FOVy * Screen.height;

        int bigSectorCount = (int)(pixelCircleX / SquareWidth) / SubsectorCount;//number of sector meshes
        int radius = (int)((pixelCircleY / 4f) / SquareWidth);//number of "rings of vertices" raround the centre

        float bigSectorAngle = Utility.tau / bigSectorCount;

        //build the mesh
        sectorMesh = GetSectorMesh(radius, SubsectorCount, bigSectorAngle);
        sectorMesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e10f);//disable culling
        #endregion

        #region sector pool
        Transform sectorParent = new GameObject("Sectors").transform;
        sectorParent.parent = transform;

        SectorPool = new Transform[bigSectorCount];
        for (int i = 0; i < bigSectorCount; i++)
        {
            var sector = Instantiate(
                    SectorPrefab,
                    Vector3.right * i * SubsectorCount,
                    Quaternion.identity,
                    sectorParent
                ).transform;
            sector.GetComponent<MeshFilter>().sharedMesh = sectorMesh;//set mesh
            sector.name = "Sector" + i.ToString();
            
            SectorPool[i] = sector;
        }
        #endregion

        SetupShaders(radius, bigSectorCount * SubsectorCount);
    }
    private void OnDestroy()
    {
        //destroy children
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        
        //destroy mesh
        if (sectorMesh)
            Destroy(sectorMesh);

        //if vertex texture isn't null, release it
        vertexTexture?.Release();
    }
    Mesh GetSectorMesh(
        int radius,//number of "rings of vertices" raround the centre
        int subsectorCount,//number of internal sectors within this sector
        float angle)//total angle in radians
    {
        //number of straight lines shooting from the centre
        int rayCount = subsectorCount + 1;

        //vertex and index array allocation
        //vertices fill function as uvs
        var vertices = new Vector3[rayCount * radius//other vertices
            + 1];//centre vertex
        var indeces = new int[2 * 3 * subsectorCount * (radius - 1)//other triangles
            + 3 * subsectorCount];//centre triangles

        //calculate all vertices and triangles except the ones in centre
        int vtxI = 0;//vertex index
        int idxI = 0;//index index
        for (int r = 1; r < radius + 1; r++)
            for (int ang = 0; ang < rayCount; ang++)
            {
                //triangles
                if (r < radius && ang < rayCount - 1)
                {
                    indeces[idxI++] = vtxI;
                    indeces[idxI++] = vtxI + 1;
                    indeces[idxI++] = vtxI + (rayCount + 1);
                    
                    indeces[idxI++] = vtxI;
                    indeces[idxI++] = vtxI + (rayCount + 1);
                    indeces[idxI++] = vtxI + rayCount;
                }

                //vertex
                vertices[vtxI++] = new Vector3(ang, r, 0f);
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

        //uvs
        var uvs = new Vector2[vertices.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = vertices[i] / 2f;
        }

        //prevent rounding errors when sampling texture in vertex shader
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = vertices[i] + Vector3.one * .5f;

        //create Mesh object
        Mesh mesh = new Mesh();
        mesh.vertices = vertices;
        mesh.triangles = indeces;
        mesh.uv = uvs;

        return mesh;
    }

    void SetupShaders(int radius, int littleSectorCount)
    {
        //errors
        if (!SystemInfo.supportsComputeShaders)
            Debug.LogError("Compute shaders not supported.");
        if (!SystemInfo.supportsAsyncGPUReadback)
            Debug.LogError("AsyncGPUReadback not supported.");

        Material Mat = SectorPrefab.GetComponent<MeshRenderer>().sharedMaterial;

        //find RenderTextures kernel
        kernelId_renderTextures = ComputeShaderAsset.FindKernel("RenderTextures");

        //create the vertex texture
        var vertexTextureDesc = new RenderTextureDescriptor(2 * (littleSectorCount + 1), radius + 1, RenderTextureFormat.ARGBFloat, 0, 1);
        vertexTextureDesc.enableRandomWrite = true;
        vertexTexture = new RenderTexture(vertexTextureDesc);
        vertexTexture.wrapMode = TextureWrapMode.Repeat;
        vertexTexture.Create();

        //bind the vertex texture
        string bufferName = "bTerrain_VertexTexturePair";
        ComputeShaderAsset.SetTexture(kernelId_renderTextures, bufferName, vertexTexture, 0);
        Mat.SetTexture("bTerrain_VertexTexturePair", vertexTexture);

        #region setup uniforms
        //target pos
        uTerrain_targetPos = Shader.PropertyToID("uTerrain_targetPos");

        //little sector count
        ComputeShaderAsset.SetInt("uTerrain_littleSectorCount", littleSectorCount);
        Mat.SetFloat("uTerrain_littleSectorCount", littleSectorCount);

        //radius
        ComputeShaderAsset.SetInt("uTerrain_radius", radius);
        Mat.SetFloat("uTerrain_radius", radius);

        //coverage percent
        ComputeShaderAsset.SetFloat("uTerrain_coveragePercent", CoveragePercent / 100f);

        //write target selector
        uTerrain_writeTargetSelect = Shader.PropertyToID("uTerrain_writeTargetSelect");
        #endregion

        //calculate number of thread groups along each axis for dispathing the RenderTextures kernel
        uint sizeX, sizeY;
        ComputeShaderAsset.GetKernelThreadGroupSizes(kernelId_renderTextures, out sizeX, out sizeY, out _);
        dispatch_threadGroupCounts = new Vector3Int(
                Mathf.CeilToInt((littleSectorCount + 1) / (float)sizeX),
                Mathf.CeilToInt((radius + 1) / (float)sizeY),
                1
            );
    }

    void StartTextureRender()
    {
        //set uniforms
        Vector3 pos = Target.position;
        ComputeShaderAsset.SetFloats(uTerrain_targetPos, pos.x, pos.y, pos.z);
        ComputeShaderAsset.SetBool(uTerrain_writeTargetSelect, false);

        //dispatch
        var tgCounts = dispatch_threadGroupCounts;
        ComputeShaderAsset.Dispatch(kernelId_renderTextures, tgCounts.x, tgCounts.y, tgCounts.z);
    }
}