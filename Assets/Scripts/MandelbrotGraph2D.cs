using UnityEngine;
using UnityEngine.InputSystem;

public class MandelbrotGraph2D : MonoBehaviour
{
    static readonly int
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        colorsId = Shader.PropertyToID("_Colors");

    [SerializeField, Resolution]
    int resolution = 1024;

    [SerializeField]
    ComputeShader computeShader2D;

    [SerializeField]
    Material pointMaterial;
    //The mesh used for each point.
    [SerializeField]
    Mesh instanceMesh;

    [SerializeField, Range(0, 1)]
    float zoomSpeed = 0.15f;

    //These 4 floats represent the current min and max values for the real and imaginary parts of the complex
    //number plane, the grid that holds the image of the fractal once plotted.
    //This default starting range is sufficient for viewing the details of the classic Mandelbrot fractal.
    //The range will be adjusted when zooming in or out on the fractal.
    readonly float defaultMinReal = -2.5f;
    readonly float defaultMaxReal = 2f;
    readonly float defaultMinImaginary = -2f;
    readonly float defaultMaxImaginary = 2f;

    [SerializeField]
    float currentMinReal;
    [SerializeField]
    float currentMaxReal;
    [SerializeField]
    float currentMinImaginary;
    [SerializeField]
    float currentMaxImaginary;

    ComputeBuffer positionsBuffer;
    ComputeBuffer colorsBuffer;
    //Step is used to determine the scale each point must be to fit within the space defined by the complex number plane, and is determined by the resolution.
    float step;
    //The world space Bounds containing the fractal.
    Bounds graphBounds;

    InputActionAsset input;

    private void OnEnable()
    {
        step = 2f / resolution;
        positionsBuffer = new ComputeBuffer(resolution * resolution, 3 * 4);
        colorsBuffer = new ComputeBuffer(resolution * resolution, 4 * 4);
        input = GetComponent<PlayerInput>().actions;
        InputAction scrollZoom = input.FindAction("ScrollZoom", true);
        scrollZoom.performed += context => HandleZoomInput(context);
        input.FindAction("SetZoomCenter", true).started += _ => SetZoomCenter();
        
    }
    private void OnDisable()
    {
        //you should always explicitly release a buffer and set it to null so that the garbage collector can collect it immediately once it is no longer needed
        positionsBuffer.Release();
        colorsBuffer.Release();
        positionsBuffer = null;
        colorsBuffer = null;
        input.Disable();
    }

    private void Start()
    {
        currentMinReal = defaultMinReal;
        currentMaxReal = defaultMaxReal;
        currentMinImaginary = defaultMinImaginary;
        currentMaxImaginary = defaultMaxImaginary;

        Vector3 graphSize = new(2f, 2f, 0.2f);
        graphBounds = new Bounds(Vector3.zero, graphSize);
    }
    private void Update()
    {
        CreateMandelbrotFractal();
    }
    /// <summary>
    /// This sets up the material and compute shader to generate the Mandelbrot fractal for the current min and max values. 
    /// </summary>
    void CreateMandelbrotFractal()
    {
        pointMaterial.SetBuffer(positionsId, positionsBuffer);
        pointMaterial.SetBuffer(colorsId, colorsBuffer);
        pointMaterial.SetFloat(stepId, step);

        int kernelHandle = computeShader2D.FindKernel("GenerateMandelbrotKernel2D");
        computeShader2D.SetInt(resolutionId, resolution);
        computeShader2D.SetFloat("_MinReal", currentMinReal);
        computeShader2D.SetFloat("_MaxReal", currentMaxReal);
        computeShader2D.SetFloat("_MinImaginary", currentMinImaginary);
        computeShader2D.SetFloat("_MaxImaginary", currentMaxImaginary);
        computeShader2D.SetBuffer(kernelHandle, positionsId, positionsBuffer);
        computeShader2D.SetBuffer(kernelHandle, colorsId, colorsBuffer);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        computeShader2D.Dispatch(kernelHandle, threadGroups, threadGroups, 1);

        Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, pointMaterial, graphBounds, positionsBuffer.count);
    }

    public void HandleZoomInput(InputAction.CallbackContext context)
    {
        Vector2 inputVec = context.ReadValue<Vector2>();

        //scroll input is only stored in the y part of the Vector2
        float zoomFactor = 1 + inputVec.y * zoomSpeed;

        ApplyZoom(zoomFactor);
    }
    public void SetZoomCenter()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        if (mouseScreenPos.x < 0 || mouseScreenPos.x > Screen.width || mouseScreenPos.y < 0 || mouseScreenPos.y > Screen.height)
        {
            return;
        }
        
        FractalSpaceCalculator.ConvertScreenPointToFractalSpace(new(mouseScreenPos.x, mouseScreenPos.y, 0), Camera.main, graphBounds, ref currentMinReal, ref currentMaxReal, ref currentMinImaginary, ref currentMaxImaginary);
        //After setting a new center, we apply the zoom to the new center. Without this step, there will be issues with zoom and centering behavior.
        ApplyZoom();
    }
    /// <summary>
    /// Applies the given zoom to the fractal's center.
    /// </summary>
    /// <param name="zoomFactor">The zoom to apply. Defaults to one, which does not apply any additional zoom, but is used to re-center things after changing the center point of the fractal.</param>
    void ApplyZoom(float zoomFactor = 1)
    {
        // Calculate new ranges based on the zoom factor
        float realRange = (currentMaxReal - currentMinReal) / zoomFactor;
        float imagRange = (currentMaxImaginary - currentMinImaginary) / zoomFactor;

        Vector2 center = new((currentMinReal + currentMaxReal) / 2f, (currentMinImaginary + currentMaxImaginary) / 2f);

        // Update min and max values to reflect the new zoom level
        currentMinReal = center.x - realRange / 2;
        currentMaxReal = center.x + realRange / 2;
        currentMinImaginary = center.y - imagRange / 2;
        currentMaxImaginary = center.y + imagRange / 2;
    }
}
