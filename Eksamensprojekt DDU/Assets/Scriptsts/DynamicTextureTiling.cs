using UnityEngine;

/// <summary>
/// DynamicTextureTiling
/// Attach to any GameObject with a Renderer (MeshRenderer, SpriteRenderer, etc.)
/// Supports per-texture-channel tiling, scrolling, animation, and runtime overrides.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class DynamicTextureTiling : MonoBehaviour
{
    // ─── Enums ────────────────────────────────────────────────────────────────

    public enum TilingMode
    {
        /// Uniform scale based on world-space object size
        WorldSpace,
        /// Fixed UV tile count regardless of scale
        Fixed,
        /// Driven by a custom curve over time
        Animated
    }

    public enum ScrollAxis { None, X, Y, Both }

    // ─── Inspector ────────────────────────────────────────────────────────────

    [Header("Target")]
    [Tooltip("Leave empty to use the first material on this object.")]
    public string targetMaterialName = "";
    [Tooltip("Shader property name for the main texture tiling (usually _MainTex).")]
    public string tilingProperty = "_MainTex";

    [Header("Tiling Mode")]
    public TilingMode mode = TilingMode.Fixed;

    [Header("Fixed / World-Space Tiling")]
    [Tooltip("Base tile count (Fixed mode) or texels-per-world-unit (WorldSpace mode).")]
    public Vector2 baseTiling = Vector2.one;
    [Tooltip("UV offset.")]
    public Vector2 baseOffset = Vector2.zero;

    [Header("World-Space Settings")]
    [Tooltip("Which axes drive the world-space tiling calculation.")]
    public bool worldSpaceX = true;
    public bool worldSpaceY = true;
    [Tooltip("Use Z scale for tiling Y — useful for vertical walls scaled on X/Z rather than X/Y.")]
    public bool useZForTilingY = false;

    [Header("Animated Tiling")]
    [Tooltip("Tiling X driven by this curve (time in seconds → tile count).")]
    public AnimationCurve tilingCurveX = AnimationCurve.Linear(0, 1, 10, 4);
    [Tooltip("Tiling Y driven by this curve (time in seconds → tile count).")]
    public AnimationCurve tilingCurveY = AnimationCurve.Linear(0, 1, 10, 4);
    [Tooltip("Duration of one animation loop (seconds). 0 = don't loop.")]
    public float animationDuration = 10f;

    [Header("UV Scrolling")]
    public ScrollAxis scrollAxis = ScrollAxis.None;
    [Tooltip("Scroll speed in UV units per second.")]
    public Vector2 scrollSpeed = new Vector2(0.1f, 0f);

    [Header("Secondary Texture Channels")]
    [Tooltip("Additional texture channels to tile/scroll independently.")]
    public TextureChannel[] extraChannels = new TextureChannel[0];

    [Header("Runtime Options")]
    [Tooltip("Apply changes to a MaterialPropertyBlock instead of the shared material (no material duplication).")]
    public bool useMaterialPropertyBlock = true;
    [Tooltip("Recalculate every frame even in Edit mode.")]
    public bool updateInEditMode = true;

    // ─── Nested Types ─────────────────────────────────────────────────────────

    [System.Serializable]
    public class TextureChannel
    {
        public string shaderProperty = "_BumpMap";
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public ScrollAxis scrollAxis = ScrollAxis.None;
        public Vector2 scrollSpeed = new Vector2(0.05f, 0f);
    }

    // ─── Private State ────────────────────────────────────────────────────────

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;
    private Material _targetMaterial;
    private int _targetMaterialIndex = 0;
    private float _animTime;
    private Vector2 _scrollOffset;

    // Cached shader IDs for performance
    private int _tilingPropertyID;
    private int _stPropertyID; // _BaseMap_ST or _MainTex_ST — works with MaterialPropertyBlock

    // ─── Unity Lifecycle ──────────────────────────────────────────────────────

    void OnEnable()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
        _tilingPropertyID = Shader.PropertyToID(tilingProperty);
        _stPropertyID = Shader.PropertyToID(tilingProperty + "_ST");
        ResolveTargetMaterial();
    }

    void Update()
    {
        if (!Application.isPlaying && !updateInEditMode) return;

        float dt = Application.isPlaying ? Time.deltaTime : 0f;

        UpdateAnimationTime(dt);
        UpdateScrollOffset(dt);
        ApplyTiling();
    }

    // ─── Core Logic ───────────────────────────────────────────────────────────

    /// Finds the correct material index based on targetMaterialName.
    void ResolveTargetMaterial()
    {
        if (_renderer == null) return;

        Material[] mats = _renderer.sharedMaterials;

        if (string.IsNullOrEmpty(targetMaterialName))
        {
            _targetMaterialIndex = 0;
            _targetMaterial = mats.Length > 0 ? mats[0] : null;
            return;
        }

        for (int i = 0; i < mats.Length; i++)
        {
            if (mats[i] != null && mats[i].name.Contains(targetMaterialName))
            {
                _targetMaterialIndex = i;
                _targetMaterial = mats[i];
                return;
            }
        }

        Debug.LogWarning($"[DynamicTextureTiling] Material '{targetMaterialName}' not found on {name}.");
    }

    void UpdateAnimationTime(float dt)
    {
        if (mode != TilingMode.Animated) return;

        _animTime += dt;
        if (animationDuration > 0f && _animTime > animationDuration)
            _animTime -= animationDuration;
    }

    void UpdateScrollOffset(float dt)
    {
        if (scrollAxis == ScrollAxis.None) return;

        switch (scrollAxis)
        {
            case ScrollAxis.X: _scrollOffset.x += scrollSpeed.x * dt; break;
            case ScrollAxis.Y: _scrollOffset.y += scrollSpeed.y * dt; break;
            case ScrollAxis.Both: _scrollOffset += scrollSpeed * dt; break;
        }

        // Keep offset in [0,1) to avoid floating-point drift over time
        _scrollOffset.x = Mathf.Repeat(_scrollOffset.x, 1f);
        _scrollOffset.y = Mathf.Repeat(_scrollOffset.y, 1f);
    }

    void ApplyTiling()
    {
        if (_renderer == null || _targetMaterial == null) return;

        Vector2 tiling = CalculateTiling();
        Vector2 offset = baseOffset + _scrollOffset;

        // The _ST vector packs tiling into XY and offset into ZW.
        // This is the only reliable way to drive texture tiling via MaterialPropertyBlock
        // across both Built-in and URP shaders (_MainTex_ST / _BaseMap_ST).
        var st = new Vector4(tiling.x, tiling.y, offset.x, offset.y);

        if (useMaterialPropertyBlock)
        {
            _renderer.GetPropertyBlock(_propBlock, _targetMaterialIndex);
            _propBlock.SetVector(_stPropertyID, st);
            _renderer.SetPropertyBlock(_propBlock, _targetMaterialIndex);
        }
        else
        {
            _targetMaterial.SetTextureScale(tilingProperty, tiling);
            _targetMaterial.SetTextureOffset(tilingProperty, offset);
        }

        ApplyExtraChannels();
    }

    Vector2 CalculateTiling()
    {
        switch (mode)
        {
            case TilingMode.WorldSpace:
                return CalculateWorldSpaceTiling();

            case TilingMode.Animated:
                return new Vector2(
                    tilingCurveX.Evaluate(_animTime),
                    tilingCurveY.Evaluate(_animTime)
                );

            default: // Fixed
                return baseTiling;
        }
    }

    Vector2 CalculateWorldSpaceTiling()
    {
        Vector3 s = transform.lossyScale;
        float x = worldSpaceX ? Mathf.Max(Mathf.Abs(s.x), 0.001f) * baseTiling.x : baseTiling.x;
        float yScale = useZForTilingY ? Mathf.Abs(s.z) : Mathf.Abs(s.y);
        float y = worldSpaceY ? Mathf.Max(yScale, 0.001f) * baseTiling.y : baseTiling.y;
        return new Vector2(x, y);
    }

    void ApplyExtraChannels()
    {
        if (extraChannels == null || extraChannels.Length == 0) return;

        if (useMaterialPropertyBlock)
            _renderer.GetPropertyBlock(_propBlock, _targetMaterialIndex);

        foreach (var ch in extraChannels)
        {
            if (string.IsNullOrEmpty(ch.shaderProperty)) continue;

            Vector2 chOffset = ch.offset;
            switch (ch.scrollAxis)
            {
                case ScrollAxis.X: chOffset.x += ch.scrollSpeed.x * _animTime; break;
                case ScrollAxis.Y: chOffset.y += ch.scrollSpeed.y * _animTime; break;
                case ScrollAxis.Both: chOffset += ch.scrollSpeed * _animTime; break;
            }
            chOffset.x = Mathf.Repeat(chOffset.x, 1f);
            chOffset.y = Mathf.Repeat(chOffset.y, 1f);

            if (useMaterialPropertyBlock)
            {
                int id = Shader.PropertyToID(ch.shaderProperty + "_ST");
                _propBlock.SetVector(id, new Vector4(ch.tiling.x, ch.tiling.y, chOffset.x, chOffset.y));
            }
            else
            {
                _targetMaterial.SetTextureScale(ch.shaderProperty, ch.tiling);
                _targetMaterial.SetTextureOffset(ch.shaderProperty, chOffset);
            }
        }

        if (useMaterialPropertyBlock)
            _renderer.SetPropertyBlock(_propBlock, _targetMaterialIndex);
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    /// Override tiling at runtime without changing inspector values.
    public void SetTiling(Vector2 tiling)
    {
        baseTiling = tiling;
        ApplyTiling();
    }

    /// Override offset at runtime.
    public void SetOffset(Vector2 offset)
    {
        baseOffset = offset;
        ApplyTiling();
    }

    /// Instantly snap the scroll back to zero.
    public void ResetScroll()
    {
        _scrollOffset = Vector2.zero;
        ApplyTiling();
    }

    /// Restart the animation timer.
    public void ResetAnimation()
    {
        _animTime = 0f;
    }

    /// Smoothly lerp to a target tiling over time (call from a Coroutine).
    public System.Collections.IEnumerator LerpTilingTo(Vector2 target, float duration)
    {
        Vector2 start = baseTiling;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            baseTiling = Vector2.Lerp(start, target, elapsed / duration);
            ApplyTiling();
            yield return null;
        }
        baseTiling = target;
        ApplyTiling();
    }
}