using System.Collections;
using UnityEngine;

/// <summary>
/// Place this on any GameObject in the NEXT scene.
/// When the scene loads, it finds the EyeBlinkController that was carried
/// over (via DontDestroyOnLoad on EyeBlinkCanvas) and opens the eyes,
/// creating a seamless transition from the previous scene's closed-eye state.
///
/// Setup:
///   1. On your EyeBlinkCanvas in the FIRST scene, add a
///      "DontDestroyOnLoad" component (or add the line to EyeBlinkController).
///   2. Place this script on any GameObject in the NEXT scene.
///   3. Optionally assign the eyeBlink reference directly if you prefer
///      not to use FindObjectOfType.
/// </summary>
public class SceneOpenEyes : MonoBehaviour
{
    [Header("═══ References")]
    [Tooltip("The EyeBlinkController from the previous scene. Leave null to auto-find via DontDestroyOnLoad.")]
    public EyeBlinkController eyeBlink;

    [Header("═══ Timing")]
    [Tooltip("Delay before opening eyes after scene load (seconds). Gives the scene a moment to finish rendering).")]
    [Range(0f, 3f)] public float openDelay = 0.1f;

    private void Start()
    {
        if (eyeBlink == null)
            eyeBlink = FindObjectOfType<EyeBlinkController>();

        if (eyeBlink != null)
            StartCoroutine(OpenAfterDelay());
        else
            Debug.LogWarning("[SceneOpenEyes] No EyeBlinkController found. Did you forget DontDestroyOnLoad on EyeBlinkCanvas?");
    }

    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSeconds(openDelay);
        yield return StartCoroutine(eyeBlink.OpenEye());

        // Optionally destroy the blink canvas now that we're done
        Destroy(eyeBlink.gameObject);
    }
}
