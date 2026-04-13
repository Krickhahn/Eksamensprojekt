using System.Collections;
using UnityEngine;

/// <summary>
/// Place on any GameObject in the NEXT scene.
/// Opens the eyelids and sweeps the EQ filter back up after the scene loads.
/// </summary>
public class SceneOpenEyes : MonoBehaviour
{
    [Header("═══ References")]
    [Tooltip("The FolderInteractionController from the previous scene. Leave null to auto-find.")]
    public FolderInteractionController folderController;

    [Header("═══ Timing")]
    [Tooltip("Delay before opening eyes after scene load (seconds).")]
    [Range(0f, 3f)] public float openDelay = 0.1f;

    private void Start()
    {
        if (folderController == null)
            folderController = FindObjectOfType<FolderInteractionController>();

        if (folderController != null)
            StartCoroutine(OpenAfterDelay());
        else
            Debug.LogWarning("[SceneOpenEyes] No FolderInteractionController found. Did you forget DontDestroyOnLoad on EyeBlinkCanvas?");
    }

    private IEnumerator OpenAfterDelay()
    {
        yield return new WaitForSeconds(openDelay);

        // Open eyelids and sweep EQ up simultaneously
        yield return StartCoroutine(folderController.OpenEyesWithEQ());

        // Clean up the persisted blink canvas
        EyeBlinkController blink = FindObjectOfType<EyeBlinkController>();
        if (blink != null) Destroy(blink.gameObject);

        Destroy(gameObject);
    }
}