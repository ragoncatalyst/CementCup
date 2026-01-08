using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRen : MonoBehaviour
{
    [Header("References")]
    [Tooltip("如果为空则通过名称查找玩家根节点。")]
    [SerializeField] private Transform playerRoot;
    [Tooltip("当未指定 Player Root 时，用于 GameObject.Find 的名称。")]
    [SerializeField] private string playerRootName = "PlayerEmpty";
    [Tooltip("渲染 Quad 的 Transform，可手动指定。如果为空则在 Player Root 下按名称查找。")]
    [SerializeField] private Transform renderSquareOverride;
    [SerializeField] private string renderSquareName = "RenderSquare";

    private Transform renderSquare;
    private MeshRenderer meshRenderer;
    private Animator animator;
    private Rigidbody playerRigidbody;
    private Vector3 initialScale;
    private bool lastFlip = false;
    [Tooltip("If true, invert billboard facing by 180° (useful when the quad's front side is opposite of Transform.forward).")]
    [SerializeField] private bool invertFacing = true;
    [Tooltip("Threshold of horizontal velocity to trigger flip (world X)")]
    public float flipThreshold = 0.05f;
    [Tooltip("If true, flip direction is inverted (useful if sprite art faces left by default).")]
    public bool invertFlip = true;
    [Tooltip("Default flip state when the special animation is not playing")]
    public bool defaultFlip = false;
    public enum BillboardMode { YOnly, FaceCamera }
    [Tooltip("YOnly: only rotate around Y to face camera (upright). FaceCamera: fully rotate to face camera on all axes")]
    public BillboardMode billboardMode = BillboardMode.YOnly;

    void Start()
    {
        if (playerRoot == null && !string.IsNullOrEmpty(playerRootName))
        {
            var found = GameObject.Find(playerRootName);
            if (found != null) playerRoot = found.transform;
        }

        if (playerRoot == null)
        {
            Debug.LogError($"PlayerRen: 找不到名为 {playerRootName} 的物体，请确认场景层级。");
            enabled = false;
            return;
        }

        renderSquare = renderSquareOverride != null ? renderSquareOverride : playerRoot.Find(renderSquareName);
        if (renderSquare == null)
        {
            Debug.LogError($"PlayerRen: 在 {playerRoot.name} 下找不到子物体 {renderSquareName}，请检查层级。");
            enabled = false;
            return;
        }

        meshRenderer = renderSquare.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            Debug.LogError("PlayerRen: RenderSquare 上需要有 MeshRenderer。请添加后重试。");
            enabled = false;
            return;
        }

        meshRenderer.enabled = true;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        meshRenderer.receiveShadows = true;

        initialScale = renderSquare.localScale;
        ApplyFlip(defaultFlip, true);

        // cache animator and player's rigidbody if present
        animator = playerRoot.GetComponentInChildren<Animator>();
        playerRigidbody = playerRoot.GetComponent<Rigidbody>();
    }

    void Update()
    {
        // 广告牌行为：RenderSquare 实时面向 Camera.main（完整朝向），但绝不修改父物体 Transform
        if (renderSquare == null) return;
        Camera cam = Camera.main;
        if (cam == null) return;

        ApplyBillboard(cam);

        // --- Animation flip logic ---
        if (meshRenderer != null)
        {
            bool handled = false;
            if (animator != null)
            {
                var clips = animator.GetCurrentAnimatorClipInfo(0);
                if (clips != null && clips.Length > 0 && clips[0].clip != null)
                {
                    string clipName = clips[0].clip.name;
                    if (clipName == "PlayerIdle_BottomRight")
                    {

                        // Determine camera-relative horizontal movement
                        float camDot = 0f;
                        Vector3 camRight = cam.transform.right;
                        camRight.y = 0f; camRight.Normalize();
                        if (playerRigidbody != null)
                        {
                            Vector3 horVel = playerRigidbody.velocity;
                            horVel.y = 0f;
                            camDot = Vector3.Dot(horVel, camRight);
                        }
                        else
                        {
                            float inH = Input.GetAxisRaw("Horizontal");
                            float inV = Input.GetAxisRaw("Vertical");
                            Vector3 camForward = cam.transform.forward;
                            camForward.y = 0f; camForward.Normalize();
                            Vector3 inputVec = camRight * inH + camForward * inV;
                            camDot = Vector3.Dot(inputVec, camRight);
                        }

                        bool flip = lastFlip;
                        // If invertFlip==true then moving to camera-right should set flip=true; else flip=false
                        bool flipWhenMovingRight = invertFlip;
                        if (camDot > flipThreshold) flip = flipWhenMovingRight;
                        else if (camDot < -flipThreshold) flip = !flipWhenMovingRight;

                        ApplyFlip(flip);

                        handled = true;
                    }
                }
            }

            // when not handled by the special animation, ensure default flip is enforced
            if (!handled)
            {
                ApplyFlip(defaultFlip);
            }
        }
    }

    private void ApplyBillboard(Camera cam)
    {
        Transform quadTransform = renderSquare;
        Vector3 toCam = cam.transform.position - quadTransform.position;
        if (toCam.sqrMagnitude < 1e-8f)
        {
            return;
        }

        Quaternion worldRot;
        if (billboardMode == BillboardMode.YOnly)
        {
            toCam.y = 0f;
            if (toCam.sqrMagnitude < 1e-8f)
            {
                toCam = cam.transform.forward;
                toCam.y = 0f;
            }
            Vector3 dir = invertFacing ? -toCam : toCam;
            worldRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }
        else
        {
            Vector3 dir = invertFacing ? -toCam : toCam;
            worldRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        if (quadTransform.parent != null)
        {
            quadTransform.localRotation = Quaternion.Inverse(quadTransform.parent.rotation) * worldRot;
        }
        else
        {
            quadTransform.rotation = worldRot;
        }
    }

    private void ApplyFlip(bool flip, bool force = false)
    {
        if (renderSquare == null) return;
        if (!force && flip == lastFlip) return;
        Vector3 targetScale = initialScale;
        targetScale.x = initialScale.x * (flip ? -1f : 1f);
        renderSquare.localScale = targetScale;
        lastFlip = flip;
    }
}
