import socket, struct, time, math, random

UNITY_IP = "127.0.0.1"
UNITY_PORT = 5005
DT = 0.01

def main():
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    addr = (UNITY_IP, UNITY_PORT)
    t = 0.0
    print(f"SensorMock → {UNITY_IP}:{UNITY_PORT}")
    try:
        while True:
            focus = 1.0 + 0.5*math.sin(t) + 0.2*random.uniform(-1,1)
            sock.sendto(b'F' + struct.pack('<f', float(focus)), addr)

            ax = 0.1*math.sin(0.7*t)
            ay = 0.1*math.cos(0.4*t)
            az = 0.98 + 0.02*math.sin(0.2*t)
            sock.sendto(b'A' + struct.pack('<fff', ax, ay, az), addr)

            t += DT
            time.sleep(DT)
    except KeyboardInterrupt:
        pass
    finally:
        sock.close()

if __name__ == "__main__":
    main()
