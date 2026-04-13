using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbienceAudio : MonoBehaviour
{
    [Header("Player reference")]
    public Transform player;
    public float raycastDistance = 2f;

    [Header("Looping Ambience (spiller konstant)")]
    public AmbienceElement[] loopingAmbience;

    [Header("Random Ambience (spiller af og til)")]
    public AmbienceElement[] randomAmbience;
    public Vector2 randomDelayRange = new Vector2(5f, 15f);

    private AudioSource loopSource;
    private AudioSource randomSource;

    private string currentGroundTag = "";

    void Awake()
    {
        loopSource = gameObject.AddComponent<AudioSource>();
        loopSource.loop = true;

        randomSource = gameObject.AddComponent<AudioSource>();
        randomSource.loop = false;
        randomSource.playOnAwake = false;
    }

    void Start()
    {
        PlayLoopingAmbience();
        StartCoroutine(RandomAmbienceRoutine());
    }

    void Update()
    {
        UpdateGroundTag();
    }

    // --- Tjek hvilket tag spilleren står på ---
    void UpdateGroundTag()
    {
        RaycastHit hit;

        if (Physics.Raycast(player.position, Vector3.down, out hit, raycastDistance))
        {
            currentGroundTag = hit.collider.tag;
        }
        else
        {
            currentGroundTag = "";
        }
    }

    // --- Vælg klip baseret på vægt + tag ---
    AmbienceElement ChooseWeighted(AmbienceElement[] elements, bool requireTagCheck)
    {
        float totalWeight = 0f;

        foreach (var elem in elements)
        {
            if (requireTagCheck && !string.IsNullOrEmpty(elem.requiredTag))
            {
                if (elem.requiredTag != currentGroundTag)
                    continue;
            }

            totalWeight += elem.weight;
        }

        if (totalWeight <= 0f) return null;

        float r = Random.Range(0, totalWeight);

        foreach (var elem in elements)
        {
            if (requireTagCheck && !string.IsNullOrEmpty(elem.requiredTag))
            {
                if (elem.requiredTag != currentGroundTag)
                    continue;
            }

            if (r < elem.weight)
                return elem;

            r -= elem.weight;
        }

        return null;
    }

    // --- Loopende ambience ---
    void PlayLoopingAmbience()
    {
        var elem = ChooseWeighted(loopingAmbience, true);

        if (elem == null)
        {
            loopSource.Stop();
            return;
        }

        loopSource.clip = elem.clip;
        loopSource.volume = elem.volume;
        loopSource.Play();
    }

    // --- Random ambience ---
    System.Collections.IEnumerator RandomAmbienceRoutine()
    {
        while (true)
        {
            var elem = ChooseWeighted(randomAmbience, true);

            if (elem != null && elem.clip != null)
            {
                randomSource.PlayOneShot(elem.clip, elem.volume);
            }

            float delay = Random.Range(randomDelayRange.x, randomDelayRange.y);
            yield return new WaitForSeconds(delay);
        }
    }
    [System.Serializable]
    public class AmbienceElement
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0f, 1f)] public float weight = 1f;

        [Header("Optional tag requirement")]
        public string requiredTag;
        // tom = ingen tag-krav
    }
}