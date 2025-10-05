using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class PythonUDPProvider : MonoBehaviour, ISensorProvider
{
    [Header("UDP")]
    public int port = 5005;

    [Header("Datos (solo lectura)")]
    [SerializeField] private float focus = 0f;           // recibido como float32 (tag 'F')
    [SerializeField] private Vector3 accel = Vector3.zero; // recibido como 3×float32 (tag 'A')
    [SerializeField] private bool isStable = true;

    private UdpClient _client;
    private Thread _thread;
    private volatile bool _running;

    // ISensorProvider
    public float Concentration01 => Mathf.Clamp01(focus);
    public bool IsStable => isStable;
    public Vector3 Accelerometer => accel;
    public void CalibrateNeutral() { /* no-op aquí; el detector maneja su neutro */ }

    void Start()
    {
        try
        {
            _client = new UdpClient(port);
            _running = true;
            _thread = new Thread(ReceiveLoop) { IsBackground = true };
            _thread.Start();
        }
        catch (Exception e)
        {
            isStable = false;
            Debug.LogError($"PythonUDPProvider: no se pudo abrir UDP {port}. {e.Message}");
        }
    }

    void OnDestroy()
    {
        _running = false;
        try { _client?.Close(); } catch { }
        try { _thread?.Join(200); } catch { }
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
                    focus = BitConverter.ToSingle(data, 1);
                    isStable = true;
                }
                else if (tag == (byte)'A' && data.Length == 13)
                {
                    float ax = BitConverter.ToSingle(data, 1);
                    float ay = BitConverter.ToSingle(data, 5);
                    float az = BitConverter.ToSingle(data, 9);
                    accel = new Vector3(ax, ay, az);
                    isStable = true;
                }
            }
            catch (SocketException) { /* cerrado al destruir */ }
            catch (Exception ex)
            {
                isStable = false;
                Debug.LogWarning($"PythonUDPProvider UDP error: {ex.Message}");
            }
        }
    }
}
