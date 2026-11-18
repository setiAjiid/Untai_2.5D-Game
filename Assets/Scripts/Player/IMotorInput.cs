namespace Untai.Player
{
    /// <summary>
    /// Sumber input untuk PlayerMotor. Pemain memakai keyboard/gamepad (InputReader);
    /// PartnerBrain mengisi antarmuka ini agar partner bergerak lewat motor yang sama
    /// (tanpa teleport), sehingga fisika dan batasan untai berlaku sama untuk keduanya.
    /// </summary>
    public interface IMotorInput
    {
        /// <summary>-1..1 (boleh sedikit di atas 1 untuk partner yang lebih cekatan).</summary>
        float Horizontal { get; }
        /// <summary>True satu kali saat lompat ditekan.</summary>
        bool JumpPressed { get; }
        bool JumpHeld { get; }
    }
}
