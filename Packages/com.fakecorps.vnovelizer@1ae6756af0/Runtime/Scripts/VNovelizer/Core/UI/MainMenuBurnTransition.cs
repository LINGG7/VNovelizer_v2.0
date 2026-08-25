using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Self-contained main-menu burn transition. A camera-facing dynamic ribbon supplies
/// the continuous flame front; ordinary ParticleSystems are reserved for sparks and smoke.
/// </summary>
[DisallowMultipleComponent]
public sealed class MainMenuBurnTransition : MonoBehaviour
{
    private const int RibbonSegments = 192;
    private const int RibbonRows = 5;
    private const string FlameRibbonTexturePath = "VNovelizerRes/VFX/MainMenuBurn/T_MainMenuBurnFlameRibbon";
    private const string SmokeTexturePath = "VNovelizerRes/VFX/MainMenuBurn/T_MainMenuBurnSmoke";
    private const string CharEdgeTexturePath = "VNovelizerRes/VFX/MainMenuBurn/T_MainMenuBurnCharEdge";
    private const string ParticleShaderPath = "VNovelizerRes/Materials/S_MainMenuBurnParticle";
    private const string FlameRibbonShaderPath = "VNovelizerRes/Materials/S_MainMenuBurnFlameRibbon";
    private const string MaskShaderPath = "VNovelizerRes/Materials/S_UI_MainMenuBurnMask";
    private const string ParticleShaderName = "VNovelizer/MainMenuBurn/Particle";
    private const string FlameRibbonShaderName = "VNovelizer/MainMenuBurn/FlameRibbon";
    private const string MaskShaderName = "VNovelizer/UI/MainMenuBurnMask";

    private static readonly int ProgressId = Shader.PropertyToID("_Progress");
    private static readonly int CenterId = Shader.PropertyToID("_Center");
    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
    private static readonly int RadiusScaleId = Shader.PropertyToID("_RadiusScale");
    private static readonly int ImpactId = Shader.PropertyToID("_Impact");
    private static readonly int FinalCoverId = Shader.PropertyToID("_FinalCover");
    private static readonly int SequenceTimeId = Shader.PropertyToID("_SequenceTime");
    private static readonly int CharEdgeTextureId = Shader.PropertyToID("_CharEdgeTex");
    private static readonly int ParticleFadeId = Shader.PropertyToID("_GlobalFade");

    [SerializeField, Min(2f)] private float duration = 4f;
    [SerializeField, Range(0.25f, 2f)] private float density = 1f;
    [SerializeField, Range(0.75f, 1.5f)] private float maxRadius = 1.12f;
    [SerializeField] private Vector2 burnCenter = new Vector2(0.5f, 0.5f);
    [SerializeField, Range(0f, 0.2f)] private float noiseStrength = 0.075f;
    [SerializeField, Range(0.08f, 0.45f)] private float flameHeight = 0.29f;
    [SerializeField, Range(0f, 0.35f)] private float flameCurlStrength = 0.16f;
    [SerializeField, Range(0.5f, 1.5f)] private float tongueActivity = 1f;
    [SerializeField, Range(0.5f, 2f)] private float tongueSpeed = 1.2f;
    [SerializeField, Min(1.3f)] private float spreadFinishTime = 3f;
    [SerializeField, Range(0.5f, 3f)] private float emberAmountMultiplier = 1.8f;

    private RectTransform host;
    private Camera renderCamera;
    private GameObject particleRoot;
    private Canvas charOverlayCanvas;
    private Image charOverlay;
    private Material charMaterial;
    private Material fallingFlameMaterial;
    private Material sparkMaterial;
    private Material smokeMaterial;
    private RibbonLayerVisual outerRibbon;
    private RibbonLayerVisual bodyRibbon;
    private RibbonLayerVisual coreRibbon;
    private readonly Vector3[] ribbonBasePoints = new Vector3[RibbonSegments + 1];
    private readonly float[] ribbonArcLengths = new float[RibbonSegments + 1];
    private readonly float[] ribbonTonguePulses = new float[RibbonSegments + 1];
    private readonly float[] ribbonTongueLeans = new float[RibbonSegments + 1];

    private ParticleSystem fallingSpark;
    private ParticleSystem impactSparks;
    private ParticleSystem smoke;
    private ParticleSystem embers;

    private float visibleWidth;
    private float visibleHeight;
    private float particleDepth;
    private float smokeAccumulator;
    private float emberAccumulator;
    private int emissionIndex;
    private bool impactEmitted;
    private bool isPlaying;

    public void Configure(float newDuration, float newDensity, float newMaxRadius, Vector2 center,
        float newNoiseStrength, float newFlameHeight, float newFlameCurlStrength,
        float newSpreadFinishTime, float newEmberAmountMultiplier,
        float newTongueActivity = 1f, float newTongueSpeed = 1.2f)
    {
        duration = Mathf.Max(2f, newDuration);
        density = Mathf.Clamp(newDensity, 0.25f, 2f);
        maxRadius = Mathf.Clamp(newMaxRadius, 0.75f, 1.5f);
        burnCenter = new Vector2(Mathf.Clamp01(center.x), Mathf.Clamp01(center.y));
        noiseStrength = Mathf.Clamp(newNoiseStrength, 0f, 0.2f);
        flameHeight = Mathf.Clamp(newFlameHeight, 0.08f, 0.45f);
        flameCurlStrength = Mathf.Clamp(newFlameCurlStrength, 0f, 0.35f);
        tongueActivity = Mathf.Clamp(newTongueActivity, 0.5f, 1.5f);
        tongueSpeed = Mathf.Clamp(newTongueSpeed, 0.5f, 2f);
        spreadFinishTime = Mathf.Clamp(newSpreadFinishTime, 1.3f, Mathf.Max(1.3f, duration - 0.6f));
        emberAmountMultiplier = Mathf.Clamp(newEmberAmountMultiplier, 0.5f, 5f);
    }

    public IEnumerator Play(RectTransform transitionHost)
    {
        if (isPlaying)
            yield break;

        host = transitionHost != null ? transitionHost : transform as RectTransform;
        EnsureVisuals();
        ResetTransition();
        isPlaying = true;

        if (charOverlay != null)
        {
            RefreshOverlaySorting();
            RefreshEffectSorting();
            GameObject overlayRoot = charOverlayCanvas != null
                ? charOverlayCanvas.gameObject
                : charOverlay.gameObject;
            overlayRoot.SetActive(true);
            overlayRoot.transform.SetAsLastSibling();
        }

        if (particleRoot != null)
            particleRoot.SetActive(true);

        PlaySystems();
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float timeline = Mathf.Clamp01(elapsed / duration);
            UpdateCameraSpace();
            UpdateMask(timeline);
            UpdateRibbons(timeline);
            UpdateParticles(timeline, Time.unscaledDeltaTime);
            yield return null;
        }

        UpdateMask(1f);
        UpdateRibbons(1f);
        StopAndClearSystems();
        if (particleRoot != null)
            particleRoot.SetActive(false);
        isPlaying = false;
    }

    public void ResetTransition()
    {
        StopAndClearSystems();
        impactEmitted = false;
        emissionIndex = 0;
        smokeAccumulator = 0f;
        emberAccumulator = 0f;
        isPlaying = false;
        SetParticleFade(1f);
        ResetRibbons();

        if (charMaterial != null)
        {
            charMaterial.SetFloat(ProgressId, 0f);
            charMaterial.SetFloat(ImpactId, 0f);
            charMaterial.SetFloat(FinalCoverId, 0f);
            charMaterial.SetFloat(SequenceTimeId, 0f);
        }

        if (charOverlay != null)
        {
            charOverlay.color = Color.white;
            GameObject overlayRoot = charOverlayCanvas != null
                ? charOverlayCanvas.gameObject
                : charOverlay.gameObject;
            overlayRoot.SetActive(false);
        }

        if (particleRoot != null)
            particleRoot.SetActive(false);
    }

    private void EnsureVisuals()
    {
        EnsureOverlay();
        if (particleRoot != null)
            return;

        renderCamera = FindRenderCamera();
        particleRoot = new GameObject("MainMenuBurn3DParticles");
        particleRoot.hideFlags = HideFlags.DontSave;
        UpdateCameraSpace();

        Shader particleShader = Resources.Load<Shader>(ParticleShaderPath);
        if (particleShader == null)
            particleShader = Shader.Find(ParticleShaderName);
        Texture2D smokeTexture = Resources.Load<Texture2D>(SmokeTexturePath);
        if (particleShader == null || smokeTexture == null)
        {
            Debug.LogWarning("[MainMenuBurnTransition] Particle shader or generated textures are missing. The char transition will still play.");
            return;
        }

        sparkMaterial = CreateParticleMaterial(particleShader, Texture2D.whiteTexture,
            new Color(1f, 1f, 0.9f, 1f), new Color(1f, 0.18f, 0.005f, 1f), 2.2f, true);
        sparkMaterial.SetFloat("_ProceduralShape", 1f);
        fallingFlameMaterial = CreateParticleMaterial(particleShader, Texture2D.whiteTexture,
            new Color(1f, 1f, 0.82f, 1f), new Color(1f, 0.055f, 0.001f, 1f), 2.65f, true);
        fallingFlameMaterial.name = "M_MainMenuBurnFallingFlame_Runtime";
        fallingFlameMaterial.SetFloat("_ProceduralShape", 2f);
        fallingFlameMaterial.SetFloat("_TextureColorWeight", 0f);
        fallingFlameMaterial.SetFloat("_Cutoff", 0.04f);
        fallingFlameMaterial.SetFloat("_Softness", 0.1f);
        smokeMaterial = CreateParticleMaterial(particleShader, smokeTexture,
            new Color(0.36f, 0.34f, 0.32f, 1f), new Color(0.035f, 0.03f, 0.028f, 1f), 0.7f, false);

        EnsureRibbonVisuals();

        fallingSpark = CreateSystem("FallingSpark", fallingFlameMaterial, 64, 30);
        impactSparks = CreateSystem("ImpactSparks", sparkMaterial, 100, 29);
        smoke = CreateSystem("Smoke", smokeMaterial, 180, 20);
        embers = CreateSystem("Embers", sparkMaterial, 200, 31);

        ConfigureFallingFlameSystem(fallingSpark);
        ConfigureSparkSystem(impactSparks, false);
        ConfigureSmokeSystem(smoke);
        ConfigureEmberSystem(embers);
        RefreshEffectSorting();
        UpdateCameraSpace();
    }

    private void EnsureOverlay()
    {
        if (charOverlay != null && charOverlayCanvas != null)
        {
            RefreshOverlaySorting();
            return;
        }

        Transform existingCanvas = transform.Find("MainMenuBurnOverlayCanvas");
        GameObject canvasObject = existingCanvas != null
            ? existingCanvas.gameObject
            : new GameObject("MainMenuBurnOverlayCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.layer = gameObject.layer;
        canvasObject.transform.SetParent(transform, false);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.localScale = Vector3.one;

        charOverlayCanvas = canvasObject.GetComponent<Canvas>();
        if (charOverlayCanvas == null)
            charOverlayCanvas = canvasObject.AddComponent<Canvas>();
        charOverlayCanvas.overrideSorting = true;
        RefreshOverlaySorting();

        Transform existing = existingCanvas != null
            ? existingCanvas.Find("MainMenuBurnCharOverlay")
            : null;
        if (existing == null)
            existing = transform.Find("MainMenuBurnCharOverlay");
        GameObject overlayObject = existing != null
            ? existing.gameObject
            : new GameObject("MainMenuBurnCharOverlay", typeof(RectTransform));
        overlayObject.layer = gameObject.layer;
        overlayObject.transform.SetParent(canvasObject.transform, false);

        RectTransform rect = overlayObject.GetComponent<RectTransform>();
        if (rect == null)
            rect = overlayObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        charOverlay = overlayObject.GetComponent<Image>();
        if (charOverlay == null)
            charOverlay = overlayObject.AddComponent<Image>();
        charOverlay.raycastTarget = false;

        Shader shader = Resources.Load<Shader>(MaskShaderPath);
        if (shader == null)
            shader = Shader.Find(MaskShaderName);
        if (shader != null)
        {
            charMaterial = new Material(shader) { name = "M_MainMenuBurnChar_Runtime" };
            Texture2D charEdgeTexture = Resources.Load<Texture2D>(CharEdgeTexturePath);
            if (charEdgeTexture != null)
                charMaterial.SetTexture(CharEdgeTextureId, charEdgeTexture);
            charOverlay.material = charMaterial;
        }
        else
        {
            charOverlay.material = null;
            Debug.LogWarning("[MainMenuBurnTransition] Char mask shader is missing; using a plain black fade.");
        }
        canvasObject.SetActive(false);
    }

    private void RefreshOverlaySorting()
    {
        if (charOverlayCanvas == null)
            return;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        Canvas buttonCanvas = null;
        Transform buttonCanvasTransform = transform.Find("ButtonCanvas");
        if (buttonCanvasTransform != null)
            buttonCanvas = buttonCanvasTransform.GetComponent<Canvas>();

        int sortingLayerId = buttonCanvas != null
            ? buttonCanvas.sortingLayerID
            : parentCanvas != null ? parentCanvas.sortingLayerID : 0;
        int highestSortingOrder = buttonCanvas != null
            ? buttonCanvas.sortingOrder
            : parentCanvas != null ? parentCanvas.sortingOrder : 0;

        Canvas[] childCanvases = GetComponentsInChildren<Canvas>(true);
        foreach (Canvas childCanvas in childCanvases)
        {
            if (childCanvas == null || childCanvas == charOverlayCanvas)
                continue;
            if (childCanvas.overrideSorting && childCanvas.sortingLayerID == sortingLayerId)
                highestSortingOrder = Mathf.Max(highestSortingOrder, childCanvas.sortingOrder);
        }

        charOverlayCanvas.overrideSorting = true;
        charOverlayCanvas.sortingLayerID = sortingLayerId;
        charOverlayCanvas.sortingOrder = highestSortingOrder + 1;
    }

    private void RefreshEffectSorting()
    {
        int sortingLayerId = charOverlayCanvas != null ? charOverlayCanvas.sortingLayerID : 0;
        int maskOrder = charOverlayCanvas != null ? charOverlayCanvas.sortingOrder : 0;

        SetRendererSorting(smoke, sortingLayerId, maskOrder + 1);
        SetRibbonSorting(outerRibbon, sortingLayerId, maskOrder + 2);
        SetRibbonSorting(bodyRibbon, sortingLayerId, maskOrder + 4);
        SetRibbonSorting(coreRibbon, sortingLayerId, maskOrder + 6);
        SetRendererSorting(fallingSpark, sortingLayerId, maskOrder + 7);
        SetRendererSorting(impactSparks, sortingLayerId, maskOrder + 8);
        SetRendererSorting(embers, sortingLayerId, maskOrder + 9);
    }

    private static void SetRibbonSorting(RibbonLayerVisual layer, int sortingLayerId, int sortingOrder)
    {
        if (layer == null)
            return;
        layer.Renderer.sortingLayerID = sortingLayerId;
        layer.Renderer.sortingOrder = sortingOrder;
    }

    private static void SetRendererSorting(ParticleSystem system, int sortingLayerId, int sortingOrder)
    {
        if (system == null)
            return;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
    }

    private Camera FindRenderCamera()
    {
        Canvas canvas = host != null ? host.GetComponentInParent<Canvas>() : null;
        if (canvas != null && canvas.worldCamera != null)
            return canvas.worldCamera;
        if (Camera.main != null)
            return Camera.main;
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<Camera>();
#else
        return FindObjectOfType<Camera>();
#endif
    }

    private void UpdateCameraSpace()
    {
        if (particleRoot == null)
            return;
        if (renderCamera == null)
            renderCamera = FindRenderCamera();
        if (renderCamera == null)
            return;

        float canvasDepth = Vector3.Dot(transform.position - renderCamera.transform.position, renderCamera.transform.forward);
        particleDepth = Mathf.Clamp(canvasDepth > renderCamera.nearClipPlane ? canvasDepth - 0.12f : 4f,
            renderCamera.nearClipPlane + 0.2f, renderCamera.farClipPlane - 0.5f);

        visibleHeight = renderCamera.orthographic
            ? renderCamera.orthographicSize * 2f
            : 2f * particleDepth * Mathf.Tan(renderCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        visibleWidth = visibleHeight * Mathf.Max(0.1f, renderCamera.aspect);

        Vector3 viewportCenter = new Vector3(burnCenter.x, burnCenter.y, particleDepth);
        particleRoot.transform.SetPositionAndRotation(
            renderCamera.ViewportToWorldPoint(viewportCenter),
            renderCamera.transform.rotation);
    }

    private void UpdateMask(float timeline)
    {
        float spread = EvaluateSpread(timeline);
        float impact = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ToTimeline(0.8f), ToTimeline(0.9f), timeline)) *
                       (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ToTimeline(1f), ToTimeline(1.2f), timeline)));
        float finalCover = Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(ToTimeline(FadeStartTime), 1f, timeline));
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.77778f;

        if (charMaterial != null)
        {
            charOverlay.color = Color.white;
            charMaterial.SetFloat(ProgressId, spread);
            charMaterial.SetVector(CenterId, burnCenter);
            charMaterial.SetFloat(AspectId, aspect);
            charMaterial.SetFloat(NoiseStrengthId, noiseStrength);
            charMaterial.SetFloat(RadiusScaleId, maxRadius);
            charMaterial.SetFloat(ImpactId, impact);
            charMaterial.SetFloat(FinalCoverId, finalCover);
            charMaterial.SetFloat(SequenceTimeId, timeline);
        }
        else if (charOverlay != null)
        {
            charOverlay.color = new Color(0f, 0f, 0f, Mathf.SmoothStep(0f, 1f, timeline));
        }
    }

    private float EvaluateSpread(float timeline)
    {
        float sparkEnd = ToTimeline(0.8f);
        float ignitionEnd = ToTimeline(1.2f);
        float spreadEnd = ToTimeline(SpreadFinishTime);
        float cornerEnd = ToTimeline(FadeStartTime);

        if (timeline <= sparkEnd)
            return 0f;
        if (timeline < ignitionEnd)
            return Mathf.Lerp(0f, 0.045f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sparkEnd, ignitionEnd, timeline)));
        if (timeline < spreadEnd)
            return Mathf.Lerp(0.045f, 0.94f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ignitionEnd, spreadEnd, timeline)));
        if (timeline < cornerEnd)
            return Mathf.Lerp(0.94f, 1f,
                Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(spreadEnd, cornerEnd, timeline)));
        return 1f;
    }

    private float SpreadFinishTime => Mathf.Clamp(spreadFinishTime, 1.3f, Mathf.Max(1.3f, FadeStartTime - 0.1f));
    private float FadeStartTime => Mathf.Max(1.3f, duration - 0.5f);
    private float ToTimeline(float seconds) => Mathf.Clamp01(seconds / Mathf.Max(duration, 0.001f));

    private void EnsureRibbonVisuals()
    {
        Shader ribbonShader = Resources.Load<Shader>(FlameRibbonShaderPath);
        if (ribbonShader == null)
            ribbonShader = Shader.Find(FlameRibbonShaderName);
        Texture2D ribbonTexture = Resources.Load<Texture2D>(FlameRibbonTexturePath);
        if (ribbonShader == null || ribbonTexture == null)
        {
            Debug.LogWarning("[MainMenuBurnTransition] Continuous flame ribbon shader or texture is missing.");
            return;
        }

        outerRibbon = CreateRibbonLayer("OuterFlameRibbon", ribbonShader, ribbonTexture, 22,
            new Color(0.72f, 0.055f, 0.002f, 1f), new Color(0.16f, 0.003f, 0.001f, 1f),
            0.92f, 1.08f, 1.34f, 1.12f, 0.35f, visibleHeight * 0.032f);
        bodyRibbon = CreateRibbonLayer("BodyFlameRibbon", ribbonShader, ribbonTexture, 24,
            new Color(1f, 0.72f, 0.08f, 1f), new Color(0.95f, 0.045f, 0.001f, 1f),
            1.22f, 1f, 1f, 1f, 1.7f, 0f);
        coreRibbon = CreateRibbonLayer("CoreFlameRibbon", ribbonShader, ribbonTexture, 26,
            new Color(1f, 1f, 0.82f, 1f), new Color(1f, 0.28f, 0.008f, 1f),
            1.55f, 0.64f, 0.52f, 0.72f, 3.1f, -visibleHeight * 0.025f);
    }

    private RibbonLayerVisual CreateRibbonLayer(string objectName, Shader shader, Texture texture, int sortingOrder,
        Color coreColor, Color edgeColor, float intensity, float heightScale, float widthScale,
        float curlScale, float phase, float zOffset)
    {
        Material material = new Material(shader) { name = $"M_{objectName}_Runtime" };
        material.SetTexture("_MainTex", texture);
        material.SetColor("_CoreColor", coreColor);
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_GlobalFade", 0f);
        material.SetFloat("_LayerPhase", phase);
        material.SetFloat("_TileCount", 4f);
        material.SetFloat("_TongueActivity", tongueActivity);

        GameObject layerObject = new GameObject(objectName);
        layerObject.transform.SetParent(particleRoot.transform, false);
        layerObject.transform.localPosition = new Vector3(0f, 0f, zOffset);
        MeshFilter filter = layerObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = layerObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        Mesh mesh = new Mesh { name = $"{objectName}_RuntimeMesh" };
        mesh.MarkDynamic();
        filter.sharedMesh = mesh;
        renderer.enabled = false;
        return new RibbonLayerVisual(layerObject, renderer, mesh, material, intensity, heightScale, widthScale, curlScale, phase);
    }

    private void UpdateRibbons(float timeline)
    {
        if (outerRibbon == null || visibleHeight <= 0f)
            return;

        float sparkEnd = ToTimeline(0.8f);
        float spreadEnd = ToTimeline(SpreadFinishTime);
        float fadeStart = ToTimeline(FadeStartTime);
        if (timeline < sparkEnd)
        {
            SetRibbonVisibility(false);
            return;
        }

        float spread = EvaluateSpread(timeline);
        float growth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(sparkEnd, spreadEnd, timeline));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, timeline));
        float sequenceSeconds = timeline * duration;
        PrepareRibbonBoundary(spread);
        PrepareTongueField(sequenceSeconds);
        UpdateRibbonLayer(outerRibbon, growth, fade, sequenceSeconds);
        UpdateRibbonLayer(bodyRibbon, growth, fade, sequenceSeconds);
        UpdateRibbonLayer(coreRibbon, growth, fade, sequenceSeconds);
        SetRibbonVisibility(fade > 0.001f);
    }

    private void PrepareRibbonBoundary(float frontProgress)
    {
        float horizontalReach = visibleWidth * Mathf.Max(burnCenter.x, 1f - burnCenter.x);
        float verticalReach = visibleHeight * Mathf.Max(burnCenter.y, 1f - burnCenter.y);
        float cornerRadius = Mathf.Sqrt(horizontalReach * horizontalReach + verticalReach * verticalReach);
        ribbonArcLengths[0] = 0f;

        for (int i = 0; i <= RibbonSegments; i++)
        {
            float angle = i / (float)RibbonSegments * Mathf.PI * 2f;
            float radius = Mathf.Max(visibleHeight * 0.035f,
                frontProgress * maxRadius * cornerRadius + BoundaryProfile(angle) * noiseStrength * visibleHeight);
            ribbonBasePoints[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            if (i > 0)
                ribbonArcLengths[i] = ribbonArcLengths[i - 1] + Vector3.Distance(ribbonBasePoints[i - 1], ribbonBasePoints[i]);
        }
    }

    private void PrepareTongueField(float sequenceSeconds)
    {
        int clusterCount = Mathf.Clamp(Mathf.RoundToInt(10f * tongueActivity), 8, 12);
        float animatedTime = sequenceSeconds * tongueSpeed;

        for (int i = 0; i <= RibbonSegments; i++)
        {
            float position = i == RibbonSegments ? 0f : i / (float)RibbonSegments;
            float strongestPulse = 0f;
            float strongestLean = 0f;

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                int seed = cluster * 193 + 41;
                float riseDuration = Mathf.Lerp(0.27f, 0.36f, Hash01(seed + 3));
                float fallDuration = Mathf.Lerp(0.48f, 0.76f, Hash01(seed + 7));
                float restDuration = Mathf.Lerp(0.08f, 0.2f, Hash01(seed + 11));
                float cycleDuration = riseDuration + fallDuration + restDuration;
                float phaseOffset = Hash01(seed + 13) * cycleDuration;
                float cyclePosition = animatedTime + phaseOffset;
                int generation = Mathf.FloorToInt(cyclePosition / cycleDuration);
                float age = Mathf.Repeat(cyclePosition, cycleDuration);

                float envelope;
                if (age < riseDuration)
                    envelope = Mathf.SmoothStep(0f, 1f, age / riseDuration);
                else if (age < riseDuration + fallDuration)
                    envelope = 1f - Mathf.SmoothStep(0f, 1f, (age - riseDuration) / fallDuration);
                else
                    envelope = 0f;
                if (envelope <= 0.001f)
                    continue;

                int generationSeed = seed + generation * 977;
                float center = Hash01(generationSeed + 17);
                float halfWidth = Mathf.Lerp(0.018f, 0.038f, Hash01(generationSeed + 23));
                float wrappedDistance = Mathf.Abs(Mathf.Repeat(position - center + 0.5f, 1f) - 0.5f);
                float spatial = 1f - Mathf.SmoothStep(0f, 1f, wrappedDistance / halfWidth);
                float amplitude = Mathf.Lerp(0.78f, 1.08f, Hash01(generationSeed + 29));
                float pulse = envelope * spatial * amplitude;
                if (pulse <= strongestPulse)
                    continue;

                strongestPulse = pulse;
                strongestLean = HashSigned(generationSeed + 31) * envelope;
            }

            float activityScale = Mathf.Lerp(0.82f, 1.18f, Mathf.InverseLerp(0.5f, 1.5f, tongueActivity));
            ribbonTonguePulses[i] = Mathf.Clamp01(strongestPulse * activityScale);
            ribbonTongueLeans[i] = strongestLean;
        }
    }

    private void UpdateRibbonLayer(RibbonLayerVisual layer, float growth, float fade, float sequenceSeconds)
    {
        if (layer == null)
            return;

        float totalArc = Mathf.Max(0.001f, ribbonArcLengths[RibbonSegments]);
        float matureHeight = visibleHeight * flameHeight * Mathf.Lerp(0.16f, 1f, growth);
        float rootWidth = visibleHeight * Mathf.Lerp(0.0045f, 0.011f, growth) * layer.WidthScale;

        for (int i = 0; i <= RibbonSegments; i++)
        {
            float normalized = i / (float)RibbonSegments;
            float angle = normalized * Mathf.PI * 2f;
            Vector3 radial = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            Vector3 basePoint = ribbonBasePoints[i];
            float topBias = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.3f, 1f, Mathf.Sin(angle)));
            float pulse = ribbonTonguePulses[i];
            float lowFlameVariation = 0.025f * Mathf.Sin(angle * 13f - sequenceSeconds * 4.1f + layer.Phase)
                + 0.018f * Mathf.Sin(angle * 23f + sequenceSeconds * 6.3f);
            float lowFlame = 0.21f + lowFlameVariation;
            float highTongue = pulse * (0.78f + topBias * 0.14f);
            float height = matureHeight * layer.HeightScale * Mathf.Max(0.14f, lowFlame + highTongue);
            float screenTop = visibleHeight * (1f - burnCenter.y) - visibleHeight * 0.018f;
            float availableHeight = screenTop - basePoint.y;
            height = Mathf.Min(height, Mathf.Max(visibleHeight * 0.052f * layer.HeightScale, availableHeight));
            float curl = height * flameCurlStrength * layer.CurlScale *
                (ribbonTongueLeans[i] * (0.42f + pulse * 0.46f)
                 + Mathf.Sin(angle * 5.3f + sequenceSeconds * 3.8f + layer.Phase) * (0.12f + pulse * 0.16f)
                 + Mathf.Sin(angle * 11.7f - sequenceSeconds * 2.6f) * 0.08f);
            float secondaryBend = height * flameCurlStrength * layer.CurlScale *
                (Mathf.Sin(angle * 7.7f - sequenceSeconds * 4.9f) * (0.1f + pulse * 0.18f));

            int vertex = i * RibbonRows;
            layer.Vertices[vertex] = basePoint + radial * rootWidth;
            layer.Vertices[vertex + 1] = basePoint - radial * rootWidth;
            layer.Vertices[vertex + 2] = basePoint - radial * rootWidth * 0.34f
                + Vector3.up * height * 0.27f
                + Vector3.right * (curl * 0.12f - secondaryBend * 0.7f);
            layer.Vertices[vertex + 3] = basePoint - radial * rootWidth * 0.08f
                + Vector3.up * height * 0.64f
                + Vector3.right * (curl * 0.55f + secondaryBend * 0.48f);
            layer.Vertices[vertex + 4] = basePoint + Vector3.up * height + Vector3.right * curl;
            float u = ribbonArcLengths[i] / totalArc;
            layer.Uvs[vertex] = new Vector2(u, 0f);
            layer.Uvs[vertex + 1] = new Vector2(u, 0.075f);
            layer.Uvs[vertex + 2] = new Vector2(u, 0.34f);
            layer.Uvs[vertex + 3] = new Vector2(u, 0.68f);
            layer.Uvs[vertex + 4] = new Vector2(u, 1f);
            Vector2 tongueData = new Vector2(pulse, Mathf.Abs(ribbonTongueLeans[i]));
            for (int row = 0; row < RibbonRows; row++)
                layer.Uv2[vertex + row] = tongueData;
        }

        layer.ApplyMesh();
        layer.Material.SetFloat("_Intensity", layer.BaseIntensity * Mathf.Lerp(0.68f, 1f, growth) * Mathf.Lerp(0.8f, 1.18f, density));
        layer.Material.SetFloat(SequenceTimeId, sequenceSeconds);
        layer.Material.SetFloat(ParticleFadeId, fade);
        layer.Material.SetFloat("_TongueActivity", tongueActivity);
    }

    private void SetRibbonVisibility(bool visible)
    {
        if (outerRibbon != null) outerRibbon.Renderer.enabled = visible;
        if (bodyRibbon != null) bodyRibbon.Renderer.enabled = visible;
        if (coreRibbon != null) coreRibbon.Renderer.enabled = visible;
    }

    private void ResetRibbons()
    {
        ResetRibbon(outerRibbon);
        ResetRibbon(bodyRibbon);
        ResetRibbon(coreRibbon);
    }

    private static void ResetRibbon(RibbonLayerVisual layer)
    {
        if (layer == null)
            return;
        layer.Renderer.enabled = false;
        layer.Material.SetFloat("_GlobalFade", 0f);
        layer.Material.SetFloat("_SequenceTime", 0f);
        layer.Mesh.Clear();
    }

    private void UpdateParticles(float timeline, float deltaTime)
    {
        float sparkEnd = ToTimeline(0.8f);
        float ignitionEnd = ToTimeline(1.2f);
        float spreadEnd = ToTimeline(SpreadFinishTime);
        float fadeStart = ToTimeline(FadeStartTime);
        SetParticleFade(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStart, 1f, timeline)));
        if (particleRoot == null || fallingSpark == null || visibleHeight <= 0f)
            return;

        if (timeline < sparkEnd)
        {
            float fall = Mathf.SmoothStep(0f, 1f, timeline / Mathf.Max(sparkEnd, 0.001f));
            Vector3 position = new Vector3(0f, Mathf.Lerp(visibleHeight * 0.52f, 0f, fall), -visibleHeight * 0.008f);
            EmitFallingSpark(position);
            return;
        }

        if (!impactEmitted)
        {
            EmitImpact();
            impactEmitted = true;
        }

        if (timeline < ignitionEnd || timeline >= fadeStart)
            return;

        float growth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ignitionEnd, spreadEnd, timeline));
        float frontProgress = EvaluateSpread(timeline);
        smokeAccumulator += deltaTime * Mathf.Lerp(2f, 10f, growth) * density;
        emberAccumulator += deltaTime * Mathf.Lerp(5f, 34f, growth) * density * emberAmountMultiplier;

        while (smokeAccumulator >= 1f)
        {
            EmitSmoke(frontProgress, growth);
            smokeAccumulator -= 1f;
        }
        while (emberAccumulator >= 1f)
        {
            EmitEmber(frontProgress, growth);
            emberAccumulator -= 1f;
        }
    }

    private void EmitFallingSpark(Vector3 position)
    {
        int index = emissionIndex++;
        float variation = Hash01(index + 17);
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position + new Vector3(HashSigned(index + 7) * visibleWidth * 0.0018f, 0f, 0f),
            velocity = new Vector3(HashSigned(index + 11) * visibleWidth * 0.004f, visibleHeight * 0.032f, 0f),
            startLifetime = Mathf.Lerp(0.15f, 0.21f, variation),
            startSize3D = new Vector3(
                visibleHeight * Mathf.Lerp(0.014f, 0.021f, variation),
                visibleHeight * Mathf.Lerp(0.048f, 0.066f, Hash01(index + 23)),
                visibleHeight * 0.004f),
            rotation = HashSigned(index + 29) * 0.065f,
            startColor = Color.Lerp(
                new Color(1f, 0.62f, 0.12f, 0.9f),
                new Color(1f, 1f, 0.72f, 1f), variation)
        };
        fallingSpark.Emit(emit, 1);
    }

    private void EmitImpact()
    {
        for (int i = 0; i < 42; i++)
        {
            float angle = Hash01(i * 17 + 3) * Mathf.PI * 2f;
            float speed = Mathf.Lerp(visibleHeight * 0.08f, visibleHeight * 0.38f, Hash01(i * 31 + 11));
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Abs(Mathf.Sin(angle)) * 0.78f + 0.1f, HashSigned(i * 13) * 0.45f);
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
            {
                position = new Vector3(0f, 0f, HashSigned(i * 23) * visibleHeight * 0.025f),
                velocity = direction * speed,
                startLifetime = Mathf.Lerp(0.22f, 0.5f, Hash01(i * 47)),
                startSize = Mathf.Lerp(visibleHeight * 0.004f, visibleHeight * 0.012f, Hash01(i * 59)),
                startColor = Color.Lerp(new Color(1f, 0.2f, 0.01f, 1f), new Color(1f, 1f, 0.75f, 1f), Hash01(i * 71))
            };
            impactSparks.Emit(emit, 1);
        }
    }

    private void EmitSmoke(float frontProgress, float growth)
    {
        int index = emissionIndex++;
        Vector3 position = GetFrontPosition(index, frontProgress);
        float size = visibleHeight * Mathf.Lerp(0.07f, 0.17f, Hash01(index + 73));
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position + new Vector3(0f, size * 0.2f, visibleHeight * 0.045f),
            velocity = new Vector3(HashSigned(index + 79) * visibleWidth * 0.012f, visibleHeight * 0.045f, HashSigned(index + 83) * visibleHeight * 0.018f),
            startLifetime = Mathf.Lerp(1.1f, 1.85f, Hash01(index + 89)),
            startSize = size,
            rotation = HashSigned(index + 97) * Mathf.PI,
            startColor = new Color(1f, 1f, 1f, Mathf.Lerp(0.12f, 0.28f, growth))
        };
        smoke.Emit(emit, 1);
    }

    private void EmitEmber(float frontProgress, float growth)
    {
        int index = emissionIndex++;
        Vector3 position = GetFrontPosition(index, frontProgress);
        ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = new Vector3(HashSigned(index + 101) * visibleWidth * 0.05f, visibleHeight * Mathf.Lerp(0.12f, 0.3f, Hash01(index + 107)), HashSigned(index + 109) * visibleHeight * 0.08f),
            startLifetime = Mathf.Lerp(0.45f, 1.05f, Hash01(index + 113)),
            startSize = visibleHeight * Mathf.Lerp(0.002f, 0.006f, Hash01(index + 127)),
            startColor = Color.Lerp(new Color(1f, 0.15f, 0.005f, 0.85f), new Color(1f, 0.9f, 0.32f, 1f), Hash01(index + 131))
        };
        embers.Emit(emit, 1);
    }

    private Vector3 GetFrontPosition(int index, float frontProgress)
    {
        const float goldenAngle = 2.39996323f;
        float angle = index * goldenAngle;
        float horizontalReach = visibleWidth * Mathf.Max(burnCenter.x, 1f - burnCenter.x);
        float verticalReach = visibleHeight * Mathf.Max(burnCenter.y, 1f - burnCenter.y);
        float cornerRadius = Mathf.Sqrt(horizontalReach * horizontalReach + verticalReach * verticalReach);
        float radius = Mathf.Max(visibleHeight * 0.035f,
            frontProgress * maxRadius * cornerRadius + BoundaryProfile(angle) * noiseStrength * visibleHeight);
        float x = Mathf.Cos(angle) * radius;
        float y = Mathf.Sin(angle) * radius;
        float z = HashSigned(index * 97 + 19) * visibleHeight * 0.075f;
        return new Vector3(x, y, z);
    }

    private static float BoundaryProfile(float angle)
    {
        return (Mathf.Sin(angle * 5f + 1.7f) * 0.28f
              + Mathf.Sin(angle * 9f - 0.8f) * 0.22f
              + Mathf.Sin(angle * 17f + 2.4f) * 0.16f
              + Mathf.Sin(angle * 29f - 1.1f) * 0.10f) * 0.46f;
    }

    private ParticleSystem CreateSystem(string objectName, Material material, int maxParticles, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(particleRoot.transform, false);
        ParticleSystem system = child.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = duration;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.maxParticles = maxParticles;
        main.startSpeed = 0f;
        main.startLifetime = 0.6f;
        main.startSize = 0.1f;
        main.startSize3D = true;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.material = material;
        renderer.sortingOrder = sortingOrder;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        return system;
    }

    private void ConfigureSparkSystem(ParticleSystem system, bool stretched)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = stretched ? 0.035f : 0f;
        renderer.lengthScale = stretched ? 1.8f : 1f;
        SetFadeGradient(system, new Color(1f, 0.7f, 0.15f), new Color(1f, 0.08f, 0.001f), 1f);
    }

    private void ConfigureFallingFlameSystem(ParticleSystem system)
    {
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.velocityScale = 0f;
        renderer.lengthScale = 1f;
        renderer.pivot = new Vector3(0f, -0.16f, 0f);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.72f),
            new Keyframe(0.18f, 1f),
            new Keyframe(0.72f, 0.88f),
            new Keyframe(1f, 0.36f)));
        SetFadeGradient(system, new Color(1f, 0.96f, 0.68f), new Color(1f, 0.08f, 0.001f), 1f);
    }

    private void ConfigureSmokeSystem(ParticleSystem system)
    {
        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.48f), new Keyframe(0.32f, 1f), new Keyframe(1f, 1.45f)));
        SetFadeGradient(system, new Color(0.42f, 0.4f, 0.38f), new Color(0.06f, 0.055f, 0.05f), 0.32f);
    }

    private void ConfigureEmberSystem(ParticleSystem system)
    {
        SetFadeGradient(system, new Color(1f, 0.9f, 0.4f), new Color(1f, 0.06f, 0.001f), 1f);
    }

    private static void SetFadeGradient(ParticleSystem system, Color start, Color end, float peakAlpha)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(start, 0f), new GradientColorKey(end, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.12f), new GradientAlphaKey(peakAlpha * 0.7f, 0.62f), new GradientAlphaKey(0f, 1f) });
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        color.color = gradient;
    }

    private static Material CreateParticleMaterial(Shader shader, Texture texture, Color core, Color edge, float intensity, bool additive)
    {
        Material material = new Material(shader) { name = "M_MainMenuBurnParticle_Runtime" };
        material.SetTexture("_MainTex", texture);
        material.SetColor("_CoreColor", core);
        material.SetColor("_EdgeColor", edge);
        material.SetFloat("_Intensity", intensity);
        material.SetFloat(ParticleFadeId, 1f);
        material.SetFloat("_Cutoff", additive ? 0.025f : 0.018f);
        material.SetFloat("_Softness", additive ? 0.16f : 0.12f);
        material.SetVector("_UvRect", additive
            ? new Vector4(0f, 0f, 1f, 1f)
            : new Vector4(0.17f, 0.12f, 0.66f, 0.76f));
        material.SetFloat("_FlipV", 0f);
        material.SetFloat("_TextureColorWeight", additive ? 0.82f : 0.9f);
        material.SetFloat("_ProceduralShape", 0f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
        return material;
    }

    private void SetParticleFade(float fade)
    {
        fade = Mathf.Clamp01(fade);
        SetMaterialFade(fallingFlameMaterial, fade);
        SetMaterialFade(sparkMaterial, fade);
        SetMaterialFade(smokeMaterial, fade);
    }

    private static void SetMaterialFade(Material material, float fade)
    {
        if (material != null)
            material.SetFloat(ParticleFadeId, fade);
    }

    private void PlaySystems()
    {
        foreach (ParticleSystem system in GetSystems())
        {
            if (system != null)
                system.Play(true);
        }
    }

    private void StopAndClearSystems()
    {
        foreach (ParticleSystem system in GetSystems())
        {
            if (system == null)
                continue;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            system.Clear(true);
        }
    }

    private ParticleSystem[] GetSystems()
    {
        return new[] { fallingSpark, impactSparks, smoke, embers };
    }

    private static float Hash01(int value)
    {
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f + 78.233f) * 43758.5453f, 1f);
    }

    private static float HashSigned(int value)
    {
        return Hash01(value) * 2f - 1f;
    }

    private void OnDisable()
    {
        ResetTransition();
    }

    private void OnDestroy()
    {
        DestroyMaterial(charMaterial);
        DestroyRibbon(outerRibbon);
        DestroyRibbon(bodyRibbon);
        DestroyRibbon(coreRibbon);
        DestroyMaterial(fallingFlameMaterial);
        DestroyMaterial(sparkMaterial);
        DestroyMaterial(smokeMaterial);
        if (particleRoot != null)
            Destroy(particleRoot);
    }

    private static void DestroyMaterial(Material material)
    {
        if (material != null)
            Destroy(material);
    }

    private static void DestroyRibbon(RibbonLayerVisual layer)
    {
        if (layer == null)
            return;
        Destroy(layer.Material);
        Destroy(layer.Mesh);
    }

    private sealed class RibbonLayerVisual
    {
        public readonly GameObject GameObject;
        public readonly MeshRenderer Renderer;
        public readonly Mesh Mesh;
        public readonly Material Material;
        public readonly Vector3[] Vertices;
        public readonly Vector2[] Uvs;
        public readonly Vector2[] Uv2;
        public readonly int[] Triangles;
        public readonly float HeightScale;
        public readonly float BaseIntensity;
        public readonly float WidthScale;
        public readonly float CurlScale;
        public readonly float Phase;

        public RibbonLayerVisual(GameObject gameObject, MeshRenderer renderer, Mesh mesh, Material material,
            float baseIntensity, float heightScale, float widthScale, float curlScale, float phase)
        {
            GameObject = gameObject;
            Renderer = renderer;
            Mesh = mesh;
            Material = material;
            BaseIntensity = baseIntensity;
            HeightScale = heightScale;
            WidthScale = widthScale;
            CurlScale = curlScale;
            Phase = phase;
            Vertices = new Vector3[(RibbonSegments + 1) * RibbonRows];
            Uvs = new Vector2[Vertices.Length];
            Uv2 = new Vector2[Vertices.Length];
            Triangles = new int[RibbonSegments * (RibbonRows - 1) * 6];

            for (int i = 0; i < RibbonSegments; i++)
            {
                int vertex = i * RibbonRows;
                int next = (i + 1) * RibbonRows;
                for (int row = 0; row < RibbonRows - 1; row++)
                {
                    int triangle = (i * (RibbonRows - 1) + row) * 6;
                    Triangles[triangle] = vertex + row;
                    Triangles[triangle + 1] = next + row;
                    Triangles[triangle + 2] = vertex + row + 1;
                    Triangles[triangle + 3] = vertex + row + 1;
                    Triangles[triangle + 4] = next + row;
                    Triangles[triangle + 5] = next + row + 1;
                }
            }
        }

        public void ApplyMesh()
        {
            Mesh.Clear();
            Mesh.vertices = Vertices;
            Mesh.uv = Uvs;
            Mesh.uv2 = Uv2;
            Mesh.triangles = Triangles;
            Mesh.RecalculateBounds();
        }
    }
}
