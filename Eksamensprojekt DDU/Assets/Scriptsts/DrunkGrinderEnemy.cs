using UnityEngine;

public class DrunkGrinderEnemy : MonoBehaviour
{
    [Header("Transforms — assign in Inspector")]
    public Transform leftHip;
    public Transform rightHip;
    public Transform leftLowerLeg;
    public Transform rightLowerLeg;
    public Transform leftForearm;
    public Transform rightForearm;

    [Header("Movement")]
    public float searchSpeed = 0.8f;
    public float chaseSpeed = 2.4f;
    public Transform player;

    [Header("Detection")]
    public float detectionRange = 8f;
    public float attackRange = 1.5f;
    public float loseSightRange = 14f;

    [Header("Leg Animation")]
    [Range(0f, 60f)] public float legSwingAngle = 30f;
    [Range(0.1f, 5f)] public float searchLegFreq = 1.0f;
    [Range(0.1f, 8f)] public float chaseLegFreq = 2.8f;
    [Range(0f, 60f)] public float lowerLegBend = 20f;
    public bool invertLegs = false;

    [Header("Arm Animation")]
    [Range(0f, 60f)] public float armSwingAngle = 20f;
    [Range(0f, 30f)] public float beerArmAngle = 8f;
    public bool invertArms = false;

    [Header("Drunk Sway")]
    [Range(0f, 20f)] public float searchSwayMag = 6f;
    [Range(0.1f, 5f)] public float searchSwayFreq = 0.7f;
    [Range(0f, 10f)] public float chaseSwayMag = 2f;
    [Range(0.1f, 5f)] public float chaseSwayFreq = 1.3f;

    [Header("Joint Smoothing")]
    [Range(5f, 50f)] public float jointSmoothSpeed = 15f;

    [Header("Grinder SFX")]
    public AudioSource grinderAudio;
    public float grinderSoundInterval = 6f;

    // --- internal ---
    float _animTime;
    bool _chasing;
    float _grinderTimer;

    // ---------------------------------------------------------------

    void Update()
    {
        EvaluateState();
        AnimateBody();
        MoveEnemy();
        TickGrinderSound();
    }

    // ---------------------------------------------------------------
    // STATE MACHINE
    // ---------------------------------------------------------------

    void EvaluateState()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (!_chasing && dist < detectionRange) _chasing = true;
        else if (_chasing && dist > loseSightRange) _chasing = false;
    }

    // ---------------------------------------------------------------
    // MOVEMENT
    // ---------------------------------------------------------------

    void MoveEnemy()
    {
        if (player == null) return;
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist < attackRange) return;

        float speed = _chasing ? chaseSpeed : searchSpeed;
        Vector3 dir;

        if (_chasing)
        {
            dir = (player.position - transform.position).normalized;
        }
        else
        {
            float wanderAngle = Mathf.Sin(Time.time * 0.3f) * 45f;
            dir = Quaternion.Euler(0, wanderAngle, 0) * transform.forward;
        }

        dir.y = 0;
        transform.position += dir * speed * Time.deltaTime;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(dir) * Quaternion.Euler(0, 180f, 0),
                Time.deltaTime * (speed * 1.5f)
            );
    }

    // ---------------------------------------------------------------
    // ANIMATION
    // ---------------------------------------------------------------

    void AnimateBody()
    {
        float freq = _chasing ? chaseLegFreq : searchLegFreq;
        float sway = _chasing
            ? Mathf.Sin(Time.time * chaseSwayFreq) * chaseSwayMag
            : Mathf.Sin(Time.time * searchSwayFreq) * searchSwayMag;

        _animTime += Time.deltaTime * freq;
        float t = Mathf.Sin(_animTime);

        float dir = invertLegs ? -1f : 1f;
        float adir = invertArms ? -1f : 1f;

        float leftHipX = -90f + dir * t * legSwingAngle + sway;
        float rightHipX = -90f + dir * -t * legSwingAngle + sway;

        float leftLowerX = -90f + lowerLegBend;
        float rightLowerX = -90f + lowerLegBend;

        float rightArmX = -90f + adir * t * armSwingAngle;
        float leftArmX = -90f + adir * -t * beerArmAngle;

        SetXRotation(leftHip, leftHipX);
        SetXRotation(rightHip, rightHipX);
        SetXRotation(leftLowerLeg, leftLowerX);
        SetXRotation(rightLowerLeg, rightLowerX);
        SetXRotation(leftForearm, leftArmX);
        SetXRotation(rightForearm, rightArmX);
    }

    void SetXRotation(Transform t, float xDeg)
    {
        if (t == null) return;
        Quaternion target = Quaternion.Euler(xDeg, t.localEulerAngles.y, t.localEulerAngles.z);
        t.localRotation = Quaternion.Slerp(t.localRotation, target, Time.deltaTime * jointSmoothSpeed);
    }

    // ---------------------------------------------------------------
    // GRINDER SOUND
    // ---------------------------------------------------------------

    void TickGrinderSound()
    {
        if (grinderAudio == null) return;
        _grinderTimer -= Time.deltaTime;
        if (_grinderTimer <= 0f)
        {
            grinderAudio.Play();
            _grinderTimer = _chasing
                ? Random.Range(2f, 4f)
                : Random.Range(grinderSoundInterval * 0.7f, grinderSoundInterval * 1.3f);
        }
    }

    // ---------------------------------------------------------------
    // DEBUG GIZMOS
    // ---------------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(transform.position, loseSightRange);
    }
}