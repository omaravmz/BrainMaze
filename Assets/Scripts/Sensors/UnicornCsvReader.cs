using System.Collections;
using System.Globalization;
using System.IO;
using UnityEngine;

/// Lee un CSV (append) producido por Python y entrega conc/yaw/stable a través de ISensorProvider.
/// No procesa señales: asume que Python ya normaliza conc (0..1) y calcula yaw_deg relativo a un neutro.
[DisallowMultipleComponent]
public class UnicornCsvReader : MonoBehaviour, ISensorProvider
{
    [Header("Archivo CSV (producido por Python)")]
    [Tooltip("Ruta absoluta o relativa al ejecutable/Carpeta del proyecto. Ej: C:/data/unicorn_out.csv")]
    public string filePath;

    [Tooltip("¿La primera fila trae nombres de columna? (recomendado)")]
    public bool hasHeader = true;

    [Tooltip("Frecuencia de lectura (seg). 0.05 = 20 Hz")]
    [Range(0.02f, 0.5f)] public float pollIntervalSec = 0.05f;

    [Header("Columnas (si hay header)")]
    public string concColumnName = "conc";
    public string yawColumnName  = "yaw_deg";
    public string stableColumnName = "stable";

    [Header("Columnas (si NO hay header) - índices base 0")]
    public int concColumnIndex = 1;   // ejemplo: 0=ts_ms, 1=conc, 2=yaw_deg, 3=stable
    public int yawColumnIndex  = 2;
    public int stableColumnIndex = 3;

    [Header("Yaw local")]
    [Tooltip("Aplicar un cero local al yaw recibido, sin pedir recalibración a Python.")]
    public bool applyLocalZero = true;

    // ---- ISensorProvider ----
    [SerializeField, Range(0f,1f)] private float concentration01 = 0f;
    [SerializeField]               private float headYawOffsetDeg = 0f;
    [SerializeField]               private bool  isStable = false;

    public float Concentration01 => concentration01;
    public float HeadYawOffsetDeg => headYawOffsetDeg - (applyLocalZero ? localYawZero : 0f);
    public bool  IsStable => isStable;

    public void CalibrateNeutralYaw()
    {
        // Toma el yaw actual como nuevo cero local
        localYawZero = headYawOffsetDeg;
    }

    // ---- internos ----
    private int concIdx = -1, yawIdx = -1, stabIdx = -1;
    private float localYawZero = 0f;
    private char detectedSep = ',';
    private bool headerParsed = false;

    void Start()
    {
        // Normaliza ruta relativa a la carpeta del proyecto si no es absoluta.
        if (!Path.IsPathRooted(filePath))
            filePath = Path.Combine(Application.dataPath, "..", filePath);

        StartCoroutine(ReadLoop());
    }

    IEnumerator ReadLoop()
    {
        var wait = new WaitForSeconds(pollIntervalSec);
        while (true)
        {
            TryReadLastLine();
            yield return wait;
        }
    }

    private void TryReadLastLine()
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        try
        {
            if (!File.Exists(filePath)) return;

            // Leemos todo y nos quedamos con la última línea no vacía (suficiente para jam/prototipo).
            // Si el archivo crece mucho, se puede optimizar con un tail incremental.
            string[] lines = File.ReadAllLines(filePath);
            if (lines == null || lines.Length == 0) return;

            int last = lines.Length - 1;

            // Detectar encabezado si aplica
            if (hasHeader && !headerParsed && lines.Length >= 2)
            {
                ParseHeader(lines[0]);
                headerParsed = true;
            }

            // Buscar última línea no vacía (por si quedó un salto final)
            while (last >= 0 && string.IsNullOrWhiteSpace(lines[last])) last--;
            if (last < 0) return;

            string line = lines[last];

            // Detectar separador si hace falta
            if (!headerParsed && !hasHeader)
                detectedSep = DetectSep(line);

            ParseDataLine(line);
        }
        catch (IOException)
        {
            // archivo en uso/lock momentáneo: ignorar y reintentar en el próximo tick
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[UnicornCsvReader] Error leyendo CSV: {ex.Message}");
        }
    }

    private void ParseHeader(string headerLine)
    {
        detectedSep = DetectSep(headerLine);
        var cols = headerLine.Split(detectedSep);
        for (int i = 0; i < cols.Length; i++)
        {
            string name = cols[i].Trim().Trim('"').ToLowerInvariant();
            if (name == concColumnName.ToLowerInvariant())  concIdx = i;
            if (name == yawColumnName.ToLowerInvariant())   yawIdx  = i;
            if (name == stableColumnName.ToLowerInvariant()) stabIdx = i;
        }
        // Fallback: si no encontró, usa índices por defecto
        if (concIdx < 0) concIdx = concColumnIndex;
        if (yawIdx  < 0) yawIdx  = yawColumnIndex;
        if (stabIdx < 0) stabIdx = stableColumnIndex;
    }

    private void ParseDataLine(string line)
    {
        var parts = line.Split(detectedSep);
        if (parts.Length == 0) return;

        // Si no hay encabezado, usa los índices configurados
        int ci = hasHeader ? concIdx : concColumnIndex;
        int yi = hasHeader ? yawIdx  : yawColumnIndex;
        int si = hasHeader ? stabIdx : stableColumnIndex;

        // Guardas defensivas
        if (ci < 0 || ci >= parts.Length) return;
        if (yi < 0 || yi >= parts.Length) return;

        // Parse invariante (puntos decimales). También quitamos comillas si vienen.
        string sConc = San(parts[ci]);
        string sYaw  = San(parts[yi]);
        string sStab = (si >= 0 && si < parts.Length) ? San(parts[si]) : "true";

        // Sustituir coma decimal por punto (por si Python lo escribe localmente distinto)
        sConc = sConc.Replace(',', '.');
        sYaw  = sYaw.Replace(',', '.');

        if (float.TryParse(sConc, NumberStyles.Float, CultureInfo.InvariantCulture, out float conc))
            concentration01 = Mathf.Clamp01(conc);

        if (float.TryParse(sYaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float yaw))
            headYawOffsetDeg = yaw;

        // stable admite true/false o 0/1
        isStable = ParseBoolFlexible(sStab);
    }

    private static char DetectSep(string sample)
    {
        // Prioridad ; luego , (o lo que más aparezca)
        int commas = 0, semis = 0, tabs = 0;
        foreach (char c in sample)
        {
            if (c == ',') commas++;
            else if (c == ';') semis++;
            else if (c == '\t') tabs++;
        }
        if (semis > commas && semis >= tabs) return ';';
        if (tabs  > commas && tabs  >= semis) return '\t';
        return ',';
    }

    private static string San(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim().Trim('"', '\'').ToLowerInvariant();

    private static bool ParseBoolFlexible(string s)
    {
        if (string.IsNullOrEmpty(s)) return true;
        if (s == "1" || s == "true" || s == "yes" || s == "y") return true;
        if (s == "0" || s == "false" || s == "no"  || s == "n") return false;

        // Intento por número
        if (float.TryParse(s.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            return f != 0f;

        return true; // por defecto, estable
    }
}
