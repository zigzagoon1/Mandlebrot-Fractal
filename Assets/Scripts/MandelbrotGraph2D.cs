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

    //The custom resolution attribute ensures this value is a number divisible by 8, for efficient GPU usage
    [SerializeField, Resolution]
    int resolution = 10;

    [SerializeField]
    ComputeShader computeShader2D;

    [SerializeField]
    Material pointMaterial;

    [SerializeField]
    Mesh instanceMesh;

    //These 4 floats represent the min and max values for the real and imaginary parts of the complex number c used in the Mandelbrot iteration equation
    //This range is sufficient for viewing the details of the classic Mandelbrot fractal, but they are configurable if you want to test that
    [SerializeField, Range(-5, 5)]
    float minReal = -2.5f;

    [SerializeField, Range(-5, 5)]
    float maxReal = 1.5f;

    [SerializeField, Range(-5, 5)]
    float minImaginary = -2.0f;

    [SerializeField, Range(-5, 5)]
    float maxImaginary = 2.0f;
    //Zoom is not yet implemented
    [SerializeField, ConstrainProportions]
    Vector2 zoom = Vector2.one;

    ComputeBuffer positionsBuffer;
    ComputeBuffer colorsBuffer;
    //Step is used to determine the scale each point must be to fit within the space defined by the complex number plane, and is determined by the resolution
    float step;

    private void OnEnable()
    {
        step = 2f / resolution;
        positionsBuffer = new ComputeBuffer(resolution * resolution, 3 * 4);
        colorsBuffer = new ComputeBuffer(resolution * resolution, 4 * 4);
    }
    private void OnDisable()
    {
        //you should always explicitly release a buffer and set it to null so that the garbage collector can collect it immediately once it is no longer needed
        positionsBuffer.Release();
        colorsBuffer.Release();
        positionsBuffer = null;
        colorsBuffer = null;
    }

    private void Start()
    {
        DispatchComputeShader();
    }
    private void Update()
    {
        pointMaterial.SetBuffer(positionsId, positionsBuffer);
        pointMaterial.SetBuffer(colorsId, colorsBuffer);
        pointMaterial.SetFloat(stepId, step);
        var bounds = new Bounds(Vector3.zero, Vector3.one * (Mathf.Abs(maxReal - minReal + 2f / resolution)));
        Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, pointMaterial, bounds, positionsBuffer.count);

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
    void DispatchComputeShader()
    {
        int kernelHandle = computeShader2D.FindKernel("GenerateMandelbrotKernel2D");
        computeShader2D.SetInt(resolutionId, resolution);
        computeShader2D.SetFloat(stepId, step);
        computeShader2D.SetFloat("_MinReal", minReal);
        computeShader2D.SetFloat("_MaxReal", maxReal);
        computeShader2D.SetFloat("_MinImaginary", minImaginary);
        computeShader2D.SetFloat("_MaxImaginary", maxImaginary);
        computeShader2D.SetBuffer(kernelHandle, positionsId, positionsBuffer);
        computeShader2D.SetBuffer(kernelHandle, colorsId, colorsBuffer);

        int threadGroups = Mathf.CeilToInt(resolution / 8.0f);
        computeShader2D.Dispatch(kernelHandle, threadGroups, threadGroups, 1);
    }
}
