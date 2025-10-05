using UnityEngine;

public class SensorMock : MonoBehaviour, ISensorProvider
{
    [Header("Concentración (simulada)")]
    [Range(0f,1f)] public float concSlider = 0f;

    [Header("Giro de cabeza (simulado)")]
    public KeyCode yawLeftKey = KeyCode.A;
    public KeyCode yawRightKey = KeyCode.D;
    public float yawSpeedDegPerSec = 60f;
    public float yawClampAbs = 35f;

    [Header("Calibración")]
    public KeyCode calibrateKey = KeyCode.C;

    public float Concentration01 => concSlider;
    public float HeadYawOffsetDeg => simYawOffsetDeg;
    public bool  IsStable => true;

    private float simYawOffsetDeg;

    void Update()
    {
        float delta = 0f;
        if (Input.GetKey(yawLeftKey))  delta -= yawSpeedDegPerSec * Time.deltaTime;
        if (Input.GetKey(yawRightKey)) delta += yawSpeedDegPerSec * Time.deltaTime;
        simYawOffsetDeg = Mathf.Clamp(simYawOffsetDeg + delta, -yawClampAbs, yawClampAbs);

        if (Input.GetKeyDown(calibrateKey)) CalibrateNeutralYaw();
    }

    public void CalibrateNeutralYaw() => simYawOffsetDeg = 0f;
}
