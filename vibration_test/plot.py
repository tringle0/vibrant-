import serial
import datetime
import os
 
PORT = "COM15"       # change to your port (Mac/Linux: "/dev/ttyACM0" or "/dev/cu.usbmodem...")
BAUD = 115200
 
timestamp = datetime.datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
filename = f"vibrationlog{timestamp}.csv"
 
print(f"Logging to {filename}")
print("Press Ctrl+C to stop.\n")
 
ser = serial.Serial(PORT, BAUD, timeout=1)
 
with open(filename, "w") as f:
    f.write("timestamp,z\n")
    while True:
        line = ser.readline().decode("utf-8", errors="ignore").strip()
        if line:
            now = datetime.datetime.now().strftime("%H:%M:%S.%f")[:-3]
            f.write(f"{now},{line}\n")
            f.flush()
            print(f"{now}  {line}")