using UnityEngine;
using UnityEngine.UI;

public class ClockController : MonoBehaviour
{
    public enum TickMode
    {
        RealTime,     // ticks every X seconds
        MinuteStep    // ticks when minute changes
    }

    [Header("Clock Hands")]
    public Transform hourHand;
    public Transform minuteHand;

    [Header("Time Settings")]
    [Range(0, 23)] public int startHour = 12;
    [Range(0, 59)] public int startMinute = 0;

    [Tooltip("1 = real time, 60 = 1 minute per second")]
    public float timeScale = 1f;

    [Header("Audio Settings")]
    public AudioSource tickAudioSource;

    [Tooltip("Sound for real-time ticking")]
    public AudioClip realTimeTickSound;

    [Tooltip("Sound for minute step ticking")]
    public AudioClip minuteTickSound;

    public TickMode tickMode = TickMode.MinuteStep;

    [Tooltip("Seconds between ticks in real-time mode")]
    public float realTimeTickInterval = 1f;

    [Range(0f, 1f)]
    public float tickVolume = 1f;

    [Header("Optional UI Slider")]
    public Slider volumeSlider;

    private float currentTimeMinutes;
    private int lastMinutePlayed = -1;
    private float realTimeTickTimer = 0f;

    void Start()
    {
        currentTimeMinutes = startHour * 60f + startMinute;

        if (volumeSlider != null)
        {
            volumeSlider.value = tickVolume;
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        ApplyVolume();
    }

    void Update()
    {
        // Advance time
        currentTimeMinutes += Time.deltaTime * timeScale;
        currentTimeMinutes %= (24f * 60f);

        float totalMinutes = currentTimeMinutes;
        float minute = totalMinutes % 60f;
        float hour = (totalMinutes / 60f) % 12f;

        // Angles
        float minuteAngle = (minute / 60f) * 360f;
        float hourAngle = ((hour + minute / 60f) / 12f) * 360f;

        minuteHand.localRotation = Quaternion.Euler(-minuteAngle, 0f, 0f);
        hourHand.localRotation = Quaternion.Euler(-hourAngle, 0f, 0f);

        HandleTicking(totalMinutes);
    }

    void HandleTicking(float totalMinutes)
    {
        switch (tickMode)
        {
            case TickMode.MinuteStep:
                HandleMinuteTick(totalMinutes);
                break;

            case TickMode.RealTime:
                HandleRealTimeTick();
                break;
        }
    }

    void HandleMinuteTick(float totalMinutes)
    {
        int currentMinuteInt = Mathf.FloorToInt(totalMinutes % 60f);

        if (currentMinuteInt != lastMinutePlayed)
        {
            lastMinutePlayed = currentMinuteInt;

            if (tickAudioSource != null && minuteTickSound != null)
            {
                tickAudioSource.PlayOneShot(minuteTickSound, tickVolume);
            }
        }
    }

    void HandleRealTimeTick()
    {
        realTimeTickTimer += Time.deltaTime;

        if (realTimeTickTimer >= realTimeTickInterval)
        {
            realTimeTickTimer = 0f;

            if (tickAudioSource != null && realTimeTickSound != null)
            {
                tickAudioSource.PlayOneShot(realTimeTickSound, tickVolume);
            }
        }
    }

    public void SetVolume(float value)
    {
        tickVolume = value;
        ApplyVolume();
    }

    private void ApplyVolume()
    {
        if (tickAudioSource != null)
        {
            tickAudioSource.volume = tickVolume;
        }
    }
}