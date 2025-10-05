public interface ISensorProvider
{
    // Valor de concentración listo (0..1). El juego solo lo lee.
    float Concentration01 { get; }

    // Giro de cabeza relativo al “centro” (grados). Derecha=+, Izquierda=-
    float HeadYawOffsetDeg { get; }

    // Señal de “listo/estable” (útil para no mover hasta que Python esté ok)
    bool IsStable { get; }

    // Marca la postura actual como centro (pone offset ≈ 0)
    void CalibrateNeutralYaw();
}
