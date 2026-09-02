using System;
using UnityEngine;
using GridSense.Physics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GridSense.Audio
{
    /// <summary>
    /// F1EngineSoundSystem provides authentic Formula 1 V6 Turbo-Hybrid engine soundscape:
    /// - Multi-layer pitch & volume blending (Idle rumble, high-RPM screaming V6 roar, turbo whistle).
    /// - Dynamic pitch scaling across Gears 1 to 8 (4,000 to 14,800 RPM).
    /// - Crisp pneumatic gear shift & overrun exhaust pop sound effects.
    /// - Studio-quality sampled audio architecture (no buzzing, no static, no glitching).
    /// </summary>
    public class F1EngineSoundSystem : MonoBehaviour
    {
        [Header("Vehicle Reference")]
        [SerializeField] private CarPhysicsController car;

        [Header("Audio Sources")]
        private AudioSource idleSource;
        private AudioSource highSource;
        private AudioSource turboSource;
        private AudioSource popSource;

        [Header("Master Volume")]
        [Range(0.0f, 1.0f)] public float masterVolume = 0.85f;

        private float currentRpmRatio = 0.0f; // 0.0 at idle to 1.0 at 14,800 RPM
        private VehicleGear prevGear = VehicleGear.Gear1;
        private float prevThrottle = 0.0f;

        private void Awake()
        {
            if (car == null)
            {
                car = GetComponentInParent<CarPhysicsController>();
            }

            SetupAudioSources();
        }

        private void SetupAudioSources()
        {
            idleSource = CreateChildAudioSource("Engine_Idle_Layer", true);
            highSource = CreateChildAudioSource("Engine_High_Layer", true);
            turboSource = CreateChildAudioSource("Engine_Turbo_Layer", true);
            popSource = CreateChildAudioSource("Engine_Pop_Layer", false);

            AudioClip idleClip = LoadClip("Assets/Audio/f1_idle.wav");
            AudioClip highClip = LoadClip("Assets/Audio/f1_engine_high.wav");
            AudioClip turboClip = LoadClip("Assets/Audio/f1_turbo.wav");
            AudioClip popClip = LoadClip("Assets/Audio/f1_gear_pop.wav");

            if (idleClip != null && idleSource != null)
            {
                idleSource.clip = idleClip;
                idleSource.Play();
            }
            if (highClip != null && highSource != null)
            {
                highSource.clip = highClip;
                highSource.Play();
            }
            if (turboClip != null && turboSource != null)
            {
                turboSource.clip = turboClip;
                turboSource.Play();
            }
            if (popClip != null && popSource != null)
            {
                popSource.clip = popClip;
            }
        }

        private AudioSource CreateChildAudioSource(string name, bool loop)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(transform, false);
            child.transform.localPosition = new Vector3(0.0f, 0.4f, -1.2f); // Rear engine/exhaust location

            AudioSource src = child.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = 0.50f;
            src.minDistance = 3.0f;
            src.maxDistance = 120.0f;
            src.rolloffMode = AudioRolloffMode.Logarithmic;
            src.volume = 0.0f;
            return src;
        }

        private AudioClip LoadClip(string assetPath)
        {
            AudioClip clip = null;
#if UNITY_EDITOR
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
            if (clip == null)
            {
                // Runtime fallback search
                string clipName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                clip = Resources.Load<AudioClip>(clipName);
            }
            return clip;
        }

        private void Update()
        {
            if (car == null)
            {
                car = GetComponentInParent<CarPhysicsController>();
                if (car == null) return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            float speedKmh = car.CurrentSpeedKmh;
            float throttle = car.ThrottleInput;

            // 1. Calculate realistic gear-dependent RPM ratio (0.0 to 1.0)
            float targetRpmRatio = 0.0f;

            if (car.CurrentGear == VehicleGear.Neutral)
            {
                targetRpmRatio = throttle * 0.75f;
            }
            else if (car.CurrentGear == VehicleGear.Reverse)
            {
                targetRpmRatio = Mathf.Clamp01(speedKmh / 65.0f);
            }
            else
            {
                float[] gearMaxSpeeds = new float[] { 105f, 155f, 205f, 250f, 290f, 325f, 355f, 380f };
                int gIdx = Mathf.Clamp((int)car.CurrentGear - 1, 0, 7);

                float minSpeed = (gIdx > 0) ? gearMaxSpeeds[gIdx - 1] * 0.70f : 0.0f;
                float maxSpeed = gearMaxSpeeds[gIdx];

                float ratio = Mathf.Clamp01((speedKmh - minSpeed) / Mathf.Max(1.0f, maxSpeed - minSpeed));
                targetRpmRatio = Mathf.Lerp(0.35f, 1.0f, ratio);

                if (speedKmh < 30.0f && gIdx == 0)
                {
                    targetRpmRatio = Mathf.Lerp(0.0f, 0.65f, speedKmh / 30.0f);
                }

                targetRpmRatio += (throttle * 0.08f);
            }

            targetRpmRatio = Mathf.Clamp01(targetRpmRatio);
            currentRpmRatio = Mathf.Lerp(currentRpmRatio, targetRpmRatio, dt * 18.0f);

            // 2. Modulate Audio Layers
            // Idle layer: Active at low RPM, fades out as engine roars
            if (idleSource != null && idleSource.isPlaying)
            {
                idleSource.pitch = Mathf.Lerp(0.90f, 1.35f, currentRpmRatio);
                idleSource.volume = Mathf.Lerp(0.70f, 0.08f, currentRpmRatio * 1.5f) * masterVolume;
            }

            // High RPM screaming V6 layer: Scales pitch and volume with RPM & throttle
            if (highSource != null && highSource.isPlaying)
            {
                highSource.pitch = Mathf.Lerp(0.55f, 1.55f, currentRpmRatio);
                float loadFactor = Mathf.Clamp01(0.35f + (throttle * 0.65f));
                highSource.volume = Mathf.Lerp(0.05f, 0.85f, currentRpmRatio) * loadFactor * masterVolume;
            }

            // Turbo whistle layer: Proportional to throttle and boost
            if (turboSource != null && turboSource.isPlaying)
            {
                float boostMult = car.IsBoostActive ? 1.4f : 1.0f;
                turboSource.pitch = Mathf.Lerp(0.85f, 1.45f, currentRpmRatio) * boostMult;
                turboSource.volume = (throttle * 0.28f + (car.IsBoostActive ? 0.20f : 0.0f)) * masterVolume;
            }

            // 3. Gear Shift Pneumatic Pop Sound Trigger
            if (car.CurrentGear != prevGear)
            {
                prevGear = car.CurrentGear;
                TriggerExhaustPop(0.85f);
            }

            // 4. Overrun Crackle on Lift-Off
            if (prevThrottle > 0.6f && throttle < 0.1f && currentRpmRatio > 0.6f)
            {
                TriggerExhaustPop(0.55f);
            }
            prevThrottle = throttle;
        }

        private void TriggerExhaustPop(float vol)
        {
            if (popSource != null && popSource.clip != null)
            {
                popSource.pitch = UnityEngine.Random.Range(0.88f, 1.15f);
                popSource.PlayOneShot(popSource.clip, vol * masterVolume);
            }
        }
    }
}
