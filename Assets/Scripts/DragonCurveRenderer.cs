using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class DragonCurveRenderer : MonoBehaviour
{
    [Header("Generation")]
    [SerializeField] private int iterations = 16;
    [SerializeField] private float stepSize = 0.02f;

    [Header("Animation")]
    [SerializeField] private float growthSpeed = 5000f;

    [Header("Optional")]
    [SerializeField] private Transform drawingHead;

    private LineRenderer lineRenderer;

    private readonly List<Vector2> points = new();
    private Vector3[] positions;

    private int visiblePoints;
    private int lastVisiblePoints;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        GenerateDragonCurve();
        InitializeRenderer();
    }

    private void Update()
    {
        AnimateGrowth();
    }

    private void GenerateDragonCurve()
    {
        int segmentCount = 1 << iterations;

        points.Clear();
        points.Capacity = segmentCount + 1;

        Vector2 position = Vector2.zero;
        Vector2 direction = Vector2.right;

        points.Add(position);

        for (int i = 1; i <= segmentCount; i++)
        {
            position += direction * stepSize;
            points.Add(position);

            if (i < segmentCount)
            {
                direction = IsLeftTurn(i)
                    ? new Vector2(-direction.y, direction.x)   // Left turn
                    : new Vector2(direction.y, -direction.x);  // Right turn
            }
        }

        positions = new Vector3[points.Count];

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            positions[i] = new Vector3(p.x, p.y, 0f);
        }
    }

    private void InitializeRenderer()
    {
        visiblePoints = 1;
        lastVisiblePoints = 1;

        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, positions[0]);

        if (drawingHead != null)
        {
            drawingHead.position = positions[0];
        }
    }

    private void AnimateGrowth()
    {
        if (visiblePoints >= positions.Length)
            return;

        visiblePoints += Mathf.CeilToInt(growthSpeed * Time.deltaTime);
        visiblePoints = Mathf.Min(visiblePoints, positions.Length);

        if (visiblePoints == lastVisiblePoints)
            return;

        lineRenderer.positionCount = visiblePoints;

        for (int i = lastVisiblePoints; i < visiblePoints; i++)
        {
            lineRenderer.SetPosition(i, positions[i]);
        }

        lastVisiblePoints = visiblePoints;

        if (drawingHead != null)
        {
            drawingHead.position = positions[visiblePoints - 1];
        }
    }

    /// <summary>
    /// Dragon curve paperfolding sequence.
    /// True = left turn
    /// False = right turn
    /// </summary>
    private bool IsLeftTurn(int i)
    {
        return (((i & -i) << 1) & i) == 0;
    }
}