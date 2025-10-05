# file: Unicorn_stream.py
# -- coding: utf-8 --
import UnicornPy
import struct
import time
import socket
import sys
from collections import deque
import numpy as np

# ============== Config ==============
UNITY_IP = "127.0.0.1"
UNITY_PORT = 5005
SEND_INTERVAL = 0.01  # s

FS = 250.0
EEG_CHANNELS = 8
ALPHA_BAND = (8.0, 13.0)
BETA_BAND  = (13.0, 30.0)

WIN_SEC = 1.0
N_PERSEG = int(FS * WIN_SEC)
OVERLAP = 0.5
STEP = max(1, int(N_PERSEG * (1.0 - OVERLAP)))
NUM_WINDOWS_TO_AVG = 3
BUFFER_SAMPLES = N_PERSEG + (NUM_WINDOWS_TO_AVG - 1) * STEP

EMA_ALPHA = 0.3
EPS = 1e-12

# ============== PSD utils ==============
def _hamming(n: int) -> np.ndarray:
    if n <= 1:
        return np.ones(n, dtype=np.float64)
    return 0.54 - 0.46 * np.cos(2.0 * np.pi * np.arange(n) / (n - 1))

def welch_psd_multichannel(x: np.ndarray, fs: float, nperseg: int, step: int):
    n_samples, n_ch = x.shape
    if n_samples < nperseg:
        return None, None

    win = _hamming(nperseg).astype(np.float64)
    win_norm = (win**2).sum()

    starts = np.arange(0, n_samples - nperseg + 1, step)
    if len(starts) == 0:
        return None, None

    fft_len = nperseg // 2 + 1
    Pxx = np.zeros((fft_len, n_ch), dtype=np.float64)

    for s in starts:
        seg = x[s:s+nperseg, :]
        seg = seg - seg.mean(axis=0, keepdims=True)
        seg_win = seg * win[:, np.newaxis]
        X = np.fft.rfft(seg_win, axis=0)
        P = (np.abs(X)**2) / (win_norm * fs)
        Pxx += P

    Pxx /= len(starts)
    freqs = np.fft.rfftfreq(nperseg, d=1.0/fs)
    return freqs, Pxx

def bandpower_from_psd(freqs: np.ndarray, Pxx: np.ndarray, fmin: float, fmax: float):
    if freqs is None or Pxx is None:
        return None
    mask = (freqs >= fmin) & (freqs <= fmax)
    if not np.any(mask):
        return np.zeros(Pxx.shape[1], dtype=np.float64)
    return np.trapz(Pxx[mask, :], freqs[mask], axis=0)

def compute_focus_relax(eeg_block_uv: np.ndarray):
    if eeg_block_uv.shape[0] < N_PERSEG:
        return None
    freqs, Pxx = welch_psd_multichannel(eeg_block_uv, FS, N_PERSEG, STEP)
    if freqs is None:
        return None
    alpha = bandpower_from_psd(freqs, Pxx, *ALPHA_BAND)
    beta  = bandpower_from_psd(freqs, Pxx, *BETA_BAND)
    if alpha is None or beta is None:
        return None
    alpha_mean = float(np.mean(alpha))
    beta_mean  = float(np.mean(beta))
    focus = beta_mean / (alpha_mean + EPS)              # mayor beta/alpha = mayor focus
    relax = alpha_mean / (alpha_mean + beta_mean + EPS) # no se envía, pero queda por consistencia
    return focus, relax, alpha_mean, beta_mean

# ============== UDP send ==============
def send_focus(sock: socket.socket, focus: float, addr):
    try:
        sock.sendto(b'F' + struct.pack('<f', float(focus)), addr)
    except Exception as e:
        print(f"[UDP] Focus error: {e}")

def send_accel(sock: socket.socket, accel_xyz, addr):
    try:
        ax, ay, az = float(accel_xyz[0]), float(accel_xyz[1]), float(accel_xyz[2])
        sock.sendto(b'A' + struct.pack('<fff', ax, ay, az), addr)
    except Exception as e:
        print(f"[UDP] Accel error: {e}")

# ============== Main loop ==============
def main():
    print("Buscando dispositivos Unicorn...")
    try:
        devices = UnicornPy.GetAvailableDevices(True)
    except Exception as e:
        print(f"Error al buscar dispositivos: {e}")
        sys.exit(1)
    if not devices:
        print("No se encontraron dispositivos Unicorn.")
        sys.exit(1)

    name = devices[0]
    print(f"Conectando con {name}...")
    try:
        device = UnicornPy.Unicorn(name)
    except Exception as e:
        print("Error al conectar:", e)
        sys.exit(1)

    try:
        info = device.GetDeviceInformation()
        print(f"Dispositivo: {info.Serial} | Firmware: {info.FWVersion}")
    except Exception:
        pass

    num_channels = device.GetNumberOfAcquiredChannels()
    print("Canales adquiridos:", num_channels)
    actual_eeg_channels = min(EEG_CHANNELS, num_channels)
    if actual_eeg_channels < EEG_CHANNELS:
        print(f"ADVERTENCIA: solo {actual_eeg_channels} canales EEG (relleno con 0).")

    num_scans = 1
    frame_bytes = num_scans * num_channels * 4
    data_buffer = bytearray(frame_bytes)

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    addr = (UNITY_IP, UNITY_PORT)
    print(f"Enviando UDP binario a {UNITY_IP}:{UNITY_PORT}")

    eeg_ring = deque(maxlen=BUFFER_SAMPLES)
    ema_focus = None
    samples_since_last_psd = 0

    try:
        device.StartAcquisition(False)
        print("Adquisición iniciada. Ctrl+C para detener.")

        while True:
            device.GetData(num_scans, data_buffer, len(data_buffer))
            frame = struct.unpack('f' * num_channels, data_buffer)

            # EEG 0..7 (o menos → rellena a 8)
            eeg = list(frame[:actual_eeg_channels]) + [0.0] * (EEG_CHANNELS - actual_eeg_channels)
            eeg_ring.append(np.array(eeg, dtype=np.float64))
            samples_since_last_psd += 1

            # IMU accel 8..10 (si existen)
            accel = frame[8:11] if num_channels >= 11 else (0.0, 0.0, 0.0)
            send_accel(sock, accel, addr)  # baja latencia (cada iteración)

            # Focus cuando haya suficientes muestras + STEP
            if len(eeg_ring) >= BUFFER_SAMPLES and samples_since_last_psd >= STEP:
                eeg_block = np.vstack(eeg_ring)
                out = compute_focus_relax(eeg_block)
                if out is not None:
                    focus, _, _, _ = out
                    ema_focus = focus if ema_focus is None else (1-EMA_ALPHA)*ema_focus + EMA_ALPHA*focus
                    send_focus(sock, ema_focus, addr)
                samples_since_last_psd = 0

            time.sleep(SEND_INTERVAL)

    except KeyboardInterrupt:
        print("\nInterrumpido por el usuario.")
    except Exception as e:
        print("Error:", repr(e))
    finally:
        try:
            device.StopAcquisition()
            del device
        except Exception:
            pass
        sock.close()
        print("Recursos liberados.")

if __name__ == "__main__":
    main()
