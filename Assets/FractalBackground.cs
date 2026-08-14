using UnityEngine;
using UnityEngine.UI;
using TMPro;
/// <summary>
/// Renders an animated fractal material into a low-resolution RenderTexture and displays it full-screen.
/// </summary>
[DisallowMultipleComponent]
public class FractalBackground : MonoBehaviour
{
    [SerializeField] private Material fractalMaterial;

    [Range(1f, 6f)]
    [SerializeField] private float maxUpscale = 3f;
    [SerializeField] private int pixelBudget = 300_000;
    [SerializeField] private int iterationBudget = 18_000_000;
    [SerializeField] private int minIterations = 24;
    [SerializeField] private int maxIterations = 72;
    [SerializeField] private bool halfRateAtHighFps = true;
    [Header("Output (assign one)")]
    [SerializeField] private RawImage targetRawImage;
    [SerializeField] private Renderer targetRenderer;
    
    [SerializeField] private TMP_InputField iterationsInput;
    [SerializeField] private TextMeshProUGUI currentIterText;
    [SerializeField] private Button targetFPSButton;
    [SerializeField] private TextMeshProUGUI screenSizeText;
    [SerializeField] private Button upscaleInput;

    /// <summary>The low-resolution RenderTexture the fractal is drawn into.</summary>
    public RenderTexture Output { get; private set; }
    private int _rtWidth;
    private int _rtHeight;
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UseJuliaId = Shader.PropertyToID("_UseJulia");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int IterationsId = Shader.PropertyToID("_MaxIter");
    
    private static readonly int PhaseAId = Shader.PropertyToID("_PhaseA");
    private static readonly int PhaseBId = Shader.PropertyToID("_PhaseB");
    private static readonly int AnimSpeedId = Shader.PropertyToID("_AnimSpeed");
    private static readonly int ZoomSpeedId = Shader.PropertyToID("_ZoomSpeed");
    private static readonly int PanSpeedId = Shader.PropertyToID("_PanSpeed");
    private static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int JuliaOrbitSpeedId = Shader.PropertyToID("_JuliaOrbitSpeed");
    private static readonly int ColorFlowSpeedId = Shader.PropertyToID("_ColorFlowSpeed");
    private const double TwoPi = 2.0 * System.Math.PI;
    private double _animTime;
    
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
        
        _animTime += Time.deltaTime * fractalMaterial.GetFloat(AnimSpeedId);
        
        if (halfRateAtHighFps && Application.targetFrameRate >= 100 && (Time.frameCount & 1) == 1)
        {
            return;
        }

        UpdateAnimationPhases();
        Graphics.Blit(null, Output, fractalMaterial);
    }

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
        
        int targetH = Mathf.CeilToInt(screenH / Mathf.Max(1f, maxUpscale));
        int targetW = Mathf.Max(1, Mathf.RoundToInt(targetH * aspect));
        
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
        Output = new RenderTexture(targetW, targetH, 0, RenderTextureFormat.ARGB32)
        {
            name = "FractalBackgroundRT",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            useMipMap = false,
            autoGenerateMips = false,
        };
        Output.Create();
        // Render immediately so the target isn't blank for a frame
        if (fractalMaterial != null)
        {
            Graphics.Blit(null, Output, fractalMaterial);
        }
        AssignOutput();
    }
    
    private void ApplyAutoIterations(int width, int height)
    {
        if (fractalMaterial == null)
        {
            return;
        }
        int iterations = Mathf.RoundToInt(iterationBudget / (float)((long)width * height));
        iterations = Mathf.Clamp(iterations, minIterations, maxIterations);
        fractalMaterial.SetFloat(IterationsId, iterations);
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