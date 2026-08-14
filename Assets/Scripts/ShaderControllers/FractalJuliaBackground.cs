using TMPro;
using UnityEngine;
using UnityEngine.UI;
/// <summary>
/// Renders an animated fractal material into a low-resolution RenderTexture and displays it full-screen.
/// </summary>
[DisallowMultipleComponent]
public class FractalJuliaBackground : MonoBehaviour
{
    [SerializeField] private Material fractalMaterial;
    [Tooltip("Maximum linear upscale from render texture to screen, applied to both axes.")]
    [Range(1f, 6f)]
    [SerializeField] private float maxUpscale = 3f;
    [SerializeField] private int pixelBudget = 300_000;
    [SerializeField] private int iterationBudget = 18_000_000;
    [SerializeField] private int minIterations = 24;
    [SerializeField] private int maxIterations = 72;
    // Reduce render update rate when target fps is >=100 (animation still looks good but work doesn't increase with our 120 fps setting) 
    [SerializeField] private bool halfRateAtHighFps = true;
    [Header("Output (assign one)")]
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private Renderer targetRenderer;

    [SerializeField] private TMP_InputField iterationsInput;
    [SerializeField] private TextMeshProUGUI currentIterText;
    [SerializeField] private Button targetFPSButton;
    [SerializeField] private TextMeshProUGUI screenSizeText;
    [SerializeField] private Button upscaleInput;

    [Header("Julia Path")]
    [SerializeField] private bool animateJuliaPath = true;
    [SerializeField] private Vector2[] juliaWaypoints =
    {
        new Vector2(-0.8f, 0.156f),      // dragon-like spirals
        new Vector2(-0.7269f, 0.1889f),  // spiral filaments
        new Vector2(-0.4f, 0.6f),        // lightning swirls
        new Vector2(-0.123f, 0.745f),    // Douady rabbit
        new Vector2(0.285f, 0.01f),      // delicate swirls near the right cusp
        new Vector2(-0.123f, -0.745f),   // mirrored rabbit
        new Vector2(-0.4f, -0.6f),       // mirrored swirls
        new Vector2(-0.70176f, -0.3842f),// tentacled spirals
        new Vector2(-0.835f, -0.2321f),  // seahorse-like spirals
    };
    /// <summary>Seconds spent orbiting at each waypoint (scaled by the material's Anim Speed)</summary>
    [SerializeField] private float dwellDuration = 8f;
    [SerializeField] private float travelDuration = 5f;
    [SerializeField] private bool randomOrder = false;
    /// <summary>The low-resolution RenderTexture the fractal is drawn into.</summary>
    public RenderTexture Output { get; private set; }
    private int _rtWidth;
    private int _rtHeight;
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UseJuliaId = Shader.PropertyToID("_UseJulia");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int IterationsId = Shader.PropertyToID("_MaxIter");

    // Accumulate time in double precision on the CPU and wrap each phase into its periodic range (0..2pi, or 0..1 for the color palette). 
    private static readonly int PhaseAId = Shader.PropertyToID("_PhaseA");
    private static readonly int PhaseBId = Shader.PropertyToID("_PhaseB");
    private static readonly int AnimSpeedId = Shader.PropertyToID("_AnimSpeed");
    private static readonly int ZoomSpeedId = Shader.PropertyToID("_ZoomSpeed");
    private static readonly int PanSpeedId = Shader.PropertyToID("_PanSpeed");
    private static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int JuliaOrbitSpeedId = Shader.PropertyToID("_JuliaOrbitSpeed");
    private static readonly int ColorFlowSpeedId = Shader.PropertyToID("_ColorFlowSpeed");
    private static readonly int JuliaCId = Shader.PropertyToID("_JuliaC");
    private static readonly int JuliaOrbitId = Shader.PropertyToID("_JuliaOrbit");
    private const double TwoPi = 2.0 * System.Math.PI;
    private double _animTime;

    // Julia path state machine
    private int _currentWp;
    private int _nextWp;
    private bool _traveling;
    private double _pathTime;

    private int gameTargetFPS = 60;
    
    private void OnEnable()
    {
        EnsureRenderTexture();
    }
    private void OnDisable()
    {
        ReleaseRenderTexture();
    }

    private void Awake()
    {
        gameTargetFPS = 60;
        Application.targetFrameRate = gameTargetFPS;
    }

    public void ChangeIterationCount()
    {
        float.TryParse(iterationsInput.text, out float iter);
        if (iter > 1f)
        {
            fractalMaterial.SetFloat(IterationsId, iter);
        }
        currentIterText.text = iter.ToString();
    }

    public void ChangeUpscale()
    {
        maxUpscale = (int)maxUpscale == 3 ? 4 : 3;
    }

    public void ChangeTargetFrameRate()
    {
        gameTargetFPS = gameTargetFPS == 60 ? 120 : 60;
        Application.targetFrameRate = gameTargetFPS;
    }
    
    public void SwitchMandelbrotJulia()
    {
        int useJulia = fractalMaterial.GetFloat(UseJuliaId) == 0 ? 1 : 0;
        fractalMaterial.SetFloat(UseJuliaId, useJulia);
        fractalMaterial.SetVector(CenterId, new Vector4(useJulia == 1 ? 0.0f : -0.4f, 0.0f, 0.0f, 0.0f));
    }
    
    private void LateUpdate()
    {
        if (fractalMaterial == null)
        {
            return;
        }
        EnsureRenderTexture();

        // Advance time even if rendering is skipped this frame
        double dt = Time.deltaTime * fractalMaterial.GetFloat(AnimSpeedId);
        _animTime += dt;
        _pathTime += dt;

        if (halfRateAtHighFps && Application.targetFrameRate >= 100 && (Time.frameCount & 1) == 1)
        {
            return;
        }

        UpdateJuliaPath();
        UpdateAnimationPhases();
        Graphics.Blit(null, Output, fractalMaterial);
    }
    
    private void UpdateJuliaPath()
    {
        if (!animateJuliaPath || juliaWaypoints == null || juliaWaypoints.Length == 0)
        {
            return;
        }

        _currentWp = Mathf.Min(_currentWp, juliaWaypoints.Length - 1);
        _nextWp = Mathf.Min(_nextWp, juliaWaypoints.Length - 1);

        Vector2 baseC;
        if (_traveling)
        {
            float t01 = Mathf.Clamp01((float)(_pathTime / Mathf.Max(0.01f, travelDuration)));
            // Smoothestep easing at both ends,
            float eased = t01 * t01 * t01 * (t01 * (t01 * 6f - 15f) + 10f);
            baseC = Vector2.Lerp(juliaWaypoints[_currentWp], juliaWaypoints[_nextWp], eased);
            if (t01 >= 1f)
            {
                _currentWp = _nextWp;
                _traveling = false;
                _pathTime = 0.0;
            }
        }
        else
        {
            baseC = juliaWaypoints[_currentWp];
            if (_pathTime >= dwellDuration && juliaWaypoints.Length > 1)
            {
                _nextWp = PickNextWaypoint();
                _traveling = true;
                _pathTime = 0.0;
            }
        }
        
        float orbitPhase = (float)Wrap(_animTime * fractalMaterial.GetFloat(JuliaOrbitSpeedId), TwoPi);
        float orbitRadius = fractalMaterial.GetFloat(JuliaOrbitId);
        Vector2 c = baseC + new Vector2(Mathf.Cos(orbitPhase), Mathf.Sin(orbitPhase)) * orbitRadius;
        fractalMaterial.SetVector(JuliaCId, new Vector4(c.x, c.y, 0f, 0f));
    }

    private int PickNextWaypoint()
    {
        if (!randomOrder)
        {
            return (_currentWp + 1) % juliaWaypoints.Length;
        }

        int pick = Random.Range(0, juliaWaypoints.Length - 1);
        if (pick >= _currentWp)
        {
            pick++;
        }
        return pick;
    }
    // Keep values small, maintain precision. Using _Time in shader was causing choppy movement eventually
    private void UpdateAnimationPhases()
    {
        var phaseA = new Vector4(
            (float)Wrap(_animTime * fractalMaterial.GetFloat(ZoomSpeedId), TwoPi),
            (float)Wrap(_animTime * fractalMaterial.GetFloat(PanSpeedId), TwoPi),
            (float)Wrap(_animTime * fractalMaterial.GetFloat(PanSpeedId) * 0.7, TwoPi),
            (float)Wrap(_animTime * fractalMaterial.GetFloat(SwirlSpeedId), TwoPi));
        var phaseB = new Vector4(
            (float)Wrap(_animTime * fractalMaterial.GetFloat(JuliaOrbitSpeedId), TwoPi),
            // The cosine palette repeats every 1.0.
            (float)Wrap(_animTime * fractalMaterial.GetFloat(ColorFlowSpeedId), 1.0),
            0f, 0f);

        fractalMaterial.SetVector(PhaseAId, phaseA);
        fractalMaterial.SetVector(PhaseBId, phaseB);
    }

    private static double Wrap(double value, double period)
    {
        return value - System.Math.Floor(value / period) * period;
    }
    private void EnsureRenderTexture()
    {
        int screenW = Mathf.Max(1, Screen.width);
        int screenH = Mathf.Max(1, Screen.height);
        float aspect = (float)screenW / screenH;

        // Size so neither axis is upscaled by more than maxUpscale
        int targetH = Mathf.CeilToInt(screenH / Mathf.Max(1f, maxUpscale));
        int targetW = Mathf.Max(1, Mathf.RoundToInt(targetH * aspect));

        // Pixel budget: shrink uniformly for very high-res screens 
        long pixels = (long)targetW * targetH;
        if (pixelBudget > 0 && pixels > pixelBudget)
        {
            float shrink = Mathf.Sqrt(pixelBudget / (float)pixels);
            targetH = Mathf.Max(1, Mathf.FloorToInt(targetH * shrink));
            targetW = Mathf.Max(1, Mathf.RoundToInt(targetH * aspect));
        }
        targetH = Mathf.Min(targetH, screenH);
        targetW = Mathf.Min(targetW, screenW);

        if (Output != null && _rtWidth == targetW && _rtHeight == targetH)
        {
            return;
        }
        ReleaseRenderTexture();
        _rtWidth = targetW;
        _rtHeight = targetH;
        ApplyAutoIterations(targetW, targetH);
        if (screenSizeText != null)
        {
            screenSizeText.text = $"SW{Screen.width} x SH{Screen.height}\nW{targetW} x H{targetH}";
        }
        Output = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32)
        {
            name = "FractalBackgroundRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
        };
        Output.Create();

        if (fractalMaterial != null)
        {
            Graphics.Blit(null, Output, fractalMaterial);
        }
        AssignOutput();
    }
    // Fractal cost is pixels * iterations, so find a good iteration count for given render size
    private void ApplyAutoIterations(int width, int height)
    {
        if (fractalMaterial == null)
        {
            return;
        }
        int iterations = Mathf.RoundToInt(iterationBudget / (float)((long)width * height));
        iterations = Mathf.Clamp(iterations, minIterations, maxIterations);
        fractalMaterial.SetFloat(IterationsId, iterations);
        if (currentIterText != null)
        {
            currentIterText.text = iterations.ToString();
        }
    }

    private void AssignOutput()
    {
        if (targetRawImage != null)
        {
            targetRawImage.texture = Output;
        }
        if (targetRenderer != null)
        {
            var block = new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(block);
            block.SetTexture(MainTexId, Output);
            targetRenderer.SetPropertyBlock(block);
        }
    }
    private void ReleaseRenderTexture()
    {
        if (Output != null)
        {
            Output.Release();
            Destroy(Output);
            Output = null;
        }
    }
}