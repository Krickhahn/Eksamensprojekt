using UnityEngine;
using System.Collections;
using UnityEngine.Video; // <-- ADD THIS

public class OfficeRayDetector : MonoBehaviour
{
    public float rayDistance = 3f;
    public LayerMask detectionLayer;

    public int requiredTriggers = 4;
    public float delayAfterLastTrigger = 4f;

    public GameObject objectToActivate;
    public AudioSource audioSource;
    public VideoPlayer videoPlayer; // <-- ADD THIS

    private int triggerCount = 0;
    private bool hasActivated = false;
    private bool isCurrentlyOnOffice = false;

    void Update()
    {
        if (hasActivated) return;

        RaycastHit hit;

        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, detectionLayer))
        {
            if (hit.collider.CompareTag("Office"))
            {
                if (!isCurrentlyOnOffice)
                {
                    isCurrentlyOnOffice = true;
                    triggerCount++;

                    Debug.Log("Office visits: " + triggerCount);

                    if (triggerCount >= requiredTriggers)
                    {
                        hasActivated = true;
                        StartCoroutine(ActivateAfterDelay());
                    }
                }
            }
            else
            {
                isCurrentlyOnOffice = false;
            }
        }
        else
        {
            isCurrentlyOnOffice = false;
        }
    }

    IEnumerator ActivateAfterDelay()
    {
        yield return new WaitForSeconds(delayAfterLastTrigger);

        if (objectToActivate != null)
        {
            objectToActivate.SetActive(true);
        }

        if (audioSource != null)
        {
            audioSource.Play();
        }

        if (videoPlayer != null) // <-- ADD THIS BLOCK
        {
            videoPlayer.Play();
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDistance);
    }
}