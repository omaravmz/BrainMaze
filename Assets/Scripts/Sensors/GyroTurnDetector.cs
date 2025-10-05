using UnityEngine;
using UnityEngine.Events;

public class GyroTurnDetector : MonoBehaviour
{
    [Header("Proveedor (el mismo que usa PlayerMotor)")]
    [SerializeField] private MonoBehaviour sensorBehaviour; // Debe implementar ISensorProvider
    private ISensorProvider sensor;

    [Header("Detección por umbral")]
    [Tooltip("Grados de cabeza para disparar giro (ej. 20). Der=+, Izq=-")]
    [SerializeField] private float thresholdDeg = 20f;

    [Tooltip("Histéresis: margen para volver a zona neutra (ej. 10)")]
    [SerializeField] private float hysteresisDeg = 10f;

    [Tooltip("Tiempo mínimo entre giros (anti-rebote)")]
    [SerializeField] private float cooldownSec = 0.5f;

    [Header("Evento de salida")]
    public UnityEvent OnTurnRight; // derecha = +yaw
    public UnityEvent OnTurnLeft;  // izquierda = -yaw

    // Estado interno
    private enum Region { Neutral, RightHold, LeftHold }
    private Region region = Region.Neutral;
    private float lastTurnTime = -999f;

    void Awake()
    {
        sensor = sensorBehaviour as ISensorProvider;
        if (sensor == null)
            Debug.LogError("GyroTurnDetector: 'sensorBehaviour' no implementa ISensorProvider.");
    }

    void Update()
    {
        if (sensor == null || !sensor.IsStable) return;

        float yaw = sensor.HeadYawOffsetDeg;
        float now = Time.time;

        switch (region)
        {
            case Region.Neutral:
                // ¿Cruzó a derecha?
                if (yaw >= thresholdDeg && (now - lastTurnTime) >= cooldownSec)
                {
                    lastTurnTime = now;
                    region = Region.RightHold;
                    OnTurnRight?.Invoke();
                }
                // ¿Cruzó a izquierda?
                else if (yaw <= -thresholdDeg && (now - lastTurnTime) >= cooldownSec)
                {
                    lastTurnTime = now;
                    region = Region.LeftHold;
                    OnTurnLeft?.Invoke();
                }
                break;

            case Region.RightHold:
                // Volver a neutro cuando baje por debajo de (threshold - hysteresis)
                if (yaw <= thresholdDeg - hysteresisDeg)
                    region = Region.Neutral;
                break;

            case Region.LeftHold:
                if (yaw >= -(thresholdDeg - hysteresisDeg))
                    region = Region.Neutral;
                break;
        }
    }

    // Helpers por si quieres disparar turnos manualmente desde UI/tests
    public void ForceTurnRight() => OnTurnRight?.Invoke();
    public void ForceTurnLeft()  => OnTurnLeft?.Invoke();

    // Accesores útiles para depuración/ajustes desde HUD
    public float ThresholdDeg => thresholdDeg;
    public float HysteresisDeg => hysteresisDeg;
    public float CooldownSec => cooldownSec;
}
