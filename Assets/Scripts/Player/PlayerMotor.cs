using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MonoBehaviour sensorBehaviour; // Arrastra aquí el proveedor (Mock o PythonUdpProvider)
    private ISensorProvider sensor;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;          // Velocidad constante
    [SerializeField, Range(0f,1f)] private float concThreshold = 0.75f; // Umbral para avanzar

    [Header("Giro por cabeza")]
    [SerializeField] private float yawSensitivity = 2.0f;   // deg/seg por cada grado de cabeza
    [SerializeField] private float maxYawOffsetAbs = 35f;   // Límite de entrada (clamp)

    // Debug / estado
    [SerializeField] private bool gateActive = false;       // true = avanza
    [SerializeField] private float currentAngleDeg = 90f;   // 90 = mirar hacia arriba en 2D

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        sensor = sensorBehaviour as ISensorProvider;
        if (sensor == null)
            Debug.LogError("PlayerMotor: 'sensorBehaviour' no implementa ISensorProvider.");
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void Update()
    {
        // 1) Integrar el giro (steering continuo por yaw)
        float yaw = Mathf.Clamp(sensor?.HeadYawOffsetDeg ?? 0f, -maxYawOffsetAbs, maxYawOffsetAbs);
        float turnRateDegPerSec = yaw * yawSensitivity;                // deg/s
        currentAngleDeg += turnRateDegPerSec * Time.deltaTime;         // integrar

        // 2) Abrir/cerrar compuerta por concentración (simple y directo)
        float conc = Mathf.Clamp01(sensor?.Concentration01 ?? 0f);
        gateActive = (sensor != null) && sensor.IsStable && (conc >= concThreshold);
    }

    void FixedUpdate()
    {
        // Dirección actual
        float rad = currentAngleDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));

        // Avanzar a velocidad constante solo si gate activo
        Vector2 vel = gateActive ? forward * moveSpeed : Vector2.zero;
        rb.MovePosition(rb.position + vel * Time.fixedDeltaTime);

        // Alinear sprite con la dirección (opcional)
        rb.SetRotation(currentAngleDeg - 90f);
    }

    // Botón de UI → recalibrar el "centro" de la cabeza
    public void CalibrateNeutral() => sensor?.CalibrateNeutralYaw();

    // --- Propiedades de solo lectura para HUD/depuración ---
    public bool GateActive => gateActive;
    public float ConcThreshold => concThreshold;
}
