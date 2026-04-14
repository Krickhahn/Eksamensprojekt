using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class AmbienceLayer
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

[System.Serializable]
public class TagAmbience
{
    public string tag;
    public AmbienceLayer[] layers;
}

[RequireComponent(typeof(AudioSource))]
public class AmbienceByTagSystem : MonoBehaviour
{
    [Header("Ambience pr tag")]
    public TagAmbience[] tagAmbiences;

    [Header("Settings")]
    public float fadeTime = 2f;
    public float checkDistance = 2f;

    private Dictionary<AudioClip, AudioSource> audioSources = new Dictionary<AudioClip, AudioSource>();
    private string currentTag = "";

    void Start()
    {
        // Opret AudioSources for ALLE clips
        foreach (var tagAmb in tagAmbiences)
        {
            foreach (var layer in tagAmb.layers)
            {
                if (layer.clip == null || audioSources.ContainsKey(layer.clip)) continue;

                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = layer.clip;
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;

                audioSources.Add(layer.clip, source);
            }
        }
    }

    void Update()
    {
        string detectedTag = DetectFloorTag();

        if (detectedTag != currentTag)
        {
            currentTag = detectedTag;
            ApplyAmbience(currentTag);
        }

        HandleFading();
    }

    string DetectFloorTag()
    {
        Ray ray = new Ray(transform.position, Vector3.down);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, checkDistance))
        {
            return hit.collider.tag;
        }

        return "";
    }

    Dictionary<AudioClip, float> targetVolumes = new Dictionary<AudioClip, float>();

    void ApplyAmbience(string tag)
    {
        targetVolumes.Clear();

        foreach (var tagAmb in tagAmbiences)
        {
            if (tagAmb.tag != tag) continue;

            foreach (var layer in tagAmb.layers)
            {
                targetVolumes[layer.clip] = layer.volume;

                if (!audioSources[layer.clip].isPlaying)
                    audioSources[layer.clip].Play();
            }
        }
    }

    void HandleFading()
    {
        foreach (var pair in audioSources)
        {
            AudioClip clip = pair.Key;
            AudioSource source = pair.Value;

            float target = targetVolumes.ContainsKey(clip) ? targetVolumes[clip] : 0f;

            source.volume = Mathf.MoveTowards(
                source.volume,
                target,
                Time.deltaTime * (1f / fadeTime)
            );

            if (target == 0f && source.volume <= 0.001f && source.isPlaying)
            {
                source.Stop();
            }
        }
    }
}