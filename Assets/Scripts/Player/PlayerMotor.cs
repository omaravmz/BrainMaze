using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MonoBehaviour sensorBehaviour; // arrastra aquí tu proveedor (p.ej. SensorMock)
    private ISensorProvider sensor;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;      // velocidad constante
    [SerializeField] private float concThreshold = 0.75f; // umbral para avanzar

    [Header("Giro por cabeza")]
    [SerializeField] private float yawSensitivity = 2.0f; // deg/s por cada grado de cabeza
    [SerializeField] private float maxYawOffsetAbs = 35f; // límite del offset en grados

    // Debug
    [SerializeField] private bool gateActive;
    [SerializeField] private float currentAngleDeg;

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        sensor = sensorBehaviour as ISensorProvider;
        if (sensor == null)
            Debug.LogError("PlayerMotor: 'sensorBehaviour' no implementa ISensorProvider.");
    }

    void Update()
    {
        // 1) Girar según la cabeza (steering continuo)
        float yaw = Mathf.Clamp(sensor?.HeadYawOffsetDeg ?? 0f, -maxYawOffsetAbs, maxYawOffsetAbs);
        float turnRate = yaw * yawSensitivity;          // grados/seg
        currentAngleDeg += turnRate * Time.deltaTime;

        // 2) Abrir/cerrar compuerta de avance por concentración (sin suavizado, simple)
        float conc = Mathf.Clamp01(sensor?.Concentration01 ?? 0f);
        gateActive = sensor != null && sensor.IsStable && conc >= concThreshold;
    }

    void FixedUpdate()
    {
        // Dirección de avance a partir del ángulo
        float rad = currentAngleDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // Velocidad constante solo si gate activo
        Vector2 vel = gateActive ? forward * moveSpeed : Vector2.zero;
        rb.MovePosition(rb.position + vel * Time.fixedDeltaTime);

        // (Opcional) alinear sprite con la dirección
        rb.SetRotation(currentAngleDeg - 90f);
    }

    // Llama esto desde un botón si quieres recalibrar el centro
    public void CalibrateNeutral() => sensor?.CalibrateNeutralYaw();
}
