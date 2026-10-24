using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuEcholocation : MonoBehaviour
{
    [Header("Camera & Ground")]
    [Tooltip("Top-down camera used to raycast mouse onto the ground plane.")]
    public Camera menuCamera;
    [Tooltip("Layer mask for the ground plane. Mouse raycast only hits this.")]
    public LayerMask groundMask = ~0;
    [Tooltip("Max raycast distance from camera to ground.")]
    public float groundRayDistance = 500f;

    [Header("Echolocation")]
    [Tooltip("Speed the wave expands at (units/sec).")]
    public float waveSpeed = 50f;
    [Tooltip("Maximum radius of the wave (units).")]
    public float maxRadius = 500f;
    [Tooltip("How long a button stays revealed after the wave hits it (seconds). Only used when permanentReveal is false.")]
    public float revealDuration = 1.5f;
    [Tooltip("How long the button fades in / out (seconds).")]
    public float fadeDuration = 0.25f;
    [Tooltip("If true, buttons stay visible forever once the wave has hit them. If false, they fade out after revealDuration.")]
    public bool permanentReveal = false;
    [Tooltip("Optional visual prefab spawned at the click point. Leave null to use the built-in procedural ring.")]
    public GameObject waveVisualPrefab;
    [Tooltip("Optional: spawn a small marker at the click point.")]
    public GameObject clickMarkerPrefab;
    [Tooltip("How long the click marker is visible.")]
    public float clickMarkerLifetime = 0.5f;

    [Header("Sonar Ring Visual")]
    [Tooltip("Material for the expanding ring. Use a transparent unlit material (URP/Unlit or Standard with Transparent mode). If null, falls back to Sprites/Default.")]
    public Material sonarRingMaterial;
    [Tooltip("How thick the ring is (units).")]
    public float ringThickness = 0.5f;
    [Tooltip("Number of segments in the ring mesh. Higher = smoother.")]
    public int ringSegments = 64;
    [Tooltip("Color tint of the ring (multiplied with material color).")]
    public Color ringColor = new Color(0f, 1f, 1f, 1f);
    [Tooltip("How much the ring fades out as it approaches max radius (0 = no fade, 1 = fully fades).")]
    [Range(0f, 1f)] public float ringFade = 1f;

    [Header("Buttons")]
    [Tooltip("All cube buttons in the menu. Each one is revealed only when the wave reaches it.")]
    public List<MenuButton> buttons = new List<MenuButton>();

    [System.Serializable]
    public class MenuButton
    {
        [Tooltip("The cube visual (must have a Renderer). Its Collider is used for click detection.")]
        public GameObject cubeVisual;
        [Tooltip("The invisible UI Button on a Canvas that handles the actual click.")]
        public Button uiButton;
        [Tooltip("Optional: label shown when this button is revealed.")]
        public GameObject label;
        [HideInInspector] public float revealTimer = 0f;
        [HideInInspector] public float currentAlpha = 0f;
        [HideInInspector] public bool revealed = false;   // used when permanentReveal == true
        [HideInInspector] public Renderer[] renderers;
        [HideInInspector] public Collider cubeCollider;
        [HideInInspector] public Vector3 worldPosition;
    }

    // Internal wave state
    private bool waveActive = false;
    private Vector3 waveOrigin;
    private float waveRadius = 0f;
    private GameObject waveVisualInstance;
    private readonly HashSet<MenuButton> hitByCurrentWave = new HashSet<MenuButton>();

    // Procedural ring state
    private GameObject ringObject;
    private MeshFilter ringFilter;
    private MeshRenderer ringRenderer;
    private Mesh ringMesh;
    private Material ringMatInstance;
    private float ringAlpha = 0f;

    void Start()
    {
        if (menuCamera == null) menuCamera = Camera.main;

        // Cache renderer / collider per button and hide them
        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;

            b.renderers = b.cubeVisual.GetComponentsInChildren<Renderer>(true);
            b.cubeCollider = b.cubeVisual.GetComponentInChildren<Collider>();
            b.worldPosition = b.cubeVisual.transform.position;
            b.currentAlpha = 0f;
            b.revealTimer = 0f;
            b.revealed = false;

            SetButtonAlpha(b, 0f);

            // Disable the UI button until revealed
            if (b.uiButton != null) b.uiButton.interactable = false;
            if (b.label != null) b.label.SetActive(false);
        }

        CreateRingObject();

        // Cursor for menu
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

    // ---------- Input ----------
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Ignore clicks that landed directly on a UI element (so revealed buttons work)
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            // Raycast from camera through mouse into the ground plane
            Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                TriggerWave(hit.point);
            }
            else
            {
                // Fallback: project mouse onto a plane at menuCamera height
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
        waveOrigin = origin;
        waveRadius = 0f;
        waveActive = true;
        ringAlpha = 1f;

        // Clear per-wave hit tracking so this wave can arm each button exactly once
        hitByCurrentWave.Clear();

        // Optional visual prefab (only used if assigned)
        if (waveVisualPrefab != null)
        {
            if (waveVisualInstance != null) Destroy(waveVisualInstance);
            waveVisualInstance = Instantiate(waveVisualPrefab, origin, Quaternion.identity);
        }

        // Optional click marker
        if (clickMarkerPrefab != null)
        {
            GameObject marker = Instantiate(clickMarkerPrefab, origin, Quaternion.identity);
            Destroy(marker, clickMarkerLifetime);
        }

        // Force the button at the click point to reveal instantly
        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;
            float d = Vector3.Distance(b.worldPosition, origin);
            if (d < 2f) // very close - reveal immediately
            {
                RevealButton(b);
            }
        }
    }

    void UpdateWave()
    {
        if (!waveActive) return;

        waveRadius += waveSpeed * Time.deltaTime;

        // Check each button: reveal it once per wave when the wave reaches it
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

        // Update the procedural ring
        BuildRingMesh(waveRadius, ringThickness);

        // Fade: alpha goes 1 -> (1 - ringFade) over the wave lifetime
        float t = Mathf.Clamp01(waveRadius / maxRadius);
        ringAlpha = 1f - t * ringFade;
        ApplyRingAlpha(ringAlpha);

        // Wave lifetime = time to reach maxRadius
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

    // Reveal a button exactly once for the current wave.
    void RevealButton(MenuButton b)
    {
        if (hitByCurrentWave.Contains(b)) return;
        hitByCurrentWave.Add(b);

        if (permanentReveal)
            b.revealed = true;
        else
            b.revealTimer = revealDuration;
    }

    // ---------- Procedural ring ----------
    void CreateRingObject()
    {
        ringObject = new GameObject("SonarRing");
        ringObject.transform.SetParent(transform, false);

        ringFilter = ringObject.AddComponent<MeshFilter>();
        ringRenderer = ringObject.AddComponent<MeshRenderer>();

        // Build a material instance
        Shader fallback = Shader.Find("Sprites/Default");
        Material baseMat = sonarRingMaterial;
        if (baseMat != null)
            ringMatInstance = new Material(baseMat);
        else if (fallback != null)
            ringMatInstance = new Material(fallback);
        else
            ringMatInstance = new Material(Shader.Find("Unlit/Color"));

        // Apply tint
        if (ringMatInstance.HasProperty("_Color"))
            ringMatInstance.SetColor("_Color", ringColor);
        if (ringMatInstance.HasProperty("_BaseColor"))
            ringMatInstance.SetColor("_BaseColor", ringColor);

        // Make sure it renders on top of the ground and doesn't write depth
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

            verts[i * 2 + 0] = new Vector3(cos * inner, 0f, sin * inner); // inner
            verts[i * 2 + 1] = new Vector3(cos * outer, 0f, sin * outer); // outer
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

        // Lay flat just above the ground at the wave origin
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

    // ---------- Button fade ----------
    void UpdateButtonFades()
    {
        foreach (var b in buttons)
        {
            if (b.cubeVisual == null) continue;

            // Count down first (only matters when not permanent)
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

            bool visible = b.currentAlpha > 0.01f;
            if (b.uiButton != null) b.uiButton.interactable = visible;
            if (b.label != null && b.label.activeSelf != visible)
                b.label.SetActive(visible);
        }
    }

    void SetButtonAlpha(MenuButton b, float alpha)
    {
        if (b.renderers == null) return;

        foreach (var r in b.renderers)
        {
            if (r == null) continue;
            // If you're using an SRP / URP, use "_BaseColor"; Built-in uses "_Color"
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

            // Optional: hide the whole renderer when fully transparent to save perf
            r.enabled = alpha > 0.001f;
        }
    }

    // ---------- Button clicks ----------
    void HandleButtonClicks()
    {
        // Only revealed buttons should be clickable
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = menuCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, groundRayDistance, ~0, QueryTriggerInteraction.Ignore))
            return;

        foreach (var b in buttons)
        {
            if (b.cubeCollider == null || b.currentAlpha <= 0.1f) continue;

            if (hit.collider == b.cubeCollider || hit.collider.transform.IsChildOf(b.cubeVisual.transform))
            {
                // Click! The UI button's onClick events will fire through UI raycast if the Canvas has a raycaster,
                // but for 3D cubes we should invoke the button programmatically.
                if (b.uiButton != null && b.uiButton.interactable)
                {
                    b.uiButton.onClick.Invoke();
                }
                return;
            }
        }
    }
}