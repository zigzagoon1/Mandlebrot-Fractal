using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;

public class MandelbrotGraph2D : MonoBehaviour
{
    static readonly int 
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"), 
        stepId = Shader.PropertyToID("_Step"), 
        colorsId = Shader.PropertyToID("_Colors");

    [SerializeField]
    Transform pointPrefab;

    [SerializeField, Resolution]
    int resolution = 10;

    [SerializeField]
    ComputeShader computeShader;

    [SerializeField]
    Material material;

    [SerializeField]
    Mesh mesh;

    [SerializeField, Range(-5, 5)]
    float minReal = -2.5f;

    [SerializeField, Range(-5, 5)]
    float maxReal = 1.5f;

    [SerializeField, Range(-5, 5)]
    float minImaginary = -2.0f;

    [SerializeField, Range(-5, 5)]
    float maxImaginary = 2.0f;

    [SerializeField, ConstrainProportions]
    Vector2 zoom = Vector2.one;

    ComputeBuffer positionsBuffer;
    ComputeBuffer colorsBuffer;

    //RenderTexture colorTexture;

    float step;

    private void OnEnable()
    {
        step = 2f / resolution;
        positionsBuffer = new ComputeBuffer(resolution * resolution, 3 * 4);
        colorsBuffer = new ComputeBuffer(resolution * resolution, 4 * 4);
    }
    private void OnDisable()
    {
        positionsBuffer.Release();
        colorsBuffer.Release();
        positionsBuffer = null;
        colorsBuffer = null;
    }

    private void Start()
    {
        //InitializeRenderTexture();
        DispatchComputeShader();
    }
    private void Update()
    {
        material.SetBuffer(positionsId, positionsBuffer);
        material.SetBuffer(colorsId, colorsBuffer);
        material.SetFloat(stepId, step);
        var bounds = new Bounds(Vector3.zero, Vector3.one * (Mathf.Abs(maxReal - minReal + 2f / resolution)));
        Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, positionsBuffer.count);

        /*        float time = Time.time;
                for (int i = 0; i < points.Length; i++)
                {
                    Transform point = points[i];
                    Vector3 position = point.localPosition;
                    position.y = Mathf.Sin(Mathf.PI * (position.x + time));
                    position.z = Mathf.Sin(Mathf.PI * (position.y + time));
                    point.localPosition = position;
                }*/
    }
/*
    void InitializeRenderTexture()
    {
        colorTexture = new RenderTexture(resolution, resolution, 0);
        colorTexture.enableRandomWrite = true;
        colorTexture.Create();
    }*/

    void DispatchComputeShader()
    {
        int kernelHandle = computeShader.FindKernel("MandelbrotKernel2D");
        computeShader.SetInt(resolutionId, resolution);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat("_MinReal", minReal);
        computeShader.SetFloat("_MaxReal", maxReal);
        computeShader.SetFloat("_MinImaginary", minImaginary);
        computeShader.SetFloat("_MaxImaginary", maxImaginary);
        computeShader.SetBuffer(kernelHandle, positionsId, positionsBuffer);
        computeShader.SetBuffer(kernelHandle, colorsId, colorsBuffer);
        //computeShader.SetTexture(kernelHandle, "_ColorTexture", colorTexture);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        computeShader.Dispatch(kernelHandle, threadGroups, threadGroups, 1);
    }

/*    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Graphics.Blit(colorTexture, destination);
    }*/

    /*void GenerateMandelbrot()
    {

        float step = 2f / resolution;
        Vector3 position = Vector3.zero;
        var scale = Vector3.one * step;

        for (int i = 0; i < points.Length; i++)
        {
            for (int j = 0; j < points.Length; j++)
            {

                Transform pointInstance = points[i] = Instantiate(pointPrefab);
                //normalize x and y to be between -1 and 1
                position.x = (i + 0.5f) * step - 1f;
                position.y = (j + 0.5f) * step - 1f;
                pointInstance.localPosition = position;
                pointInstance.localScale = scale;
                pointInstance.SetParent(transform, false);


                //determine if the position on the complex plane is included in the Mandelbrot set
                //TODO: switch to shader
*//*                Renderer renderer = pointInstance.GetComponent<Renderer>();
                if (IsInMandelbrotSet(x0, y0))
                {
                    renderer.material.color = Color.black;
                }
                else
                {
                    renderer.material.color = Color.white;
                }*//*
            }
           
        }
    }

   *//* bool IsInMandelbrotSet(float x, float y)
    {
        //Mandelbrot equation: z(n + 1) = z(n)^2 + c
        //c = x + yi

        //i^2 = -1
        //(zyi)^2 = -zy^2 (so when squaring a complex number, in this case (zx + zyi)^2, there will be a subtraction of zy^2

        float zx = 0.0f;
        float zy = 0.0f;
        int i = 0;
        int maxI = 1000;

        while (zx * zx + zy * zy < 4.0f && i  < maxI)
        {
            //calculate new zx value in a temporary variable in order to not update zx before using it to calculate new zy
            float xTemp = zx * zx - zy * zy + x;
            //calculate new zy
            zy = 2.0f * zx * zy + y;
            //update zx
            zx = xTemp;

            i++;
        }

        return i == maxI;
    }

    public void Regenerate()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
            points = new Transform[resolution * resolution];
        }
        GenerateMandelbrot();
    }*/
}
