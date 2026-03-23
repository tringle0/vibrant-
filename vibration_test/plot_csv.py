import pandas as pd
import matplotlib.pyplot as plt

filename = "vibrationlog2026-03-13_12-55-31.csv"

df = pd.read_csv(filename)

if "elapsed_s" in df.columns:
    x = df["elapsed_s"]
    x_label = "Time (s)"
elif "timestamp" in df.columns:
    try:
        x = pd.to_datetime(df["timestamp"])
        x_label = "Time"
    except:
        x = range(len(df))
        x_label = "Sample Number"
else:
    x = range(len(df))
    x_label = "Sample Number"

if "z" not in df.columns:
    raise ValueError("CSV must contain a 'z' column.")

z_numeric = pd.to_numeric(df["z"], errors="coerce")
valid_mask = z_numeric.notna()
z = z_numeric[valid_mask]

if hasattr(x, "loc"):
    x = x.loc[valid_mask]
else:
    x = [xi for xi, keep in zip(x, valid_mask) if keep]

avg_displacement = z.abs().mean()

print(f"Average displacement from zero: {avg_displacement:.4f}")

plt.figure(figsize=(12, 6))
plt.plot(x, z, label="Z value", linewidth=1)
plt.axhline(0, linestyle="--", linewidth=1, label="Zero")
plt.axhline(avg_displacement, color="red", linestyle="--", linewidth=2,
            label=f"+Average abs displacement = {avg_displacement:.4f}")
plt.axhline(-avg_displacement, color="red", linestyle="--", linewidth=2,
            label=f"-Average abs displacement = {-avg_displacement:.4f}")

plt.title("Vibration Plot")
plt.xlabel(x_label)
plt.ylabel("Z")
plt.legend()
plt.grid(True)
plt.tight_layout()
plt.show()