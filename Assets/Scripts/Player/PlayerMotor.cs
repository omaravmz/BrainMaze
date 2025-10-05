using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private MonoBehaviour sensorBehaviour;          // Debe implementar ISensorProvider
    [SerializeField] private AcelerometerTurnDetector turnDetector;  // Detector de giros por acelerómetro
    private ISensorProvider sensor;

    [Header("Movimiento")]
    [SerializeField] private float moveSpeed = 4f;                   // Velocidad constante
    [SerializeField, Range(0f,1f)] private float concThreshold = 0.75f;

    // Estado
    [SerializeField] private bool gateActive = false;                // true = avanza
    [SerializeField] private float currentAngleDeg = 90f;            // 90 = hacia arriba en 2D

    void Awake()
    {
        if (!rb) rb = GetComponent<Rigidbody2D>();
        sensor = sensorBehaviour as ISensorProvider;
        if (sensor == null) Debug.LogError("PlayerMotor: 'sensorBehaviour' no implementa ISensorProvider.");

        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    void OnEnable()
    {
        if (turnDetector != null)
        {
            turnDetector.OnTurnLeft  += TurnLeft;
            turnDetector.OnTurnRight += TurnRight;
        }
    }

    void OnDisable()
    {
        if (turnDetector != null)
        {
            turnDetector.OnTurnLeft  -= TurnLeft;
            turnDetector.OnTurnRight -= TurnRight;
        }
    }

    void Update()
    {
        // Compuerta por concentración
        float conc = Mathf.Clamp01(sensor?.Concentration01 ?? 0f);
        gateActive = (sensor != null) && sensor.IsStable && (conc >= concThreshold);
    }

    void FixedUpdate()
    {
        float rad = currentAngleDeg * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
        Vector2 vel = gateActive ? forward * moveSpeed : Vector2.zero;

        rb.MovePosition(rb.position + vel * Time.fixedDeltaTime);
        rb.SetRotation(currentAngleDeg - 90f); // opcional: alinear sprite
    }

    private void TurnLeft()  => currentAngleDeg = Mathf.Repeat(currentAngleDeg - 90f, 360f);
    private void TurnRight() => currentAngleDeg = Mathf.Repeat(currentAngleDeg + 90f, 360f);

    // Botón UI para recalibrar neutro (acelerómetro y proveedor si aplica)
    public void CalibrateNeutral()
    {
        turnDetector?.CalibrateNeutral();
        sensor?.CalibrateNeutral();
    }

    // Lectura para HUD
    public bool GateActive => gateActive;
    public float ConcThreshold => concThreshold;
}
