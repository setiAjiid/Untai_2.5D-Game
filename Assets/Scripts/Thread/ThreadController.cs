using UnityEngine;
using Untai.Player;

namespace Untai.Thread
{
    /// <summary>
    /// Sumber daya benang. Benang terikat pada checkpoint aktif (titik tambat):
    /// sisa benang = panjang maksimum - jarak horizontal pemain dari titik tambat.
    /// Jika benang habis, pemain tidak bisa menjauh lagi (benang tegang).
    /// Mencapai checkpoint baru memindahkan titik tambat sehingga benang pulih.
    /// </summary>
    [DefaultExecutionOrder(50)] // setelah PlayerMotor dan ThreadSwing agar batas benang menang
    [RequireComponent(typeof(PlayerMotor))]
    public class ThreadController : MonoBehaviour
    {
        public float maxLength = 20f;

        public Vector3 TetherPoint { get; private set; }
        public float Used => Mathf.Abs(transform.position.x - TetherPoint.x);
        public float Remaining => Mathf.Max(0f, maxLength - Used);
        public float RemainingRatio => maxLength > 0f ? Remaining / maxLength : 0f;
        public bool IsTaut => Used >= maxLength - 0.05f;

        PlayerMotor _motor;

        void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            TetherPoint = transform.position;
        }

        /// <summary>Pindahkan titik tambat (checkpoint) dan atur panjang benang.</summary>
        public void SetTether(Vector3 point, float length)
        {
            TetherPoint = point;
            maxLength = Mathf.Max(1f, length);
        }

        void FixedUpdate()
        {
            var body = _motor.Body;
            Vector3 pos = body.position;
            float dx = pos.x - TetherPoint.x;
            if (Mathf.Abs(dx) <= maxLength) return;

            // Benang habis: tahan pemain di batas dan hapus kecepatan yang menjauh.
            float side = Mathf.Sign(dx);
            pos.x = TetherPoint.x + side * maxLength;
            body.position = pos;
            Vector3 v = body.linearVelocity;
            if (v.x * side > 0f) v.x = 0f;
            body.linearVelocity = v;
        }
    }
}
