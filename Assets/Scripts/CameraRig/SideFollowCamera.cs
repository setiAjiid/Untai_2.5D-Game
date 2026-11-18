using UnityEngine;

namespace Untai.CameraRig
{
    /// <summary>
    /// Kamera samping yang mengikuti target dengan SmoothDamp dan sedikit look-ahead
    /// ke arah gerak, sehingga pemain melihat celah di depannya.
    /// </summary>
    public class SideFollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 2.5f, -17f);
        public float smoothTime = 0.25f;
        public float lookAheadDistance = 3f;
        public float lookAheadSmooth = 0.6f;
        public float pitch = 5f;

        Vector3 _velocity;
        float _lookAhead;
        float _lookAheadVel;
        Rigidbody _targetBody;

        public static SideFollowCamera Attach(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.fieldOfView = 45f;
            var follow = cam.GetComponent<SideFollowCamera>();
            if (follow == null) follow = cam.gameObject.AddComponent<SideFollowCamera>();
            follow.SetTarget(target);
            return follow;
        }

        public void SetTarget(Transform t)
        {
            target = t;
            _targetBody = t != null ? t.GetComponent<Rigidbody>() : null;
            Snap();
        }

        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            _velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            if (target == null) return;
            float vx = _targetBody != null ? _targetBody.linearVelocity.x : 0f;
            float desiredAhead = Mathf.Clamp(vx * 0.5f, -lookAheadDistance, lookAheadDistance);
            _lookAhead = Mathf.SmoothDamp(_lookAhead, desiredAhead, ref _lookAheadVel, lookAheadSmooth);

            Vector3 desired = target.position + offset + new Vector3(_lookAhead, 0f, 0f);
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
            transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }
}
