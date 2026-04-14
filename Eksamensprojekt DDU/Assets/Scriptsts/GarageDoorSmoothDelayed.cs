using UnityEngine;

public class GarageDoorSmoothDelayed : MonoBehaviour
{
    [Header("Door Settings")]
    public Transform door;
    public Vector3 closedPosition;
    public Vector3 openPosition;

    [Header("Movement")]
    public float moveSpeed = 10f;

    [Header("Trigger")]
    public BoxCollider triggerZone; // 👈 assign in Inspector

    [Header("Timing")]
    public float stayOpenTime = 3f;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip openSound;
    public AudioClip closeSound;

    [Range(0f, 1f)]
    public float volume = 1f;

    private bool isOpen = false;
    private Vector3 targetPosition;
    private float closeTimer = 0f;
    private bool playerInRange = false;

    void Start()
    {
        if (door != null)
        {
            door.localPosition = closedPosition;
            targetPosition = closedPosition;
        }

        UpdateVolume();
    }

    void Update()
    {
        if (door == null) return;

        UpdateVolume();

        if (!playerInRange && isOpen)
        {
            closeTimer -= Time.deltaTime;

            if (closeTimer <= 0f)
                CloseDoor();
        }

        door.localPosition = Vector3.MoveTowards(
            door.localPosition,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
            closeTimer = stayOpenTime;

            if (!isOpen)
                OpenDoor();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }

    void UpdateVolume()
    {
        if (audioSource != null)
            audioSource.volume = volume;
    }

    void OnValidate()
    {
        if (audioSource != null)
            audioSource.volume = volume;
    }

    void OpenDoor()
    {
        isOpen = true;
        targetPosition = openPosition;

        if (audioSource != null && openSound != null)
            audioSource.PlayOneShot(openSound, volume);
    }

    void CloseDoor()
    {
        isOpen = false;
        targetPosition = closedPosition;

        if (audioSource != null && closeSound != null)
            audioSource.PlayOneShot(closeSound, volume);
    }
}