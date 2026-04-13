using UnityEngine;
using System.Collections;

[RequireComponent(typeof(AudioSource))]
public class AudioLightning : MonoBehaviour
{
    public Light lightningLight;

    [Header("Audio Settings")]
    public float threshold = 0.3f;   // Peak detection threshold
    public int sampleSize = 1024;

    [Header("Lightning Settings")]
    public float minFlashDuration = 0.05f;
    public float maxFlashDuration = 0.2f;
    public float minIntensity = 2f;
    public float maxIntensity = 8f;
    public int flickerCount = 3;

    private AudioSource audioSource;
    private float[] samples;
    private bool isFlashing = false;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        samples = new float[sampleSize];

        if (lightningLight != null)
            lightningLight.enabled = false;
    }

    void Update()
    {
        if (isFlashing || !audioSource.isPlaying) return;

        float peak = GetAudioPeak();

        if (peak > threshold)
        {
            StartCoroutine(LightningFlash());
        }
    }

    float GetAudioPeak()
    {
        // Get raw clip data based on playback position (NOT affected by volume)
        int micPosition = audioSource.timeSamples;

        if (micPosition < sampleSize) return 0f;

        audioSource.clip.GetData(samples, micPosition - sampleSize);

        float max = 0f;

        for (int i = 0; i < sampleSize; i++)
        {
            float abs = Mathf.Abs(samples[i]);
            if (abs > max)
                max = abs;
        }

        return max;
    }

    IEnumerator LightningFlash()
    {
        isFlashing = true;

        for (int i = 0; i < flickerCount; i++)
        {
            lightningLight.enabled = true;
            lightningLight.intensity = Random.Range(minIntensity, maxIntensity);

            yield return new WaitForSeconds(Random.Range(minFlashDuration, maxFlashDuration));

            lightningLight.enabled = false;

            yield return new WaitForSeconds(Random.Range(0.02f, 0.1f));
        }

        isFlashing = false;
    }
}