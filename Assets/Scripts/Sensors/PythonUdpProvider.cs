using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

/// Recibe JSON por UDP desde Python y expone conc/yaw/stable a través de ISensorProvider.
/// Formato esperado por paquete: {"conc":0.82,"yaw_deg":12.5,"stable":true}
[DisallowMultipleComponent]
public class PythonUdpProvider : MonoBehaviour, ISensorProvider
{
    [Header("UDP")]
    [Tooltip("Puerto UDP que escucha Unity.")]
    public int listenPort = 5005;

    [Tooltip("True = solo 127.0.0.1; False = todas las interfaces.")]
    public bool localhostOnly = true;

    [Header("Valores recibidos (solo lectura)")]
    [Range(0f,1f)] [SerializeField] private float concentration01 = 0f;
    [SerializeField] private float headYawOffsetDeg = 0f;
    [SerializeField] private bool isStable = true; // si Python no lo manda, asumimos true

    // Offset local para recalibrar visualmente sin pedirle a Python
    private float localYawZero = 0f;

    // ISensorProvider
    public float Concentration01 => concentration01;
    public float HeadYawOffsetDeg => headYawOffsetDeg - localYawZero;
    public bool  IsStable => isStable;
    public void CalibrateNeutralYaw() => localYawZero = headYawOffsetDeg;

    // Red
    private UdpClient udp;
    private Thread recvThread;
    private volatile bool running;

    // Clase para parsear JSON simple
    [System.Serializable]
    private class MsgSimple
    {
        public float conc;
        public float yaw_deg;
        public bool stable;
    }

    void Start()
    {
        try
        {
            IPEndPoint ep = localhostOnly
                ? new IPEndPoint(IPAddress.Loopback, listenPort)
                : new IPEndPoint(IPAddress.Any, listenPort);

            udp = new UdpClient(ep);
            running = true;

            recvThread = new Thread(ReceiveLoop) { IsBackground = true };
            recvThread.Start();

            Debug.Log($"[PythonUdpProvider] Escuchando UDP en {ep.Address}:{listenPort}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError("[PythonUdpProvider] No se pudo abrir el puerto UDP: " + ex.Message);
            enabled = false;
        }
    }

    private void ReceiveLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);

        while (running)
        {
            try
            {
                byte[] data = udp.Receive(ref any);
                if (data == null || data.Length == 0) continue;

                string json = Encoding.UTF8.GetString(data).Trim();

                // Limpiar por si llegan saltos de línea u otros caracteres
                int a = json.IndexOf('{');
                int b = json.LastIndexOf('}');
                if (a >= 0 && b > a) json = json.Substring(a, b - a + 1);

                var msg = JsonUtility.FromJson<MsgSimple>(json);
                if (msg != null)
                {
                    // Asignamos directamente (tipos primitivos son seguros aquí)
                    concentration01   = Mathf.Clamp01(msg.conc);
                    headYawOffsetDeg  = msg.yaw_deg;
                    isStable          = msg.stable;
                }
            }
            catch (SocketException)
            {
                // Cierra limpio cuando se destruye
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[PythonUdpProvider] Paquete inválido: " + ex.Message);
            }
        }
    }

    void OnDestroy()
    {
        running = false;
        try { udp?.Close(); } catch { }
        try { recvThread?.Join(100); } catch { }
    }
}
