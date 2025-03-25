using UnityEngine;
using UnityEngine.InputSystem;
using PeterO.Numbers;

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


    EFloat currentMinReal;
    EFloat currentMaxReal;
    EFloat currentMinImaginary;
    EFloat currentMaxImaginary;

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
        //input.FindAction("SetZoomCenter", true).started += _ => SetZoomCenter();
        
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
        currentMinReal = EFloat.FromString(defaultMinReal.ToString());
        Debug.Log($"Current min real: {currentMinReal}"); 
        currentMaxReal = EFloat.FromString(defaultMaxReal.ToString());
        Debug.Log($"Current max real: {currentMaxReal}");

        currentMinImaginary = EFloat.FromString(defaultMinImaginary.ToString());
        Debug.Log($"Current min imaginary: {currentMinImaginary}");

        currentMaxImaginary = EFloat.FromString(defaultMaxImaginary.ToString());
        Debug.Log($"Current max imaginary: {currentMaxImaginary}");


        Vector3 graphSize = new(2f, 2f, 0.2f);
        graphBounds = new Bounds(Vector3.zero, graphSize);
/*        EFloat zoomTest = EFloat.FromString("1e100");
        ApplyZoom((float)zoomTest);*/
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
        int kernelHandle = computeShader2D.FindKernel("GenerateMandelbrotKernel2D");
        /*
                EFloat rangeRe = currentMaxReal.Subtract(currentMinReal);
                EFloat rangeIm = currentMaxImaginary.Subtract(currentMinImaginary);
                EFloat stepReE = rangeRe.Divide(resolution);
                EFloat stepImE = stepReE.Divide(resolution);

                //Convert to float for GPU
                Vector2 baseReAsFloat2 = EFloatToHiLo(currentMinReal.Add(stepReE.Divide(2)));
                Vector2 baseImAsFloat2 = EFloatToHiLo(currentMinImaginary.Add(stepImE.Divide(2)));

                Vector2 stepReAsFloat2 = EFloatToHiLo(stepReE);
                Vector2 stepImAsFloat2 = EFloatToHiLo(stepImE);*/

        EFloat rangeRe = currentMaxReal.Subtract(currentMinReal);
        EFloat rangeIm = currentMaxImaginary.Subtract(currentMinImaginary);
        EFloat stepReE = rangeRe.Divide(resolution);
        EFloat stepImE = rangeIm.Divide(resolution);

        // Convert AFTER math is done
        float stepRe = ClampEFloat(stepReE);
        float stepIm = ClampEFloat(stepImE);

        Vector2 baseReAsFloat2 = EFloatToFloat2(currentMinReal.Add(stepReE.Divide(2)));
        Vector2 baseImAsFloat2 = EFloatToFloat2(currentMinImaginary.Add(stepImE.Divide(2)));

        Vector2 stepReAsFloat2 = EFloatToFloat2(stepReE);
        Vector2 stepImAsFloat2 = EFloatToFloat2(stepImE);

        Debug.Log("StepRe: " + stepRe);
        Debug.Log("StepIm: " + stepIm);




        computeShader2D.SetInt(resolutionId, resolution);
        /*        computeShader2D.SetFloat("_MinReal", currentMinReal);
                computeShader2D.SetFloat("_MaxReal", currentMaxReal);
                computeShader2D.SetFloat("_MinImaginary", currentMinImaginary);
                computeShader2D.SetFloat("_MaxImaginary", currentMaxImaginary);*/
        computeShader2D.SetVector("_BaseRe", baseReAsFloat2);
        computeShader2D.SetVector("_BaseIm", baseImAsFloat2);
        computeShader2D.SetVector("_StepRe", stepReAsFloat2);
        computeShader2D.SetVector("_StepIm", stepImAsFloat2);
/*        computeShader2D.SetFloat("_BaseRe", ConvertEFloatToSafeFloat(currentMinReal.Add(stepReE.Divide(2))));
        computeShader2D.SetFloat("_BaseIm", ConvertEFloatToSafeFloat(currentMinImaginary.Add(stepImE.Divide(2))));
        computeShader2D.SetFloat("_StepRe", stepRe);
        computeShader2D.SetFloat("_StepIm", stepIm);*/

        computeShader2D.SetBuffer(kernelHandle, positionsId, positionsBuffer);
        computeShader2D.SetBuffer(kernelHandle, colorsId, colorsBuffer);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
/*        Debug.Log($"[Zoom] stepRe: {stepReAsFloat2}, stepIm: {stepImAsFloat2}");
        Debug.Log($"[Zoom] baseRe: {baseReAsFloat2}, baseIm: {baseImAsFloat2}");*/
        computeShader2D.Dispatch(kernelHandle, threadGroups, threadGroups, 1);

        pointMaterial.SetBuffer(positionsId, positionsBuffer);
        pointMaterial.SetBuffer(colorsId, colorsBuffer);
        pointMaterial.SetFloat(stepId, step);

        Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, pointMaterial, graphBounds, positionsBuffer.count);

    }

    private float ClampEFloat(EFloat value)
    {
        if (value.IsNaN() || value.IsInfinity())
            return 0f;

        double val = value.ToDouble();
        if (double.IsNaN(val) || double.IsInfinity(val))
            return 0f;

        if (val > float.MaxValue) return float.MaxValue;
        if (val < -float.MaxValue) return -float.MaxValue;

        return (float)val;
    }

    private Vector2 EFloatToFloat2(EFloat value)
    {
        Debug.Log(value.ToString());
        double d = value.ToDouble();
        float hi = (float)d;
        float lo = (float)(d - (double)hi);
        return new Vector2(hi, lo);
    }

    public void HandleZoomInput(InputAction.CallbackContext context)
    {
        Vector2 inputVec = context.ReadValue<Vector2>();

        //scroll input is only stored in the y part of the Vector2
        float zoomFactor = 1 + inputVec.y * zoomSpeed;

        ApplyZoom(zoomFactor);
    }
/*    public void SetZoomCenter()
    {
        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();

        if (mouseScreenPos.x < 0 || mouseScreenPos.x > Screen.width || mouseScreenPos.y < 0 || mouseScreenPos.y > Screen.height)
        {
            return;
        }
        
        FractalSpaceCalculator.ConvertScreenPointToFractalSpace(new(mouseScreenPos.x, mouseScreenPos.y, 0), Camera.main, graphBounds, ref currentMinReal, ref currentMaxReal, ref currentMinImaginary, ref currentMaxImaginary);
        //After setting a new center, we apply the zoom to the new center. Without this step, there will be issues with zoom and centering behavior.
        ApplyZoom();
    }*/
    /// <summary>
    /// Applies the given zoom to the fractal's center.
    /// </summary>
    /// <param name="zoomFactor">The zoom to apply. Defaults to one, which does not apply any additional zoom, but is used to re-center things after changing the center point of the fractal.</param>
    void ApplyZoom(float zoomFactor = 1)
    {
        EFloat realRange = currentMaxReal.Subtract(currentMinReal);
        EFloat imRange = currentMaxImaginary.Subtract(currentMinImaginary);
        EFloat zoom = EFloat.FromDouble(zoomFactor);

        realRange = realRange.Divide(zoom);
        imRange = imRange.Divide(zoom);

        EFloat centerRe = currentMinReal.Add(currentMaxReal).Divide(2);
        EFloat centerIm = currentMinImaginary.Add(currentMaxImaginary).Divide(2);

        currentMinReal = centerRe.Subtract(realRange.Divide(2));
        currentMaxReal = centerRe.Add(realRange.Divide(2));

        currentMinImaginary = centerIm.Subtract(imRange.Divide(2));
        currentMaxImaginary = centerIm.Add(imRange.Divide(2));
/*        // Calculate new ranges based on the zoom factor
        float realRange = (currentMaxReal - currentMinReal) / zoomFactor;
        float imagRange = (currentMaxImaginary - currentMinImaginary) / zoomFactor;

        Vector2 center = new((currentMinReal + currentMaxReal) / 2f, (currentMinImaginary + currentMaxImaginary) / 2f);

        // Update min and max values to reflect the new zoom level
        currentMinReal = center.x - realRange / 2;
        currentMaxReal = center.x + realRange / 2;
        currentMinImaginary = center.y - imagRange / 2;
        currentMaxImaginary = center.y + imagRange / 2;*/
    }
}
