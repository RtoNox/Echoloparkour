using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuEcholocation : MonoBehaviour
{
    [Header("Camera & Ground")]
    public Camera menuCamera;
    public LayerMask groundMask = ~0;
    public float groundRayDistance = 500f;

    [Header("Echolocation")]
    public float waveSpeed = 50f;
    public float maxRadius = 500f;
    public float revealDuration = 1.5f;
    public float fadeDuration = 0.25f;
    public bool permanentReveal = false;
    public GameObject waveVisualPrefab;
    public GameObject clickMarkerPrefab;
    public float clickMarkerLifetime = 0.5f;

    [Header("Sonar Ring Visual")]
    public Material sonarRingMaterial;
    public float ringThickness = 0.5f;
    public int ringSegments = 64;
    public Color ringColor = new Color(0f, 1f, 1f, 1f);
    [Range(0f, 1f)] public float ringFade = 1f;

    // === Cooldown ===
    [Header("Cooldown")]
    [Tooltip("Minimum seconds between two waves being triggered by clicks. Set 0 to disable.")]
    public float clickCooldown = 0.3f;

    [Header("Audio")]
    [Tooltip("Played once on every click that triggers a wave.")]
    public AudioClip clickAudio;
    [Tooltip("Played once per object revealed by the wave.")]
    public AudioClip revealAudio;
    [Range(0f, 1f)] public float clickVolume = 1f;
    [Range(0f, 1f)] public float revealVolume = 1f;
    public Vector2 revealPitchRange = new Vector2(0.95f, 1.05f);
    public int audioPoolSize = 8;

    [Header("Buttons")]
    public List<MenuButton> buttons = new List<MenuButton>();

    [System.Serializable]
    public class MenuButton
    {
        [Tooltip("The cube visual (must have a Renderer). Its Collider is used for click detection.")]
        public GameObject cubeVisual;

        [Tooltip("Optional: the invisible UI Button on a Canvas that handles the actual click.")]
        public Button uiButton;

        [Tooltip("Optional: a legacy UI InputField revealed by the wave.")]
        public InputField uiInputField;

        [Tooltip("Optional: a UI Text component used to display/read the input field's text.")]
        public Text inputText;

        [Tooltip("Optional: label shown when this button is revealed.")]
        public GameObject label;

        [HideInInspector] public float revealTimer = 0f;
        [HideInInspector] public float currentAlpha = 0f;
        [HideInInspector] public bool revealed = false;
        [HideInInspector] public Renderer[] renderers;
        [HideInInspector] public Collider cubeCollider;
        [HideInInspector] public Vector3 worldPosition;

        [HideInInspector] public List<Graphic> uiGraphics = new List<Graphic>();
        [HideInInspector] public List<Color> uiBaseColors = new List<Color>();
        [HideInInspector] public List<Renderer> labelRenderers = new List<Renderer>();
        [HideInInspector] public List<Material> labelMaterials = new List<Material>();
        [HideInInspector] public List<Color> labelBaseColors = new List<Color>();
        [HideInInspector] public CanvasGroup uiCanvasGroup;
        [HideInInspector] public CanvasGroup labelCanvasGroup;

        public bool HasInteractable =>
            uiButton != null || uiInputField != null || inputText != null;
    }

    private bool waveActive = false;
    private Vector3 waveOrigin;
    private float waveRadius = 0f;
    private GameObject waveVisualInstance;
    private readonly HashSet<MenuButton> hitByCurrentWave = new HashSet<MenuButton>();

    private GameObject ringObject;
    private MeshFilter ringFilter;
    private MeshRenderer ringRenderer;
    private Mesh ringMesh;
    private Material ringMatInstance;
    private float ringAlpha = 0f;

    private AudioSource[] audioPool;
    private int audioPoolIndex = 0;

    // === Cooldown timer ===
    private float nextClickTime = 0f;

    void Start()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;

            b.renderers = b.cubeVisual.GetComponentsInChildren<Renderer>(true);
            b.cubeCollider = b.cubeVisual.GetComponentInChildren<Collider>();
            b.worldPosition = b.cubeVisual.transform.position;
            b.currentAlpha = 0f;
            b.revealTimer = 0f;
            b.revealed = false;

            CacheFadeTargets(b);

            SetButtonAlpha(b, 0f);
            SetUIFade(b, 0f);

            SetInteractablesActive(b, false);
        }

        CreateAudioPool();
        CreateRingObject();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Update()
    {
        HandleInput();
        UpdateWave();
        UpdateButtonFades();
        HandleButtonClicks();
    }

    // ---------- Audio pool ----------
    void CreateAudioPool()
    {
        int size = Mathf.Max(1, audioPoolSize);
        audioPool = new AudioSource[size];
        for (int i = 0; i < size; i++)
        {
            var go = new GameObject($"SonarAudio_{i}");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            audioPool[i] = src;
        }
    }

    AudioSource GetNextAudioSource()
    {
        if (audioPool == null || audioPool.Length == 0) CreateAudioPool();
        var src = audioPool[audioPoolIndex];
        audioPoolIndex = (audioPoolIndex + 1) % audioPool.Length;
        return src;
    }

    void PlayClickAudio()
    {
        if (clickAudio == null) return;
        var src = GetNextAudioSource();
        src.pitch = 1f;
        src.PlayOneShot(clickAudio, clickVolume);
    }

    void PlayRevealAudio()
    {
        if (revealAudio == null) return;
        var src = GetNextAudioSource();
        if (revealPitchRange.x < revealPitchRange.y)
            src.pitch = Random.Range(revealPitchRange.x, revealPitchRange.y);
        else
            src.pitch = revealPitchRange.x;
        src.PlayOneShot(revealAudio, revealVolume);
    }

    // ---------- Input ----------
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // === Click cooldown gate ===
            if (clickCooldown > 0f && Time.time < nextClickTime)
                return;

            Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                TriggerWave(hit.point);
            }
            else
            {
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                if (groundPlane.Raycast(ray, out float dist))
                {
                    TriggerWave(ray.GetPoint(dist));
                }
            }
        }
    }

    // ---------- Echolocation wave ----------
    void TriggerWave(Vector3 origin)
    {
        // === Stamp the click cooldown ===
        if (clickCooldown > 0f)
            nextClickTime = Time.time + clickCooldown;

        waveOrigin = origin;
        waveRadius = 0f;
        waveActive = true;
        ringAlpha = 1f;

        hitByCurrentWave.Clear();

        PlayClickAudio();

        if (waveVisualPrefab != null)
        {
            if (waveVisualInstance != null) Destroy(waveVisualInstance);
            waveVisualInstance = Instantiate(waveVisualPrefab, origin, Quaternion.identity);
        }

        if (clickMarkerPrefab != null)
        {
            GameObject marker = Instantiate(clickMarkerPrefab, origin, Quaternion.identity);
            Destroy(marker, clickMarkerLifetime);
        }

        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;
            float d = Vector3.Distance(b.worldPosition, origin);
            if (d < 2f)
            {
                RevealButton(b);
            }
        }
    }

    void UpdateWave()
    {
        if (!waveActive) return;

        waveRadius += waveSpeed * Time.deltaTime;

        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;
            if (hitByCurrentWave.Contains(b)) continue;

            float d = Vector3.Distance(b.worldPosition, waveOrigin);
            if (d <= waveRadius)
            {
                RevealButton(b);
            }
        }

        BuildRingMesh(waveRadius, ringThickness);

        float t = Mathf.Clamp01(waveRadius / maxRadius);
        ringAlpha = 1f - t * ringFade;
        ApplyRingAlpha(ringAlpha);

        if (waveRadius > maxRadius)
        {
            waveActive = false;
            waveRadius = 0f;
            ringAlpha = 0f;
            ApplyRingAlpha(0f);
            if (ringObject != null) ringObject.SetActive(false);

            if (waveVisualInstance != null)
            {
                Destroy(waveVisualInstance);
                waveVisualInstance = null;
            }
        }
    }

    void RevealButton(MenuButton b)
    {
        if (hitByCurrentWave.Contains(b)) return;
        hitByCurrentWave.Add(b);

        if (permanentReveal)
            b.revealed = true;
        else
            b.revealTimer = revealDuration;

        PlayRevealAudio();
    }

    // ---------- Procedural ring ----------
    void CreateRingObject()
    {
        ringObject = new GameObject("SonarRing");
        ringObject.transform.SetParent(transform, false);

        ringFilter = ringObject.AddComponent<MeshFilter>();
        ringRenderer = ringObject.AddComponent<MeshRenderer>();

        Shader fallback = Shader.Find("Sprites/Default");
        Material baseMat = sonarRingMaterial;
        if (baseMat != null)
            ringMatInstance = new Material(baseMat);
        else if (fallback != null)
            ringMatInstance = new Material(fallback);
        else
            ringMatInstance = new Material(Shader.Find("Unlit/Color"));

        if (ringMatInstance.HasProperty("_Color"))
            ringMatInstance.SetColor("_Color", ringColor);
        if (ringMatInstance.HasProperty("_BaseColor"))
            ringMatInstance.SetColor("_BaseColor", ringColor);

        ringMatInstance.renderQueue = 3000;

        ringRenderer.material = ringMatInstance;
        ringRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ringRenderer.receiveShadows = false;

        ringMesh = new Mesh { name = "SonarRingMesh" };
        ringMesh.MarkDynamic();
        ringFilter.mesh = ringMesh;

        ringObject.SetActive(false);
    }

    void BuildRingMesh(float radius, float thickness)
    {
        if (ringMesh == null || ringObject == null) return;
        if (radius < 0.01f) radius = 0.01f;

        int segs = Mathf.Max(8, ringSegments);

        Vector3[] verts = new Vector3[segs * 2];
        int[] tris = new int[segs * 6];

        float inner = Mathf.Max(0f, radius - thickness * 0.5f);
        float outer = radius + thickness * 0.5f;

        for (int i = 0; i < segs; i++)
        {
            float a = (i / (float)segs) * Mathf.PI * 2f;
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

            verts[i * 2 + 0] = new Vector3(cos * inner, 0f, sin * inner);
            verts[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer);
        }

        for (int i = 0; i < segs; i++)
        {
            int next = (i + 1) % segs;
            int vi = i * 2;
            int vn = next * 2;
            int t = i * 6;
            tris[t + 0] = vi + 0; tris[t + 1] = vi + 1; tris[t + 2] = vn + 1;
            tris[t + 3] = vi + 0; tris[t + 4] = vn + 1; tris[t + 5] = vn + 0;
        }

        ringMesh.Clear();
        ringMesh.vertices = verts;
        ringMesh.triangles = tris;
        ringMesh.RecalculateNormals();
        ringMesh.RecalculateBounds();

        ringObject.transform.position = waveOrigin + Vector3.up * 0.02f;
        ringObject.transform.rotation = Quaternion.identity;
        ringObject.transform.localScale = Vector3.one;

        if (!ringObject.activeSelf) ringObject.SetActive(true);
    }

    void ApplyRingAlpha(float a)
    {
        if (ringMatInstance == null) return;

        Color c = ringColor;
        c.a = Mathf.Clamp01(a) * ringColor.a;

        if (ringMatInstance.HasProperty("_Color"))
            ringMatInstance.SetColor("_Color", c);
        if (ringMatInstance.HasProperty("_BaseColor"))
            ringMatInstance.SetColor("_BaseColor", c);
    }

    // ---------- Cache fade targets ----------
    void CacheFadeTargets(MenuButton b)
    {
        b.uiGraphics.Clear();
        b.uiBaseColors.Clear();
        b.labelRenderers.Clear();
        b.labelMaterials.Clear();
        b.labelBaseColors.Clear();

        if (b.uiButton != null)
            b.uiCanvasGroup = b.uiButton.GetComponentInParent<CanvasGroup>();
        if (b.uiInputField != null && b.uiCanvasGroup == null)
            b.uiCanvasGroup = b.uiInputField.GetComponentInParent<CanvasGroup>();

        if (b.label != null)
            b.labelCanvasGroup = b.label.GetComponent<CanvasGroup>();
        if (b.labelCanvasGroup == null && b.label != null)
            b.labelCanvasGroup = b.label.GetComponentInParent<CanvasGroup>();

        var seen = new HashSet<Graphic>();

        if (b.uiButton != null)
            CollectGraphics(b.uiButton.gameObject, b.uiGraphics, b.uiBaseColors, seen);

        if (b.uiInputField != null)
        {
            CollectGraphics(b.uiInputField.gameObject, b.uiGraphics, b.uiBaseColors, seen);
            if (b.uiInputField.textComponent != null)
                CollectGraphics(b.uiInputField.textComponent.gameObject,
                                b.uiGraphics, b.uiBaseColors, seen);
            if (b.uiInputField.placeholder is Graphic ph)
                CollectGraphics(ph.gameObject, b.uiGraphics, b.uiBaseColors, seen);
        }

        if (b.inputText != null)
            CollectGraphics(b.inputText.gameObject, b.uiGraphics, b.uiBaseColors, seen);

        if (b.label != null)
        {
            var lrs = b.label.GetComponentsInChildren<Renderer>(true);
            foreach (var r in lrs)
            {
                b.labelRenderers.Add(r);
                foreach (var mat in r.materials)
                {
                    b.labelMaterials.Add(mat);
                    Color baseCol = Color.white;
                    if (mat.HasProperty("_BaseColor")) baseCol = mat.GetColor("_BaseColor");
                    else if (mat.HasProperty("_Color")) baseCol = mat.GetColor("_Color");
                    b.labelBaseColors.Add(baseCol);
                }
            }

            CollectGraphics(b.label, b.uiGraphics, b.uiBaseColors, seen);
        }
    }

    void CollectGraphics(GameObject root, List<Graphic> outGfx, List<Color> outCols, HashSet<Graphic> seen)
    {
        if (root == null) return;
        var gfx = root.GetComponentsInChildren<Graphic>(true);
        foreach (var g in gfx)
        {
            if (g == null || seen.Contains(g)) continue;
            seen.Add(g);
            outGfx.Add(g);
            outCols.Add(g.color);
        }
    }

    // ---------- Button fade ----------
    void UpdateButtonFades()
    {
        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;

            if (!permanentReveal && b.revealTimer > 0f)
                b.revealTimer = Mathf.Max(0f, b.revealTimer - Time.deltaTime);

            float target;
            if (permanentReveal)
                target = b.revealed ? 1f : 0f;
            else
                target = b.revealTimer > 0f ? 1f : 0f;

            float step = fadeDuration > 0.0001f ? Time.deltaTime / fadeDuration : 1f;
            b.currentAlpha = Mathf.MoveTowards(b.currentAlpha, target, step);

            SetButtonAlpha(b, b.currentAlpha);
            SetUIFade(b, b.currentAlpha);

            bool visible = b.currentAlpha > 0.01f;
            SetInteractablesActive(b, visible);
        }
    }

    void SetUIFade(MenuButton b, float alpha)
    {
        alpha = Mathf.Clamp01(alpha);

        if (b.uiCanvasGroup != null)
        {
            b.uiCanvasGroup.alpha = alpha;
            b.uiCanvasGroup.interactable = alpha > 0.01f;
            b.uiCanvasGroup.blocksRaycasts = alpha > 0.01f;
        }
        if (b.labelCanvasGroup != null)
        {
            b.labelCanvasGroup.alpha = alpha;
        }

        for (int i = 0; i < b.uiGraphics.Count; i++)
        {
            var g = b.uiGraphics[i];
            if (g == null) continue;
            Color c = b.uiBaseColors[i];
            c.a *= alpha;
            g.color = c;
            if (!g.gameObject.activeSelf) g.gameObject.SetActive(true);
        }

        for (int i = 0; i < b.labelMaterials.Count; i++)
        {
            var mat = b.labelMaterials[i];
            if (mat == null) continue;
            Color c = b.labelBaseColors[i];
            c.a *= alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
        foreach (var r in b.labelRenderers)
        {
            if (r != null) r.enabled = alpha > 0.001f;
        }

        if (b.label != null)
        {
            bool show = alpha > 0.01f;
            if (b.label.activeSelf != show && b.labelCanvasGroup == null && b.labelRenderers.Count == 0)
                b.label.SetActive(show);
        }
    }

    void SetInteractablesActive(MenuButton b, bool active)
    {
        if (b.uiButton != null) b.uiButton.interactable = active;
        if (b.uiInputField != null) b.uiInputField.interactable = active;
    }

    void SetButtonAlpha(MenuButton b, float alpha)
    {
        if (b.renderers == null) return;

        foreach (var r in b.renderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat.HasProperty("_BaseColor"))
                {
                    Color c = mat.GetColor("_BaseColor");
                    c.a = alpha;
                    mat.SetColor("_BaseColor", c);
                }
                if (mat.HasProperty("_Color"))
                {
                    Color c = mat.GetColor("_Color");
                    c.a = alpha;
                    mat.SetColor("_Color", c);
                }
            }

            r.enabled = alpha > 0.001f;
        }
    }

    // ---------- Button clicks ----------
    void HandleButtonClicks()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, ~0, QueryTriggerInteraction.Ignore))
            return;

        foreach (var b in buttons)
        {
            if (b.cubeCollider == null || b.currentAlpha <= 0.1f) continue;

            if (hit.collider == b.cubeCollider || hit.collider.transform.IsChildOf(b.cubeVisual.transform))
            {
                if (b.uiButton != null && b.uiButton.interactable)
                {
                    b.uiButton.onClick.Invoke();
                }
                if (b.uiInputField != null && b.uiInputField.interactable)
                {
                    b.uiInputField.ActivateInputField();
                    b.uiInputField.Select();
                }
                return;
            }
        }
    }
}