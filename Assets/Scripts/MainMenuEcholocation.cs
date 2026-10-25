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

        // === CHANGED: TMP_InputField replaced with a plain UI Text reference ===
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

            SetButtonAlpha(b, 0f);

            SetInteractablesActive(b, false);
            if (b.label != null) b.label.SetActive(false);
        }

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
        waveOrigin = origin;
        waveRadius = 0f;
        waveActive = true;
        ringAlpha = 1f;

        hitByCurrentWave.Clear();

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

            bool visible = b.currentAlpha > 0.01f;
            SetInteractablesActive(b, visible);
            if (b.label != null && b.label.activeSelf != visible)
                b.label.SetActive(visible);
        }
    }

    // === MODIFIED: now handles InputField + Text ===
    void SetInteractablesActive(MenuButton b, bool active)
    {
        if (b.uiButton != null) b.uiButton.interactable = active;
        if (b.uiInputField != null) b.uiInputField.interactable = active;
        // Text isn't interactable; toggle its visibility instead
        if (b.inputText != null && b.inputText.gameObject.activeSelf != active)
            b.inputText.gameObject.SetActive(active);
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
                // === MODIFIED: focus the InputField so the player can type ===
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