using System;
using UnityEngine;

public class AcelerometerTurnDetector : MonoBehaviour
{
    [Header("Proveedor (ISensorProvider)")]
    [SerializeField] private MonoBehaviour sensorBehaviour; // arrastra PythonUDPProvider (o mock)
    private ISensorProvider sensor;

    [Header("Umbrales (g)")]
    [SerializeField] private float deadzone = 0.05f;   // zona muerta
    [SerializeField] private float threshold = 0.20f;  // dispara giro
    [SerializeField] private float release = 0.12f;    // histéresis para soltar

    [Header("Antirrebote")]
    [SerializeField] private float cooldown = 0.35f;   // s entre giros

    [Header("Calibración")]
    [SerializeField] private float neutralAccelX = 0f; // offset

    // Estado interno
    private int tiltState = 0; // -1 izq, 0 neutro, +1 der
    private float lastTurnTime = -999f;

    // Eventos
    public event Action OnTurnLeft;
    public event Action OnTurnRight;

    void Awake()
    {
        sensor = sensorBehaviour as ISensorProvider;
        if (sensor == null)
            Debug.LogError("AcelerometerTurnDetector: 'sensorBehaviour' no implementa ISensorProvider.");

        // Calibración inicial
        neutralAccelX = sensor != null ? sensor.Accelerometer.x : 0f;
    }

    void Update()
    {
        if (sensor == null) return;

        float ax = sensor.Accelerometer.x - neutralAccelX;
        if (Mathf.Abs(ax) < deadzone) ax = 0f;

        int target = 0;
        if (ax >= threshold) target = +1;            // derecha
        else if (ax <= -threshold) target = -1;      // izquierda
        else if (Mathf.Abs(ax) <= release) target = 0;

        if (tiltState == 0 && target != 0) TryFire(target);
        tiltState = target;
    }

    private void TryFire(int dir)
    {
        if (Time.time - lastTurnTime < cooldown) return;
        lastTurnTime = Time.time;

        if (dir > 0) OnTurnRight?.Invoke();
        else OnTurnLeft?.Invoke();
    }

    public void CalibrateNeutral()
    {
        neutralAccelX = sensor != null ? sensor.Accelerometer.x : 0f;
    }
}
