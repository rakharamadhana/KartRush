using UnityEngine;

namespace KartRush
{
    /// <summary>Smooth chase camera that swings behind the kart's heading, pulls back and widens FOV with speed.</summary>
    [RequireComponent(typeof(Camera))]
    public class KartCamera : MonoBehaviour
    {
        public KartController kart;
        public float distance = 7.5f;
        public float height = 3.2f;
        public float lookAhead = 4f;
        public float positionLag = 7f;
        public float rotationLag = 5f;
        public float baseFov = 62f;
        public float maxFovBonus = 22f;

        Camera cam;
        Vector3 smoothHeading = Vector3.forward;

        void Awake() => cam = GetComponent<Camera>();

        void LateUpdate()
        {
            if (kart == null) return;

            Vector3 target = kart.transform.position;
            Vector3 heading = kart.Heading;
            // Drift looks better when the camera lags behind the kart's nose.
            smoothHeading = Vector3.Slerp(smoothHeading, heading, 1f - Mathf.Exp(-rotationLag * Time.deltaTime));
            smoothHeading.y = 0f;
            if (smoothHeading.sqrMagnitude < 0.001f) smoothHeading = heading;
            smoothHeading.Normalize();

            float speedT = Mathf.Clamp01(kart.SpeedKmh / 160f);
            float dist = distance + speedT * 1.5f;
            Vector3 desired = target - smoothHeading * dist + Vector3.up * height;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionLag * Time.deltaTime));

            Vector3 lookPoint = target + smoothHeading * lookAhead + Vector3.up * 1.2f;
            Quaternion wanted = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-12f * Time.deltaTime));

            float fovTarget = baseFov + maxFovBonus * speedT + (kart.IsBoosting ? 10f : 0f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fovTarget, 4f * Time.deltaTime);
        }

        /// <summary>Snap behind the kart (race start / respawn).</summary>
        public void Snap()
        {
            if (kart == null) return;
            smoothHeading = kart.Heading;
            transform.position = kart.transform.position - smoothHeading * distance + Vector3.up * height;
            transform.rotation = Quaternion.LookRotation(kart.transform.position + smoothHeading * lookAhead - transform.position, Vector3.up);
        }
    }
}
