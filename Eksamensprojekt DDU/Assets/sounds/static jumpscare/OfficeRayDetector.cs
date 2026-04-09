using UnityEngine;
using System.Collections;

public class OfficeRayDetector : MonoBehaviour
{
    public float rayDistance = 3f;
    public LayerMask detectionLayer; // optional filter

    public int requiredTriggers = 4;
    public float delayAfterLastTrigger = 4f;

    public GameObject objectToActivate;
    public AudioSource audioSource;

    private int triggerCount = 0;
    private bool hasActivated = false;
    private bool isCurrentlyOnOffice = false;

    void Update()
    {
        if (hasActivated) return;

        RaycastHit hit;

        // Shoot ray downward
        if (Physics.Raycast(transform.position, Vector3.down, out hit, rayDistance, detectionLayer))
        {
            if (hit.collider.CompareTag("Office"))
            {
                // Only count when first stepping onto it
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
    }

    // Optional: visualize the ray in Scene view
    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * rayDistance);
    }
}
