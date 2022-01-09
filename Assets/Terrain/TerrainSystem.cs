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
    [SerializeField] private int FramesPerDispatch = 5;
    [SerializeField] private NoiseParams HeightMapParams;
    [SerializeField] private NoiseParams MountainHeightMapParams;
    [SerializeField] private MountainMaskNoiseParams MountainMaskParams;
    [SerializeField] private GameObject SectorPrefab;
    [Tooltip("TextureRendering.compute")]
    [SerializeField] private ComputeShader ComputeShaderAsset;

    private Transform[] SectorPool;//pool of gameobjects with the sector mesh
    private Mesh sectorMesh;
    private Vector2Int lastWindowSize = Vector2Int.zero;
    private Dispatcher dispatcher;
    private Material sectorMaterial;

    private RenderTexture vertexTexture;
    private RenderTexture normalTexture;

    [System.Serializable]
    private class NoiseParams
    {
        [Tooltip("Wavelength of the biggest wave")]
        public float majorWavelength = 100f;

        [Tooltip("Amplitude of the biggest wave")]
        public float majorAmplitude = 120f;

        public float verticesPerWave = 4f;
    }

    [System.Serializable]
    private class MountainMaskNoiseParams
    {
        [Tooltip("Wavelength of the biggest wave")]
        public float majorWavelength = 100f;

        [Tooltip("Higher values mean less mountains")]
        public float exponent = 5f;

        public float verticesPerWave = 4f;
    }

    private class Dispatcher
    {
        private readonly int framesPerDispatch;
        private readonly Vector2Int threadGroupSizes;
        private readonly Vector3Int renderTextures_threadGroupCounts;
        private readonly Vector3Int renderNormals_threadGroupCounts;
        private readonly ComputeShader computeShader;
        private readonly Material material;
        private readonly Transform target;
        private readonly Vector2Int textureSize;

        private readonly int uTerrain_targetPos;
        private readonly int uTerrain_writeTargetSelect;
        private readonly int uTerrain_uvOfset;
        private readonly int uTerrain_interpolator;
        private readonly int kernelId_renderTextures;
        private readonly int kernelId_renderNormals;

        private int writeTargetSelect = 0;
        private int frameCounter = 0;
        private int columnsDone;

        public Dispatcher(int framesPerDispatch, ComputeShader computeShader, Material material, Transform target, Vector2Int textureSize)
        {
            this.framesPerDispatch = framesPerDispatch;
            this.computeShader = computeShader;
            this.material = material;
            this.target = target;
            this.textureSize = textureSize;

            uTerrain_targetPos = Shader.PropertyToID("uTerrain_targetPos");
            uTerrain_writeTargetSelect = Shader.PropertyToID("uTerrain_writeTargetSelect");
            uTerrain_uvOfset = Shader.PropertyToID("uTerrain_uvOfset");
            uTerrain_interpolator = Shader.PropertyToID("uTerrain_interpolator");
            kernelId_renderTextures = computeShader.FindKernel("RenderTextures");
            kernelId_renderNormals = computeShader.FindKernel("RenderNormals");

            uint sizeX, sizeY;
            computeShader.GetKernelThreadGroupSizes(computeShader.FindKernel("RenderTextures"), out sizeX, out sizeY, out _);
            threadGroupSizes = new Vector2Int((int)sizeX, (int)sizeY);
            renderTextures_threadGroupCounts = new Vector3Int(
                    Mathf.CeilToInt(textureSize.x / (float)sizeX),
                    Mathf.CeilToInt(textureSize.y / (float)sizeY),
                    1
                );
            renderNormals_threadGroupCounts = renderTextures_threadGroupCounts;
            renderTextures_threadGroupCounts.x = Mathf.CeilToInt(renderTextures_threadGroupCounts.x / (float)(framesPerDispatch - 1));

            InitDispatchCycle();
        }

        //should be called once every frame
        public void AdvanceFrame()
        {
            if (frameCounter >= framesPerDispatch)
                InitDispatchCycle();

            Dispatch(frameCounter);
            UpdateInterpolator();

            frameCounter++;
        }

        void InitDispatchCycle()
        {
            frameCounter = 0;
            columnsDone = 0;
            writeTargetSelect = (writeTargetSelect + 1) % 3;
            
            //set uniforms
            Vector3 pos = target.position;
            computeShader.SetFloats(uTerrain_targetPos, pos.x, pos.y, pos.z);
            computeShader.SetInt(uTerrain_writeTargetSelect, writeTargetSelect);
            material.SetFloat(uTerrain_writeTargetSelect, (float)writeTargetSelect);
        }

        void Dispatch(int frame)
        {
            if (frame < framesPerDispatch - 1)
            {
                var groupCounts = renderTextures_threadGroupCounts;
                int columnsLeft = textureSize.x - columnsDone;
                groupCounts.x = Mathf.Min(groupCounts.x, columnsLeft);

                if (groupCounts.x > 0)
                {
                    computeShader.SetInts(uTerrain_uvOfset, columnsDone * threadGroupSizes.x, 0, 0);
                    computeShader.Dispatch(kernelId_renderTextures, groupCounts.x, groupCounts.y, groupCounts.z);
                }

                columnsDone += groupCounts.x;
            }
            else//last frame for this dispatch cycle
            {
                var groupCounts = renderNormals_threadGroupCounts;
                computeShader.Dispatch(kernelId_renderNormals, groupCounts.x, groupCounts.y, groupCounts.z);
            }
        }

        void UpdateInterpolator()
        {
            float interpolator = frameCounter / (float)framesPerDispatch;
            material.SetFloat(uTerrain_interpolator, interpolator);
        }
    }

    private void Update()
    {
        if (IsWindowSizeChanged())
            Init();

        dispatcher.AdvanceFrame();
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

        int littleSectorCount = bigSectorCount * SubsectorCount;
        Vector2Int textureSize = new Vector2Int(littleSectorCount + 1, radius + 1);

        sectorMaterial = SectorPrefab.GetComponent<MeshRenderer>().sharedMaterial;
        dispatcher = new Dispatcher(FramesPerDispatch, ComputeShaderAsset, sectorMaterial, Target, textureSize);
        SetupShaders(radius, littleSectorCount);
    }

    private void OnDestroy()
    {
        //destroy children
        foreach (Transform child in transform)
            Destroy(child.gameObject);
        
        //destroy mesh
        if (sectorMesh)
            Destroy(sectorMesh);

        //if not null, release
        vertexTexture?.Release();
        normalTexture?.Release();
    }

    Mesh GetSectorMesh(
        int radius,//number of "rings of vertices" around the centre
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
            vertices[i] = vertices[i] + new Vector3(.5f, .5f, 0f);

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

        //find kernels
        int kernelId_renderTextures = ComputeShaderAsset.FindKernel("RenderTextures");
        int kernelId_renderNormals = ComputeShaderAsset.FindKernel("RenderNormals");

        #region setup textures
            const int bufferCount = 3;
            Vector2Int textureSize = new Vector2Int(littleSectorCount + 1, radius + 1);

            //create vertex texture
            var vertexTextureDesc = new RenderTextureDescriptor(bufferCount * textureSize.x, textureSize.y, RenderTextureFormat.ARGBFloat, 0, 1);
            vertexTextureDesc.enableRandomWrite = true;
            vertexTexture = new RenderTexture(vertexTextureDesc);
            vertexTexture.Create();

            //bind vertex texture
            string bufferName = "bTerrain_VertexTextureSet";
            ComputeShaderAsset.SetTexture(kernelId_renderTextures, bufferName, vertexTexture, 0);
            ComputeShaderAsset.SetTexture(kernelId_renderNormals, bufferName, vertexTexture, 0);
            sectorMaterial.SetTexture(bufferName, vertexTexture);

            //create normal texture
            var normalTextureDesc = vertexTextureDesc;
            normalTexture = new RenderTexture(normalTextureDesc);
            normalTexture.Create();

            //bind normal texture
            bufferName = "bTerrain_NormalTextureSet";
            ComputeShaderAsset.SetTexture(kernelId_renderNormals, bufferName, normalTexture, 0);
            sectorMaterial.SetTexture(bufferName, normalTexture);
        #endregion

        #region setup uniforms
            ComputeShaderAsset.SetInt("uTerrain_littleSectorCount", littleSectorCount);
            ComputeShaderAsset.SetInt("uTerrain_radius", radius);
            sectorMaterial.SetVector("uTerrain_textureSize", (Vector2)textureSize);
            ComputeShaderAsset.SetFloat("uTerrain_coveragePercent", CoveragePercent / 100f);

            ComputeShaderAsset.SetFloat("uTerrainHeightMap_amplitudeMul", HeightMapParams.majorAmplitude / 2f);
            ComputeShaderAsset.SetFloat("uTerrainHeightMap_minorFreq", 1f / HeightMapParams.majorWavelength);
            ComputeShaderAsset.SetFloat("uTerrainHeightMap_verticesPerWave", HeightMapParams.verticesPerWave);

            ComputeShaderAsset.SetFloat("uTerrainMountHeightMap_amplitudeMul", MountainHeightMapParams.majorAmplitude / 2f);
            ComputeShaderAsset.SetFloat("uTerrainMountHeightMap_minorFreq", 1f / MountainHeightMapParams.majorWavelength);
            ComputeShaderAsset.SetFloat("uTerrainMountHeightMap_verticesPerWave", MountainHeightMapParams.verticesPerWave);

            ComputeShaderAsset.SetFloat("uTerrainMountMask_minorFreq", 1f / MountainMaskParams.majorWavelength);
            ComputeShaderAsset.SetFloat("uTerrainMountMask_verticesPerWave", MountainMaskParams.verticesPerWave);
            ComputeShaderAsset.SetFloat("uTerrainMountMask_exponent", MountainMaskParams.exponent);
        #endregion
    }
}