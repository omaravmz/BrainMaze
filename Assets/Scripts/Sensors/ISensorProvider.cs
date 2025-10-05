using UnityEngine;

public interface ISensorProvider
{
    // Concentración normalizada [0..1]
    float Concentration01 { get; }

    // Estado válido de la señal / conexión
    bool IsStable { get; }

    // Acelerómetro (idealmente en g)
    Vector3 Accelerometer { get; }

    // Calibración de neutro (si aplica en el provider)
    void CalibrateNeutral();
}
