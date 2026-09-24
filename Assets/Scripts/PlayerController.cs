using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 4.5f;
    public float sprintSpeed = 8f;
    public float crouchSpeed = 2.2f;
    public float acceleration = 12f;
    public float airControl = 0.25f;
    public float rotationSpeed = 15f;
    public float mouseSensitivity = 2f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.4f;
    public float gravity = -20f;
    public float fallMultiplier = 2.2f;
    public float lowJumpMultiplier = 2.5f;
    public float coyoteTime = 0.15f;
    public float jumpBufferTime = 0.12f;

    [Header("Ledge Climb / Vault")]
    [Tooltip("How far in front of the player to search for a climbable surface.")]
    public float ledgeCheckForwardDistance = 1.2f;
    [Tooltip("Height above the feet for the WALL ray (mid-body).")]
    public float wallCheckHeight = 0.9f;
    [Tooltip("Height above the feet for the LEDGE ray (near top of player).")]
    public float ledgeCheckHeight = 1.6f;
    [Tooltip("Height above the wall hit to look for a top surface.")]
    public float ledgeCheckUpHeight = 2.4f;
    [Tooltip("How far above the ledge top the player is placed when climbing.")]
    public float climbHeightOffset = 0.1f;
    [Tooltip("Forward distance moved onto the ledge after climbing.")]
    public float climbForwardOffset = 0.7f;
    [Tooltip("Max height of a surface the player can climb.")]
    public float maxClimbHeight = 3.0f;
    [Tooltip("Minimum height (below this it's just a step).")]
    public float minClimbHeight = 0.2f;
    [Tooltip("Duration of the climb animation/lerp.")]
    public float climbDuration = 0.35f;
    public KeyCode climbKey = KeyCode.Space;

    [Header("Ground Check (Raycast)")]
    public Transform groundCheckPoint;
    public float groundCheckDistance = 0.2f;
    public float groundCheckRadius = 0.28f;
    public bool debugGroundRay = true;
    public bool debugClimbRays = true;

    private bool isGrounded;
    private GameObject groundObject;
    private float lastGroundedTime;
    private float lastJumpPressedTime;

    [Header("References")]
    public Camera playerCamera;

    [Header("Script Buttons")]
    [Tooltip("Each entry: pick a key + drag in a script. When the key is pressed, that script runs.")]
    public List<ScriptButton> buttons = new List<ScriptButton>();

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 currentHorizontalVel;
    private float xRotation = 0f;

    // Climb state
    private bool isClimbing = false;
    private Vector3 climbStartPos;
    private Vector3 climbTargetPos;
    private float climbTimer = 0f;

    [Serializable]
    public class ScriptButton
    {
        public string label = "New Button";
        public KeyCode key = KeyCode.E;
        public MonoBehaviour targetScript;
        public bool onKeyDownOnly = true;
    }

    public interface IScriptTrigger
    {
        void Trigger();
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        HandleMouseLook();

        if (isClimbing)
        {
            UpdateClimb();
            HandleScriptButtons();
            return;
        }

        CheckGrounded();
        HandleMovement();

        // Climb gets first dibs on Space this frame.
        bool climbStarted = TryStartClimb();

        // Only jump if we did not just start a climb.
        if (!climbStarted)
            HandleJumpAndGravity();

        HandleScriptButtons();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    void CheckGrounded()
    {
        Vector3 origin = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        float dist = groundCheckDistance + 0.1f;

        isGrounded = false;
        groundObject = null;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            groundCheckRadius,
            Vector3.down,
            dist,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            isGrounded = true;
            groundObject = hit.collider.gameObject;
            break;
        }

        if (isGrounded)
            lastGroundedTime = Time.time;

        if (debugGroundRay)
        {
            Debug.DrawRay(origin, Vector3.down * dist, isGrounded ? Color.green : Color.red);
        }
    }

    void HandleMovement()
    {
        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = (transform.right * x + transform.forward * z);
        if (inputDir.sqrMagnitude > 1f) inputDir.Normalize();

        float targetSpeed = walkSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            targetSpeed = sprintSpeed;

        Vector3 targetVel = inputDir * targetSpeed;

        float accel = isGrounded ? acceleration : acceleration * airControl;
        currentHorizontalVel = Vector3.Lerp(
            currentHorizontalVel,
            targetVel,
            accel * Time.deltaTime
        );

        controller.Move(currentHorizontalVel * Time.deltaTime);
    }

    void HandleJumpAndGravity()
    {
        if (Input.GetButtonDown("Jump"))
            lastJumpPressedTime = Time.time;

        bool canJump = (Time.time - lastGroundedTime) <= coyoteTime;
        bool wantsJump = (Time.time - lastJumpPressedTime) <= jumpBufferTime;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        if (canJump && wantsJump)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastJumpPressedTime = -999f;
            lastGroundedTime = -999f;
        }

        float g = gravity;
        if (velocity.y < 0f)
            g *= fallMultiplier;
        else if (velocity.y > 0f && !Input.GetButton("Jump"))
            g *= lowJumpMultiplier;

        velocity.y += g * Time.deltaTime;
        velocity.y = Mathf.Max(velocity.y, -50f);

        controller.Move(new Vector3(0f, velocity.y, 0f) * Time.deltaTime);
    }

    // ---------- LEDGE CLIMB / VAULT ----------
    // Returns true if a climb started this frame.
    // Works on any object with a corner/edge whose top is within
    // [minClimbHeight, maxClimbHeight] of the player's feet, as long
    // as any part of the body front is facing it.
    bool TryStartClimb()
    {
        if (!Input.GetKeyDown(climbKey)) return false;

        Vector3 forward = transform.forward;
        float halfHeight = controller.height * 0.5f;
        Vector3 feetPos = transform.position - Vector3.up * halfHeight;

        // ---------------------------------------------------------
        // 1) SAMPLE THE BODY FRONT
        //    A grid of horizontal rays from feet to above head,
        //    spread across the player's width. This catches corners,
        //    thin walls, pillars, crates, etc.
        // ---------------------------------------------------------
        const int VERTICAL_SAMPLES = 7;
        const int HORIZONTAL_SAMPLES = 5;

        float halfWidth = Mathf.Max(controller.radius, 0.3f);
        float chestY = feetPos.y + halfHeight;

        RaycastHit bestHit = default;
        bool foundSurface = false;
        float bestScore = float.MaxValue;

        for (int v = 0; v < VERTICAL_SAMPLES; v++)
        {
            float t = v / (float)(VERTICAL_SAMPLES - 1);
            float sampleY = Mathf.Lerp(
                feetPos.y + 0.15f,
                feetPos.y + controller.height + 0.15f,
                t
            );

            for (int h = 0; h < HORIZONTAL_SAMPLES; h++)
            {
                float u = (HORIZONTAL_SAMPLES == 1)
                    ? 0f
                    : (h / (float)(HORIZONTAL_SAMPLES - 1)) * 2f - 1f;

                Vector3 lateral = transform.right * (u * halfWidth);
                Vector3 origin = new Vector3(
                    transform.position.x + lateral.x,
                    sampleY,
                    transform.position.z + lateral.z
                );

                if (debugClimbRays)
                    Debug.DrawRay(origin, forward * ledgeCheckForwardDistance, Color.yellow, 0.35f);

                if (Physics.Raycast(origin, forward, out RaycastHit h2,
                        ledgeCheckForwardDistance, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (h2.collider.transform.IsChildOf(transform)) continue;

                    // Prefer hits near chest height (natural climb pose).
                    float score = Mathf.Abs(sampleY - chestY);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestHit = h2;
                        foundSurface = true;
                    }
                }
            }
        }

        // Fallback: short ledges/steps that forward rays can miss.
        if (!foundSurface)
        {
            if (!TryDetectShortLedge(feetPos, forward, out bestHit))
                return false;
        }

        // ---------------------------------------------------------
        // 2) FIND THE TOP EDGE ABOVE THE HIT
        //    Fan of downward casts at increasing forward offsets.
        //    Handles irregular tops, chamfers, and thin corners.
        // ---------------------------------------------------------
        Vector3 topOriginBase = bestHit.point + Vector3.up * ledgeCheckUpHeight;
        float downDistance = ledgeCheckUpHeight + 1.0f;

        RaycastHit topHit = default;
        bool foundTop = false;

        float[] forwardOffsets = { -0.15f, 0f, 0.15f, 0.35f, 0.6f };
        foreach (float off in forwardOffsets)
        {
            Vector3 origin = topOriginBase + forward * off;

            if (debugClimbRays)
                Debug.DrawRay(origin, Vector3.down * downDistance, Color.cyan, 0.35f);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit th,
                    downDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (th.collider.transform.IsChildOf(transform)) continue;

                // Must be above the wall hit (not the floor).
                if (th.point.y <= bestHit.point.y + 0.02f) continue;

                // Must be walkable-ish (not a vertical face).
                if (Vector3.Angle(th.normal, Vector3.up) > 60f) continue;

                topHit = th;
                foundTop = true;
                break;
            }
        }

        if (!foundTop) return false;

        float ledgeHeight = topHit.point.y - feetPos.y;
        if (ledgeHeight < minClimbHeight || ledgeHeight > maxClimbHeight) return false;

        // ---------------------------------------------------------
        // 3) VERIFY LANDING CLEARANCE (capsule test)
        // ---------------------------------------------------------
        Vector3 landingFeetPos = topHit.point + forward * climbForwardOffset;
        Vector3 landingCenter = landingFeetPos + Vector3.up * (halfHeight + climbHeightOffset);

        if (!HasClearance(landingCenter))
        {
            bool ok = false;
            for (float extra = 0.3f; extra <= 1.0f; extra += 0.35f)
            {
                Vector3 tryCenter = topHit.point
                                    + forward * (climbForwardOffset + extra)
                                    + Vector3.up * (halfHeight + climbHeightOffset);

                if (HasClearance(tryCenter))
                {
                    landingCenter = tryCenter;
                    ok = true;
                    break;
                }
            }

            if (!ok)
            {
                if (debugClimbRays)
                    Debug.Log("[Climb] Landing area blocked.");
                return false;
            }
        }

        // ---------------------------------------------------------
        // 4) START CLIMB
        // ---------------------------------------------------------
        isClimbing = true;
        climbTimer = 0f;
        climbStartPos = transform.position;
        climbTargetPos = landingCenter;
        velocity = Vector3.zero;
        currentHorizontalVel = Vector3.zero;
        controller.enabled = false;

        if (debugClimbRays)
        {
            Debug.Log($"[PlayerController] Climbing '{topHit.collider.name}'. " +
                      $"Height = {ledgeHeight:F2}m");
            Debug.DrawLine(climbStartPos, climbTargetPos, Color.green, 1f);
        }

        return true;
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    /// <summary>
    /// True if the player capsule fits (no overlap) at the given center.
    /// </summary>
    bool HasClearance(Vector3 center)
    {
        float radius = controller.radius * 0.95f;
        float cylHeight = Mathf.Max(0f, controller.height - radius * 2f);

        Vector3 p1 = center + Vector3.up * (cylHeight * 0.5f);
        Vector3 p2 = center - Vector3.up * (cylHeight * 0.5f);

        bool wasEnabled = controller.enabled;
        controller.enabled = false;

        bool blocked = Physics.CheckCapsule(
            p1, p2, radius, ~0, QueryTriggerInteraction.Ignore);

        controller.enabled = wasEnabled;
        return !blocked;
    }

    /// <summary>
    /// Catches short ledges/steps the forward rays can miss.
    /// </summary>
    bool TryDetectShortLedge(Vector3 feetPos, Vector3 forward, out RaycastHit hit)
    {
        Vector3 origin = feetPos + Vector3.up * (controller.height * 0.15f);
        Vector3 dir = (forward + Vector3.down * 0.25f).normalized;

        if (Physics.Raycast(origin, dir, out hit,
                ledgeCheckForwardDistance * 1.2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) return false;
            return true;
        }

        hit = default;
        return false;
    }

    void UpdateClimb()
    {
        climbTimer += Time.deltaTime;
        float t = Mathf.Clamp01(climbTimer / climbDuration);

        float eased = t * t * (3f - 2f * t);

        Vector3 pos = Vector3.Lerp(climbStartPos, climbTargetPos, eased);
        pos.y += Mathf.Sin(eased * Mathf.PI) * 0.15f;

        transform.position = pos;

        if (t >= 1f)
        {
            isClimbing = false;
            controller.enabled = true;
            velocity.y = 0f;
        }
    }

    void HandleScriptButtons()
    {
        foreach (var btn in buttons)
        {
            if (btn.targetScript == null) continue;

            bool shouldFire = btn.onKeyDownOnly
                ? Input.GetKeyDown(btn.key)
                : Input.GetKey(btn.key);

            if (!shouldFire) continue;

            if (btn.targetScript is IScriptTrigger trigger)
            {
                trigger.Trigger();
            }
            else
            {
                Debug.LogWarning(
                    $"[PlayerController] '{btn.targetScript.name}' does not implement IScriptTrigger.");
            }
        }
    }
}