using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom/Pixel Art Effect")]
public class PixelArtVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Header("Pixelering")]
    [Tooltip("Størrelsen på hver pixel. Højere = mere pixeleret.")]
    public ClampedFloatParameter pixelSize = new ClampedFloatParameter(1f, 1f, 16f);

    [Tooltip("Reducerer farvepaletten. 0 = fuld kvalitet, 1 = 8 farver.")]
    public ClampedFloatParameter paletteStrength = new ClampedFloatParameter(0f, 0f, 1f);

    // IPostProcessComponent: fortæller URP om denne komponent er aktiv
    public bool IsActive() => pixelSize.value > 1f || paletteStrength.value > 0f;
    public bool IsTileCompatible() => false;
}