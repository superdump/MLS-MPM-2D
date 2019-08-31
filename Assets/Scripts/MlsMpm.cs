using UnityEngine;
using Unity.Mathematics;

public class MlsMpm : MonoBehaviour {
    struct Particle {
        public float2 x;
        public float2 v;
        public float2x2 F;
        public float2x2 C;
        public float Jp;
    }
    private ComputeBuffer particles;
    private Particle[] pArray;
    private RenderTexture grid;
    private GameObject[] cubes;

    public GameObject parent;
    public float cubeScale = 0.01f;
    public ComputeShader shader;

    const int BLOCK_SIZE = 8;
    const int PPS = 1024;
    const int steps_per_frame = 10;
    public int gridResolution = 10 * BLOCK_SIZE;

    // Start is called before the first frame update
    void Start() {
        particles = new ComputeBuffer(
            PPS * 3,
            sizeof(float) * 13,
            ComputeBufferType.Default
        );

        pArray = new Particle[PPS * 3];
        AddParticlesInSquare(
            PPS * 0,
            new float2(0.5f, 0.25f)
        );
        AddParticlesInSquare(
            PPS * 1,
            new float2(0f, 2f)
        );
        AddParticlesInSquare(
            PPS * 2,
            new float2(0.5f, 3.75f)
        );
        particles.SetData(pArray);

        cubes = new GameObject[PPS * 3];

        Color[] colors = {
            new Color(0.93f, 0.33f, 0.23f, 0.8f),
            new Color(0.95f, 0.69f, 0.20f, 0.8f),
            new Color(0.09f, 0.52f, 0.53f, 0.8f)
        };
        for (int i = 0; i < PPS * 3; i++) {
            Particle p = pArray[i];
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(parent.transform);
            cube.transform.localPosition = new Vector3(p.x.x, p.x.y, 0.0f);
            cube.transform.localScale = new Vector3(cubeScale, cubeScale, cubeScale);
            cube.GetComponent<Renderer>().material.color = colors[i / PPS];
            cubes[i] = cube;
        }

        grid = new RenderTexture(
            gridResolution, gridResolution, 0,
            RenderTextureFormat.ARGBFloat,
            RenderTextureReadWrite.Linear
        );
        grid.enableRandomWrite = true;
    }

    void ClearGrid() {
        int kernelHandle = shader.FindKernel("CSMainClearGrid");
        shader.SetTexture(kernelHandle, "Grid", grid);
        shader.Dispatch(
            kernelHandle,
            gridResolution / BLOCK_SIZE,
            gridResolution / BLOCK_SIZE,
            1
        );
    }

    void ProcessP2G() {
        // Debug.Log("Running ProcessP2G");
        int kernelHandle = shader.FindKernel("CSMainP2G");
        shader.SetBuffer(kernelHandle, "Particles", particles);
        shader.SetTexture(kernelHandle, "Grid", grid);
        shader.Dispatch(
            kernelHandle,
            3 * PPS / 16,
            1,
            1
        );
    }

    void ProcessGrid() {
        int kernelHandle = shader.FindKernel("CSMainGrid");
        shader.SetTexture(kernelHandle, "Grid", grid);
        shader.Dispatch(
            kernelHandle,
            gridResolution / BLOCK_SIZE,
            gridResolution / BLOCK_SIZE,
            1
        );
    }

    void ProcessG2P() {
        int kernelHandle = shader.FindKernel("CSMainG2P");
        shader.SetBuffer(kernelHandle, "Particles", particles);
        shader.SetTexture(kernelHandle, "Grid", grid);
        shader.Dispatch(
            kernelHandle,
            3 * PPS / 16,
            1,
            1
        );

        particles.GetData(pArray, 0, 0, PPS * 3);
        for (int i = 0; i < PPS * 3; i++) {
            cubes[i].transform.localPosition = new Vector3(
                pArray[i].x.x,
                pArray[i].x.y,
                0.0f
            );
        }
    }

    void UpdateTextureFromCompute() {
        ClearGrid();
        ProcessP2G();
        ProcessGrid();
        ProcessG2P();
    }

    // Update is called once per frame
    void Update() {
        for (int i = 0; i < steps_per_frame; i++) {
            UpdateTextureFromCompute();
        }
    }

    private void OnDestroy() {
        particles.Release();
        grid.Release();
    }

    void AddParticlesInSquare(int offset, float2 center) {
        for (int i = offset; i < offset + PPS; i++) {
            pArray[i] = ParticleAtCenterWithColor(center);
        }
    }

    Particle ParticleAtCenterWithColor(float2 center) {
        const float squareSize = 0.75f;
        return new Particle {
            x = new float2(
                UnityEngine.Random.Range(center.x - squareSize, center.x + squareSize),
                UnityEngine.Random.Range(center.y - squareSize, center.y + squareSize)
            ),
            v = new float2(0.0f),
            F = new float2x2(1.0f, 0.0f, 0.0f, 1.0f),
            C = new float2x2(0.0f),
            Jp = 1.0f
        };
    }
}
