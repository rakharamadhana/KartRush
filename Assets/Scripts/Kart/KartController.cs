using UnityEngine;
using UnityEngine.InputSystem;

namespace KartRush
{
    /// <summary>
    /// Arcade kart driven by an invisible physics sphere. The visible kart (this transform)
    /// follows the sphere, yaws with steering and tilts to match the ground.
    /// Keyboard: W/S or Up/Down = gas/brake, A/D or Left/Right = steer, Space/Left Shift = drift, R = reset.
    /// Gamepad: right trigger = gas, left trigger = brake, left stick = steer, south button = drift.
    /// </summary>
    public class KartController : MonoBehaviour
    {
        [Header("References")]
        public Rigidbody sphere;
        public Transform visual;          // child that leans / wobbles
        public Transform[] frontWheels;   // optional, turn with steering
        public Transform[] allWheels;     // optional, spin with speed

        [Header("Speed")]
        public float maxSpeed = 32f;
        public float maxReverseSpeed = 9f;
        public float acceleration = 22f;
        public float brakeDeceleration = 40f;
        public float coastDeceleration = 8f;
        public float offRoadSpeedFactor = 0.55f;

        [Header("Handling")]
        public float turnRate = 110f;     // degrees / second at full lock
        public float driftTurnBonus = 1.25f;
        public float gripPerSecond = 9f;
        public float driftGripPerSecond = 1.6f;
        public float extraGravity = 25f;
        public float groundRayLength = 1.1f;
        public LayerMask groundMask = ~0;

        [Header("Drift / Boost")]
        public float minDriftSpeed = 12f;
        public float[] miniTurboTimes = { 0.9f, 1.8f, 2.8f };
        public float[] miniTurboDurations = { 0.6f, 1.1f, 1.7f };
        public float boostSpeed = 44f;
        public float boostAcceleration = 60f;

        [Header("Drift feedback")]
        public float hopDuration = 0.3f;
        public float hopHeight = 0.6f;

        [Header("Hits")]
        public float spinDuration = 1.3f;
        public float hitImmunity = 1.5f;

        [Header("Scripted input (AI / tests)")]
        public bool useScriptedInput;
        public float scriptedThrottle, scriptedBrake, scriptedSteer;
        public bool scriptedDrift;

        // --- Runtime state (read by HUD / camera / race logic) ---
        public bool canDrive;
        public bool IsGrounded { get; private set; }
        public bool IsDrifting { get; private set; }
        public bool IsBoosting => boostTimer > 0f;
        public bool IsSpinning => spinTimer > 0f;
        /// <summary>0..1 progress of the drift charge towards the last mini-turbo level.</summary>
        public float DriftCharge01 => IsDrifting ? Mathf.Clamp01(driftTimer / miniTurboTimes[miniTurboTimes.Length - 1]) : 0f;
        public event System.Action DriftStarted;
        public int DriftLevel { get; private set; }   // 0 = none, 1..3 = mini-turbo charge
        public float ForwardSpeed { get; private set; }
        public float SpeedKmh => Mathf.Abs(ForwardSpeed) * 3.6f;
        public Vector3 Heading => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        float yaw;
        float steerInput, throttleInput, brakeInput;
        bool driftHeld, driftPressedThisFrame;
        float driftDir, driftTimer, boostTimer;
        float spinTimer, spinTotal = 1f, immuneTimer, hopTimer;
        Vector3 groundNormal = Vector3.up;
        Vector3 smoothNormal = Vector3.up;
        Quaternion visualRoll = Quaternion.identity;
        float wheelSpin;

        Vector3 lastSafePosition;
        float lastSafeYaw;

        void Start()
        {
            if (sphere != null)
            {
                sphere.transform.SetParent(null, true);
                sphere.interpolation = RigidbodyInterpolation.Interpolate;
                sphere.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
            yaw = transform.eulerAngles.y;
            lastSafePosition = transform.position;
            lastSafeYaw = yaw;
        }

        /// <summary>Teleport the kart (used at race start and for respawns).</summary>
        public void PlaceAt(Vector3 position, float newYaw)
        {
            yaw = newYaw;
            sphere.position = position;
            sphere.transform.position = position;
            sphere.linearVelocity = Vector3.zero;
            sphere.angularVelocity = Vector3.zero;
            transform.position = position - Vector3.up * sphere.GetComponent<SphereCollider>().radius;
            transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            driftTimer = 0f; IsDrifting = false; DriftLevel = 0; boostTimer = 0f;
            spinTimer = 0f; immuneTimer = 0f; hopTimer = 0f;
        }

        public void SetCheckpointRespawn(Vector3 position, float newYaw)
        {
            lastSafePosition = position;
            lastSafeYaw = newYaw;
        }

        public void Respawn() => PlaceAt(lastSafePosition + Vector3.up * 1.2f, lastSafeYaw);

        public void GiveBoost(float seconds) => boostTimer = Mathf.Max(boostTimer, seconds);

        /// <summary>Knocked off course by a banana or shell: spin, lose speed and any boost. Returns false if immune.</summary>
        public bool SpinOut(float seconds = -1f)
        {
            if (spinTimer > 0f || immuneTimer > 0f) return false;
            spinTotal = spinTimer = seconds > 0f ? seconds : spinDuration;
            boostTimer = 0f;
            CancelDrift(false);
            return true;
        }

        void Update()
        {
            ReadInput();

            if (transform.position.y < -15f) Respawn();

            if (spinTimer > 0f)
            {
                spinTimer -= Time.deltaTime;
                if (spinTimer <= 0f) immuneTimer = hitImmunity;
            }
            else if (immuneTimer > 0f) immuneTimer -= Time.deltaTime;

            float speedFactor = Mathf.Clamp(ForwardSpeed / 6f, -1f, 1f);
            if (canDrive && IsGrounded && spinTimer <= 0f && Mathf.Abs(ForwardSpeed) > 0.5f)
            {
                UpdateDrift();
                float steer = EffectiveSteer();
                yaw += steer * turnRate * speedFactor * Time.deltaTime;
            }
            else if (!canDrive || spinTimer > 0f)
            {
                steerInput = throttleInput = brakeInput = 0f;
                CancelDrift(false);
            }

            if (boostTimer > 0f) boostTimer -= Time.deltaTime;
            if (hopTimer > 0f) hopTimer -= Time.deltaTime;

            FollowSphere();
            AnimateVisuals();
        }

        void ReadInput()
        {
            float steer = 0f, gas = 0f, brake = 0f;
            bool drift = false;

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) gas = 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) brake = 1f;
                if (kb.spaceKey.isPressed || kb.leftShiftKey.isPressed) drift = true;
                if (kb.rKey.wasPressedThisFrame && canDrive) Respawn();
            }

            var pad = Gamepad.current;
            if (pad != null)
            {
                steer += pad.leftStick.x.ReadValue();
                gas = Mathf.Max(gas, pad.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, pad.leftTrigger.ReadValue());
                if (pad.buttonSouth.isPressed || pad.rightShoulder.isPressed) drift = true;
            }

            // Scripted control (AI racers, automated tests) replaces the player's input.
            if (useScriptedInput)
            {
                steer = scriptedSteer; gas = scriptedThrottle; brake = scriptedBrake; drift = scriptedDrift;
            }

            steerInput = Mathf.MoveTowards(steerInput, Mathf.Clamp(steer, -1f, 1f), 6f * Time.deltaTime);
            throttleInput = gas;
            brakeInput = brake;

            driftPressedThisFrame = drift && !driftHeld;
            driftHeld = drift;
        }

        void UpdateDrift()
        {
            if (!IsDrifting)
            {
                // Start a drift: need speed, hold drift while steering.
                if (driftHeld && Mathf.Abs(steerInput) > 0.3f && ForwardSpeed > minDriftSpeed)
                {
                    IsDrifting = true;
                    driftDir = Mathf.Sign(steerInput);
                    driftTimer = 0f;
                    DriftLevel = 0;
                    hopTimer = hopDuration;
                    DriftStarted?.Invoke();
                }
                return;
            }

            if (!driftHeld || ForwardSpeed < minDriftSpeed * 0.6f)
            {
                CancelDrift(true);
                return;
            }

            driftTimer += Time.deltaTime;
            DriftLevel = 0;
            for (int i = 0; i < miniTurboTimes.Length; i++)
                if (driftTimer >= miniTurboTimes[i]) DriftLevel = i + 1;
        }

        void CancelDrift(bool awardBoost)
        {
            if (awardBoost && DriftLevel > 0)
                GiveBoost(miniTurboDurations[Mathf.Clamp(DriftLevel - 1, 0, miniTurboDurations.Length - 1)]);
            IsDrifting = false;
            DriftLevel = 0;
            driftTimer = 0f;
        }

        // While drifting you turn wider/tighter around the drift direction (like the real thing).
        float EffectiveSteer()
        {
            if (!IsDrifting) return steerInput;
            float t = Mathf.InverseLerp(-1f, 1f, steerInput * driftDir); // 0 = against drift, 1 = into it
            return driftDir * Mathf.Lerp(0.35f, driftTurnBonus, t);
        }

        void FixedUpdate()
        {
            if (sphere == null) return;

            var origin = sphere.position;
            float radius = ((SphereCollider)sphere.GetComponent<Collider>()).radius;
            IsGrounded = Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                radius + groundRayLength * 0.2f, groundMask, QueryTriggerInteraction.Ignore);

            Vector3 vel = sphere.linearVelocity;

            if (IsGrounded)
            {
                groundNormal = hit.normal;
                Vector3 fwd = Vector3.ProjectOnPlane(Heading, groundNormal).normalized;
                float cur = Vector3.Dot(vel, fwd);
                ForwardSpeed = cur;

                float top = maxSpeed;
                float accel = acceleration;
                if (IsBoosting) { top = boostSpeed; accel = boostAcceleration; }
                if (hit.collider != null && hit.collider.GetComponent<OffRoadSurface>() != null) top *= offRoadSpeedFactor;

                float target;
                float rate;
                if (!canDrive) { target = 0f; rate = brakeDeceleration; }
                else if (spinTimer > 0f) { target = 0f; rate = brakeDeceleration * 0.5f; }
                else if (IsBoosting) { target = top; rate = accel; }
                else if (brakeInput > 0.05f && throttleInput < 0.05f)
                {
                    target = cur > 0.5f ? 0f : -maxReverseSpeed * brakeInput;
                    rate = cur > 0.5f ? brakeDeceleration : acceleration * 0.6f;
                }
                else if (throttleInput > 0.05f) { target = top * throttleInput; rate = accel; }
                else { target = 0f; rate = coastDeceleration; }

                float newSpeed = Mathf.MoveTowards(cur, target, rate * Time.fixedDeltaTime);

                Vector3 lateral = vel - fwd * cur;
                lateral.y = 0f;
                float grip = IsDrifting ? driftGripPerSecond : gripPerSecond;
                lateral = Vector3.Lerp(lateral, Vector3.zero, 1f - Mathf.Exp(-grip * Time.fixedDeltaTime));

                Vector3 newVel = fwd * newSpeed + lateral;
                newVel.y = vel.y; // keep gravity / slopes handled by physics
                sphere.linearVelocity = newVel;

                // Stick to the ground a bit.
                sphere.AddForce(-groundNormal * 6f, ForceMode.Acceleration);
            }
            else
            {
                sphere.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);
                ForwardSpeed = Vector3.Dot(vel, Heading);
            }
        }

        void FollowSphere()
        {
            // Use the interpolated transform (sphere.position is the raw 50 Hz physics value => visible stutter).
            float radius = sphere != null ? ((SphereCollider)sphere.GetComponent<Collider>()).radius : 0.5f;
            transform.position = sphere.transform.position - Vector3.up * radius;

            smoothNormal = Vector3.Slerp(smoothNormal, IsGrounded ? groundNormal : Vector3.up, 8f * Time.deltaTime);
            Vector3 fwd = Vector3.ProjectOnPlane(Heading, smoothNormal);
            if (fwd.sqrMagnitude < 0.0001f) fwd = Heading;
            transform.rotation = Quaternion.LookRotation(fwd, smoothNormal);
        }

        void AnimateVisuals()
        {
            // Body roll / drift slide.
            float roll = -steerInput * 6f;
            float slide = 0f;
            if (IsDrifting) { roll = -driftDir * 10f; slide = driftDir * 18f; }
            if (visual != null)
            {
                if (spinTimer > 0f)
                    visual.localRotation = Quaternion.Euler(0f, 720f * (1f - spinTimer / spinTotal), 0f);
                else
                {
                    var target = Quaternion.Euler(0f, slide, roll);
                    visual.localRotation = Quaternion.Slerp(visual.localRotation, target, 10f * Time.deltaTime);
                }
                // Little hop when a drift starts.
                float hop = hopTimer > 0f ? hopHeight * Mathf.Sin(Mathf.PI * (1f - hopTimer / hopDuration)) : 0f;
                visual.localPosition = new Vector3(0f, hop, 0f);
            }

            float steerAngle = steerInput * 28f;
            if (frontWheels != null)
                foreach (var w in frontWheels)
                    if (w != null) w.localRotation = Quaternion.Euler(0f, steerAngle, 0f) * Quaternion.Euler(wheelSpin, 0f, 0f);

            wheelSpin += ForwardSpeed * 60f * Time.deltaTime;
            if (allWheels != null)
                foreach (var w in allWheels)
                    if (w != null && (frontWheels == null || System.Array.IndexOf(frontWheels, w) < 0))
                        w.localRotation = Quaternion.Euler(wheelSpin, 0f, 0f);
        }
    }
}
