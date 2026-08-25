using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNovelizer.Core.UI
{
    public class PanoramaHotspot
    {
        public string Label;
        public float Yaw;
        public float Pitch;
        public string Command;
    }

    public class PanoramaPanel : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler
    {
        private const float DefaultFov = 58f;
        private const float MinFov = 45f;
        private const float MaxFov = 75f;
        private const float DragSensitivity = 0.08f;
        private const float ViewSmoothTime = 0.08f;
        private const float MinPitch = -65f;
        private const float MaxPitch = 65f;
        private const int CanvasSortingOrder = 3000;
        private const int PanoramaSphereColumns = 128;
        private const int PanoramaSphereRows = 64;
        private const float PanoramaSphereRadius = 100f;
        private const float HotspotDistance = 12f;
        private const float HotspotEdgeFade = 0.08f;
        private const float HotspotIndicatorMargin = 84f;

        private RawImage viewImage;
        private RectTransform rootRect;
        private Camera panoramaCamera;
        private RenderTexture renderTexture;
        private GameObject cameraRoot;
        private GameObject fallbackSphere;
        private Material skyboxMaterial;
        private Material fallbackSphereMaterial;
        private Material previousRenderSettingsSkybox;
        private Action onClosed;
        private Action<string> onHotspotSelected;
        private readonly List<HotspotView> hotspotViews = new List<HotspotView>();
        private Vector2 lastPointerPosition;
        private Texture panoramaTexture;
        private float yaw;
        private float pitch;
        private float targetYaw;
        private float targetPitch;
        private float yawVelocity;
        private float pitchVelocity;
        private bool closed;
        private bool renderSettingsSkyboxOverridden;

        public static PanoramaPanel Create(Transform parent)
        {
            return Create(parent, true);
        }

        public static PanoramaPanel Create(Transform parent, bool showExitButton)
        {
            GameObject root = new GameObject("PanoramaPanel", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup), typeof(RawImage));
            root.transform.SetParent(parent, false);

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.SetAsLastSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = CanvasSortingOrder;

            PanoramaPanel panel = root.AddComponent<PanoramaPanel>();
            panel.BuildUi(root, showExitButton);
            return panel;
        }

        public void Show(Texture texture, float startYaw, float startPitch, Action closedCallback)
        {
            Show(texture, startYaw, startPitch, closedCallback, null, null);
        }

        public void Show(Texture texture, float startYaw, float startPitch, Action closedCallback, IList<PanoramaHotspot> hotspots, Action<string> hotspotSelectedCallback)
        {
            if (texture == null)
            {
                Debug.LogError("[PanoramaPanel] Panorama texture is null.");
                Close();
                return;
            }

            panoramaTexture = texture;
            panoramaTexture.wrapMode = TextureWrapMode.Repeat;
#if UNITY_2017_1_OR_NEWER
            panoramaTexture.wrapModeU = TextureWrapMode.Repeat;
            panoramaTexture.wrapModeV = TextureWrapMode.Clamp;
#endif
            onClosed = closedCallback;
            onHotspotSelected = hotspotSelectedCallback;
            yaw = startYaw;
            pitch = Mathf.Clamp(startPitch, MinPitch, MaxPitch);
            targetYaw = yaw;
            targetPitch = pitch;
            yawVelocity = 0f;
            pitchVelocity = 0f;

            EnsureRenderTexture();
            CreatePanoramaCamera();
            ConfigureSkyboxOrFallbackSphere();
            CreateHotspots(hotspots);
            ApplyCameraRotation();
            UpdateHotspotViews();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 delta = eventData.position - lastPointerPosition;
            lastPointerPosition = eventData.position;

            targetYaw -= delta.x * DragSensitivity;
            targetPitch = Mathf.Clamp(targetPitch + delta.y * DragSensitivity, MinPitch, MaxPitch);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (panoramaCamera == null) return;

            panoramaCamera.fieldOfView = Mathf.Clamp(panoramaCamera.fieldOfView - eventData.scrollDelta.y * 2f, MinFov, MaxFov);
        }

        private void LateUpdate()
        {
            if (panoramaTexture == null) return;

            if (Screen.width > 0 && Screen.height > 0 &&
                (renderTexture == null || renderTexture.width != Screen.width || renderTexture.height != Screen.height))
            {
                EnsureRenderTexture();
            }

            UpdateSmoothedCameraRotation();
            UpdateHotspotViews();
        }

        private void BuildUi(GameObject root, bool showExitButton)
        {
            rootRect = root.GetComponent<RectTransform>();
            viewImage = root.GetComponent<RawImage>();
            viewImage.color = Color.white;
            viewImage.raycastTarget = true;

            CreateHintText(root.transform);
            if (showExitButton)
            {
                CreateExitButton(root.transform);
            }
        }

        private void CreateHintText(Transform parent)
        {
            GameObject hintObj = new GameObject("Hint", typeof(RectTransform), typeof(Text));
            hintObj.transform.SetParent(parent, false);

            RectTransform rect = hintObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(420f, 48f);

            Text text = hintObj.GetComponent<Text>();
            text.text = "拖动查看";
            text.alignment = TextAnchor.MiddleCenter;
            text.fontSize = 24;
            text.color = new Color(1f, 1f, 1f, 0.72f);
            text.raycastTarget = false;
            text.font = GetUiFont();
        }

        private void CreateExitButton(Transform parent)
        {
            GameObject buttonObj = new GameObject("ExitButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(parent, false);

            RectTransform rect = buttonObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(32f, -48f);
            rect.sizeDelta = new Vector2(112f, 48f);

            Image image = buttonObj.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.52f);

            Button button = buttonObj.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(Close);

            GameObject labelObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(buttonObj.transform, false);

            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            Text label = labelObj.GetComponent<Text>();
            label.text = "返回";
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = 24;
            label.color = Color.white;
            label.raycastTarget = false;
            label.font = GetUiFont();
        }

        private void CreateHotspots(IList<PanoramaHotspot> hotspots)
        {
            ClearHotspots();

            if (hotspots == null || hotspots.Count == 0)
            {
                return;
            }

            int count = Mathf.Min(3, hotspots.Count);
            for (int i = 0; i < count; i++)
            {
                PanoramaHotspot hotspot = hotspots[i];
                if (hotspot == null)
                {
                    continue;
                }

                GameObject hotspotObj = new GameObject("Hotspot", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
                hotspotObj.transform.SetParent(rootRect, false);

                RectTransform rect = hotspotObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(240f, 68f);

                Image background = hotspotObj.GetComponent<Image>();
                background.color = new Color(0f, 0f, 0f, 0.62f);

                CanvasGroup canvasGroup = hotspotObj.GetComponent<CanvasGroup>();

                Button button = hotspotObj.GetComponent<Button>();
                button.targetGraphic = background;
                string command = hotspot.Command;
                button.onClick.AddListener(() => SelectHotspot(command));

                GameObject dotObj = new GameObject("Dot", typeof(RectTransform), typeof(Image));
                dotObj.transform.SetParent(hotspotObj.transform, false);
                RectTransform dotRect = dotObj.GetComponent<RectTransform>();
                dotRect.anchorMin = new Vector2(0f, 0.5f);
                dotRect.anchorMax = new Vector2(0f, 0.5f);
                dotRect.pivot = new Vector2(0f, 0.5f);
                dotRect.anchoredPosition = new Vector2(18f, 0f);
                dotRect.sizeDelta = new Vector2(20f, 20f);
                dotObj.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.85f);

                GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
                labelObj.transform.SetParent(hotspotObj.transform, false);
                RectTransform labelRect = labelObj.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = new Vector2(52f, 0f);
                labelRect.offsetMax = new Vector2(-18f, 0f);

                Text label = labelObj.GetComponent<Text>();
                label.text = string.IsNullOrWhiteSpace(hotspot.Label) ? "调查" : hotspot.Label;
                label.alignment = TextAnchor.MiddleLeft;
                label.fontSize = 30;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 28;
                label.resizeTextMaxSize = 30;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.color = Color.white;
                label.raycastTarget = false;
                label.font = GetUiFont();

                float preferredWidth = label.preferredWidth + 52f + 18f;
                float clampedWidth = Mathf.Clamp(preferredWidth, 240f, 500f);
                rect.sizeDelta = new Vector2(clampedWidth, 68f);

                GameObject indicatorObj = CreateHotspotIndicator(rootRect);
                CanvasGroup indicatorCanvasGroup = indicatorObj.GetComponent<CanvasGroup>();
                RectTransform indicatorRect = indicatorObj.GetComponent<RectTransform>();

                hotspotViews.Add(new HotspotView
                {
                    Data = hotspot,
                    Root = hotspotObj,
                    Rect = rect,
                    CanvasGroup = canvasGroup,
                    IndicatorRoot = indicatorObj,
                    IndicatorRect = indicatorRect,
                    IndicatorCanvasGroup = indicatorCanvasGroup
                });
            }
        }

        private GameObject CreateHotspotIndicator(Transform parent)
        {
            GameObject indicatorObj = new GameObject("HotspotIndicator", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            indicatorObj.transform.SetParent(parent, false);

            RectTransform rect = indicatorObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(54f, 54f);

            Image background = indicatorObj.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0f);
            background.raycastTarget = false;

            CanvasGroup canvasGroup = indicatorObj.GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            GameObject arrowObj = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
            arrowObj.transform.SetParent(indicatorObj.transform, false);

            RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
            arrowRect.anchorMin = Vector2.zero;
            arrowRect.anchorMax = Vector2.one;
            arrowRect.offsetMin = Vector2.zero;
            arrowRect.offsetMax = Vector2.zero;

            Text arrow = arrowObj.GetComponent<Text>();
            arrow.text = ">";
            arrow.alignment = TextAnchor.MiddleCenter;
            arrow.fontSize = 36;
            arrow.color = new Color(1f, 1f, 1f, 0.88f);
            arrow.raycastTarget = false;
            arrow.font = GetUiFont();

            return indicatorObj;
        }

        private void ClearHotspots()
        {
            for (int i = 0; i < hotspotViews.Count; i++)
            {
                if (hotspotViews[i].Root != null)
                {
                    Destroy(hotspotViews[i].Root);
                }

                if (hotspotViews[i].IndicatorRoot != null)
                {
                    Destroy(hotspotViews[i].IndicatorRoot);
                }
            }

            hotspotViews.Clear();
        }

        private void SelectHotspot(string command)
        {
            if (closed)
            {
                return;
            }

            Action<string> callback = onHotspotSelected;
            onHotspotSelected = null;
            callback?.Invoke(command);
            Close();
        }

        private void UpdateHotspotViews()
        {
            if (panoramaCamera == null || cameraRoot == null || rootRect == null || hotspotViews.Count == 0)
            {
                return;
            }

            for (int i = 0; i < hotspotViews.Count; i++)
            {
                HotspotView view = hotspotViews[i];
                if (view == null || view.Data == null || view.Root == null)
                {
                    continue;
                }

                Vector3 direction = Quaternion.Euler(view.Data.Pitch, view.Data.Yaw, 0f) * Vector3.forward;
                Vector3 worldPosition = cameraRoot.transform.position + direction * HotspotDistance;
                Vector3 viewportPosition = panoramaCamera.WorldToViewportPoint(worldPosition);
                bool visible = viewportPosition.z > 0f &&
                               viewportPosition.x > 0f && viewportPosition.x < 1f &&
                               viewportPosition.y > 0f && viewportPosition.y < 1f;

                if (!visible)
                {
                    view.CanvasGroup.alpha = 0f;
                    view.CanvasGroup.interactable = false;
                    view.CanvasGroup.blocksRaycasts = false;
                    UpdateHotspotIndicator(view);
                    continue;
                }

                HideHotspotIndicator(view);

                Vector2 screenPoint = new Vector2(viewportPosition.x * Screen.width, viewportPosition.y * Screen.height);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rootRect, screenPoint, null, out Vector2 localPoint))
                {
                    view.Rect.anchoredPosition = localPoint;
                }

                float edgeDistance = Mathf.Min(
                    Mathf.Min(viewportPosition.x, 1f - viewportPosition.x),
                    Mathf.Min(viewportPosition.y, 1f - viewportPosition.y));
                float alpha = Mathf.Clamp01(edgeDistance / HotspotEdgeFade);
                view.CanvasGroup.alpha = alpha;
                view.CanvasGroup.interactable = alpha > 0.45f;
                view.CanvasGroup.blocksRaycasts = alpha > 0.45f;
            }
        }

        private void UpdateHotspotIndicator(HotspotView view)
        {
            if (view == null || view.Data == null || view.IndicatorRect == null || view.IndicatorCanvasGroup == null)
            {
                return;
            }

            float yawDelta = Mathf.DeltaAngle(yaw, view.Data.Yaw);
            float pitchDelta = Mathf.Clamp(view.Data.Pitch - pitch, -60f, 60f);
            float yawRadians = yawDelta * Mathf.Deg2Rad;
            Vector2 screenDirection = new Vector2(Mathf.Sin(yawRadians), -Mathf.Sin(pitchDelta * Mathf.Deg2Rad));

            if (Mathf.Abs(yawDelta) > 90f && Mathf.Abs(screenDirection.x) < 0.2f)
            {
                screenDirection.x = yawDelta >= 0f ? 1f : -1f;
            }

            if (screenDirection.sqrMagnitude < 0.0001f)
            {
                screenDirection = new Vector2(yawDelta >= 0f ? 1f : -1f, 0f);
            }

            screenDirection.Normalize();

            Rect rect = rootRect.rect;
            float halfWidth = Mathf.Max(0f, rect.width * 0.5f - HotspotIndicatorMargin);
            float halfHeight = Mathf.Max(0f, rect.height * 0.5f - HotspotIndicatorMargin);
            float xScale = Mathf.Abs(screenDirection.x) > 0.001f ? halfWidth / Mathf.Abs(screenDirection.x) : float.PositiveInfinity;
            float yScale = Mathf.Abs(screenDirection.y) > 0.001f ? halfHeight / Mathf.Abs(screenDirection.y) : float.PositiveInfinity;
            float scale = Mathf.Min(xScale, yScale);
            if (float.IsInfinity(scale))
            {
                scale = Mathf.Min(halfWidth, halfHeight);
            }

            view.IndicatorRect.anchoredPosition = screenDirection * scale;
            view.IndicatorRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg);
            view.IndicatorCanvasGroup.alpha = 0.78f;
            view.IndicatorCanvasGroup.interactable = false;
            view.IndicatorCanvasGroup.blocksRaycasts = false;
        }

        private void HideHotspotIndicator(HotspotView view)
        {
            if (view == null || view.IndicatorCanvasGroup == null)
            {
                return;
            }

            view.IndicatorCanvasGroup.alpha = 0f;
            view.IndicatorCanvasGroup.interactable = false;
            view.IndicatorCanvasGroup.blocksRaycasts = false;
        }

        private Font GetUiFont()
        {
#if UNITY_EDITOR
            Font liberationSans = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (liberationSans != null)
            {
                return liberationSans;
            }
#endif

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                return font;
            }

            return null;
        }

        private void EnsureRenderTexture()
        {
            int width = Mathf.Max(1, Screen.width);
            int height = Mathf.Max(1, Screen.height);

            if (renderTexture != null && renderTexture.width == width && renderTexture.height == height)
            {
                return;
            }

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            renderTexture.name = "PanoramaPanelRT";
            renderTexture.Create();

            if (viewImage != null)
            {
                viewImage.texture = renderTexture;
                viewImage.uvRect = new Rect(0f, 0f, 1f, 1f);
            }

            if (panoramaCamera != null)
            {
                panoramaCamera.targetTexture = renderTexture;
            }
        }

        private void CreatePanoramaCamera()
        {
            if (cameraRoot != null) return;

            cameraRoot = new GameObject("PanoramaCameraRoot");
            cameraRoot.transform.position = new Vector3(10000f, 10000f, 10000f);

            GameObject cameraObj = new GameObject("PanoramaCamera");
            cameraObj.transform.SetParent(cameraRoot.transform, false);
            cameraObj.transform.localPosition = Vector3.zero;
            cameraObj.transform.localRotation = Quaternion.identity;

            panoramaCamera = cameraObj.AddComponent<Camera>();
            panoramaCamera.enabled = true;
            panoramaCamera.fieldOfView = DefaultFov;
            panoramaCamera.nearClipPlane = 0.01f;
            panoramaCamera.farClipPlane = 200f;
            panoramaCamera.clearFlags = CameraClearFlags.Skybox;
            panoramaCamera.cullingMask = 0;
            panoramaCamera.targetTexture = renderTexture;
        }

        private void ConfigureSkyboxOrFallbackSphere()
        {
            if (panoramaCamera == null || panoramaTexture == null) return;

            ConfigureFallbackSphere();
            return;

#pragma warning disable CS0162
            Shader panoramicShader = Shader.Find("Skybox/Panoramic");
            if (panoramicShader != null)
            {
                skyboxMaterial = new Material(panoramicShader);
                skyboxMaterial.name = "PanoramaSkyboxMaterial";

                if (!TryConfigurePanoramicSkyboxMaterial(skyboxMaterial))
                {
                    Debug.LogWarning("[PanoramaPanel] Skybox/Panoramic shader does not expose a supported texture property. Falling back to an inverted sphere.");
                    Destroy(skyboxMaterial);
                    skyboxMaterial = null;
                    ConfigureFallbackSphere();
                    return;
                }

                Skybox skybox = panoramaCamera.gameObject.GetComponent<Skybox>();
                if (skybox == null)
                {
                    skybox = panoramaCamera.gameObject.AddComponent<Skybox>();
                }

                skybox.material = skyboxMaterial;
                OverrideRenderSettingsSkybox(skyboxMaterial);
                panoramaCamera.clearFlags = CameraClearFlags.Skybox;
                panoramaCamera.cullingMask = 0;
                return;
            }

            Debug.LogWarning("[PanoramaPanel] Skybox/Panoramic shader not found. Falling back to an inverted sphere.");
            ConfigureFallbackSphere();
#pragma warning restore CS0162
        }

        private void OverrideRenderSettingsSkybox(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (!renderSettingsSkyboxOverridden)
            {
                previousRenderSettingsSkybox = RenderSettings.skybox;
                renderSettingsSkyboxOverridden = true;
            }

            RenderSettings.skybox = material;
        }

        private void RestoreRenderSettingsSkybox()
        {
            if (!renderSettingsSkyboxOverridden)
            {
                return;
            }

            RenderSettings.skybox = previousRenderSettingsSkybox;
            previousRenderSettingsSkybox = null;
            renderSettingsSkyboxOverridden = false;
        }

        private bool TryConfigurePanoramicSkyboxMaterial(Material material)
        {
            bool hasTextureProperty = false;

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", panoramaTexture);
                hasTextureProperty = true;
            }

            if (material.HasProperty("_Tex"))
            {
                material.SetTexture("_Tex", panoramaTexture);
                hasTextureProperty = true;
            }

            if (!hasTextureProperty)
            {
                return false;
            }

            if (material.HasProperty("_Exposure"))
            {
                material.SetFloat("_Exposure", 1f);
            }

            if (material.HasProperty("_Rotation"))
            {
                material.SetFloat("_Rotation", 0f);
            }

            if (material.HasProperty("_Mapping"))
            {
                material.SetFloat("_Mapping", 1f);
            }

            if (material.HasProperty("_ImageType"))
            {
                material.SetFloat("_ImageType", 0f);
            }

            if (material.HasProperty("_Layout"))
            {
                material.SetFloat("_Layout", 0f);
            }

            return true;
        }

        private void ConfigureFallbackSphere()
        {
            RestoreRenderSettingsSkybox();
            panoramaCamera.clearFlags = CameraClearFlags.SolidColor;
            panoramaCamera.backgroundColor = Color.black;
            panoramaCamera.cullingMask = 1 << gameObject.layer;
            CreateFallbackSphere();
        }

        private void CreateFallbackSphere()
        {
            if (fallbackSphere != null || cameraRoot == null || panoramaTexture == null) return;

            fallbackSphere = new GameObject("PanoramaFallbackSphere", typeof(MeshFilter), typeof(MeshRenderer));
            fallbackSphere.transform.SetParent(cameraRoot.transform, false);
            fallbackSphere.transform.localPosition = Vector3.zero;
            fallbackSphere.transform.localRotation = Quaternion.identity;
            fallbackSphere.transform.localScale = Vector3.one;
            fallbackSphere.layer = gameObject.layer;

            MeshFilter meshFilter = fallbackSphere.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                meshFilter.sharedMesh = CreateInsideOutEquirectSphereMesh();
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader == null)
            {
                Debug.LogError("[PanoramaPanel] No fallback shader found for panorama rendering.");
                return;
            }

            fallbackSphereMaterial = new Material(shader);
            fallbackSphereMaterial.name = "PanoramaSphereMaterial";
            fallbackSphereMaterial.mainTexture = panoramaTexture;
            SetMaterialTexture(fallbackSphereMaterial, panoramaTexture);

            MeshRenderer renderer = fallbackSphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = fallbackSphereMaterial;
            }
        }

        private Mesh CreateInsideOutEquirectSphereMesh()
        {
            int columns = PanoramaSphereColumns;
            int rows = PanoramaSphereRows;
            int vertexCount = (columns + 1) * (rows + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[columns * rows * 6];

            int vertexIndex = 0;
            for (int row = 0; row <= rows; row++)
            {
                float v = (float)row / rows;
                float theta = v * Mathf.PI;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int column = 0; column <= columns; column++)
                {
                    float u = (float)column / columns;
                    float phi = u * Mathf.PI * 2f;
                    Vector3 direction = new Vector3(
                        Mathf.Sin(phi) * sinTheta,
                        cosTheta,
                        Mathf.Cos(phi) * sinTheta);

                    vertices[vertexIndex] = direction * PanoramaSphereRadius;
                    normals[vertexIndex] = -direction;
                    uvs[vertexIndex] = new Vector2(u, 1f - v);
                    vertexIndex++;
                }
            }

            int triangleIndex = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int current = row * (columns + 1) + column;
                    int next = current + columns + 1;

                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = next + 1;
                    triangles[triangleIndex++] = next;

                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = next + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "PanoramaInsideOutSphereMesh";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void SetMaterialTexture(Material material, Texture texture)
        {
            if (material == null || texture == null)
            {
                return;
            }

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }
        }

        private void ApplyCameraRotation()
        {
            if (panoramaCamera == null) return;

            panoramaCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void UpdateSmoothedCameraRotation()
        {
            if (panoramaCamera == null) return;

            yaw = Mathf.SmoothDampAngle(yaw, targetYaw, ref yawVelocity, ViewSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVelocity, ViewSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            ApplyCameraRotation();
        }

        private void RenderPanoramaFrame()
        {
            // URP renders enabled cameras into their target textures during the render loop.
        }

        public void Close()
        {
            if (closed) return;
            closed = true;

            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            RestoreRenderSettingsSkybox();

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
                renderTexture = null;
            }

            if (skyboxMaterial != null)
            {
                Destroy(skyboxMaterial);
                skyboxMaterial = null;
            }

            if (fallbackSphereMaterial != null)
            {
                Destroy(fallbackSphereMaterial);
                fallbackSphereMaterial = null;
            }

            if (cameraRoot != null)
            {
                Destroy(cameraRoot);
                cameraRoot = null;
            }

            if (!closed)
            {
                Action callback = onClosed;
                onClosed = null;
                closed = true;
                callback?.Invoke();
            }
        }

        private class HotspotView
        {
            public PanoramaHotspot Data;
            public GameObject Root;
            public RectTransform Rect;
            public CanvasGroup CanvasGroup;
            public GameObject IndicatorRoot;
            public RectTransform IndicatorRect;
            public CanvasGroup IndicatorCanvasGroup;
        }
    }
}
