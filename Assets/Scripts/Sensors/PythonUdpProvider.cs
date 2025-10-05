using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class PythonUDPProvider : MonoBehaviour, ISensorProvider
{
    [Header("UDP")]
    public int port = 5005;

    [Header("Datos (Inspector)")]
    [SerializeField] private float focus;     // copia para mostrar
    [SerializeField] private Vector3 accel;   // copia para mostrar
    [SerializeField] private bool isStable;   // copia para mostrar

    // ----- valores RAW que escribe el hilo -----
    private volatile float focusRaw = 0f;
    private volatile float axRaw = 0f, ayRaw = 0f, azRaw = 0f;
    private volatile bool stableRaw = false;
    private volatile float lastPacketTime = 0f; // Time.realtimeSinceStartup

    // ----- anclas de normalización (0..1) -----
    // Puedes exponerlos con [SerializeField] si quieres verlos en el inspector
    private volatile float fRelax = 0.8f;  // estimado inicial
    private volatile float fFocus = 1.2f;  // estimado inicial

    // ===== ISensorProvider =====
    public float Concentration01
        => Mathf.Clamp01((focusRaw - fRelax) / Mathf.Max(0.05f, fFocus - fRelax));
    public bool IsStable => stableRaw;
    public Vector3 Accelerometer => new Vector3(axRaw, ayRaw, azRaw);
    public void CalibrateNeutral() { /* noop aquí (lo usa el detector) */ }

    // Calibraciones para anclas (llámalas desde UI/teclas)
    public void CalibrateRelax()  { fRelax = focusRaw; }
    public void CalibrateFocus()  { fFocus = Mathf.Max(fRelax + 0.05f, focusRaw); }

    // ===== Internos UDP =====
    private UdpClient _client;
    private Thread _thread;
    private volatile bool _running;

    void Start()
    {
        try
        {
            Debug.Log($"[UDP] Trying to bind port {port}");
            _client = new UdpClient(port);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
            Debug.Log("[UDP] Receiver started");
        }
        catch (Exception e)
        {
            stableRaw = false;
            Debug.LogError($"PythonUDPProvider: cannot bind UDP {port}. {e.Message}");
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
    }

    void Update()
    {
        // Copia a campos serializados para ver en Inspector
        focus = focusRaw;
        accel = new Vector3(axRaw, ayRaw, azRaw);
        isStable = stableRaw;

        // Timeout de estabilidad (si no llegan paquetes por > 0.5s)
        if (Time.realtimeSinceStartup - lastPacketTime > 0.5f)
            stableRaw = false;

        // Teclas de calibración (opcionales)
        if (Input.GetKeyDown(KeyCode.R)) { CalibrateRelax();  Debug.Log("[UDP] CalibrateRelax"); }
        if (Input.GetKeyDown(KeyCode.F)) { CalibrateFocus();  Debug.Log("[UDP] CalibrateFocus"); }
    }

    private void ReceiveLoop()
    {
        IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);

        while (_running)
        {
            try
            {
                var data = _client.Receive(ref ep);
                if (data == null || data.Length == 0) continue;

                byte tag = data[0];

                if (tag == (byte)'F' && data.Length == 5)
                {
                    focusRaw = BitConverter.ToSingle(data, 1);
                    stableRaw = true;
                    lastPacketTime = Time.realtimeSinceStartup;
                    // Debug.Log($"[UDP] Focus {focusRaw:F3}");
                }
                else if (tag == (byte)'A' && data.Length == 13)
                {
                    axRaw = BitConverter.ToSingle(data, 1);
                    ayRaw = BitConverter.ToSingle(data, 5);
                    azRaw = BitConverter.ToSingle(data, 9);
                    stableRaw = true;
                    lastPacketTime = Time.realtimeSinceStartup;
                    // Debug.Log($"[UDP] Accel ({axRaw:F2},{ayRaw:F2},{azRaw:F2})");
                }
                // Otros tamaños/tags se ignoran
            }
            catch (SocketException)
            {
                // cierre limpio al salir
            }
            catch (Exception ex)
            {
                stableRaw = false;
                Debug.LogWarning($"PythonUDPProvider UDP error: {ex.Message}");
            }
        }
    }
}
