#include <Wire.h>
#include "Adafruit_DRV2605.h"


// --- CONFIG ---
const uint8_t NUM_CHANNELS = 4;
const uint8_t TCA_ADDR = 0x70;
const unsigned long SAMPLE_PERIOD = 42667UL;  // 42 ms window (microseconds)
const unsigned long MIN_PULSE_US = 2000UL;    // minimum on/off time (2 ms)
const int adcPin = 1;

// --- DATA ---
Adafruit_DRV2605 drv[NUM_CHANNELS];
uint8_t amplitudes[NUM_CHANNELS];       // 0..127
uint8_t pulsesPerSample[NUM_CHANNELS];  // 0..10 (0 = off)
bool vibrating[NUM_CHANNELS];
unsigned long lastToggleTime[NUM_CHANNELS];
unsigned long onTime[NUM_CHANNELS];
unsigned long offTime[NUM_CHANNELS];

int8_t lastTCASelected = -1;

// Fixed-size binary frame buffer
const uint8_t FRAME_SIZE = NUM_CHANNELS * 2;
uint8_t frameBuf[FRAME_SIZE];
uint8_t framePos = 0;

// --- TCA SELECT ---
void tcaSelect(uint8_t channel) {
  if (channel > 7) return;
  if (lastTCASelected == (int8_t)channel) return;
  Wire.beginTransmission(TCA_ADDR);
  Wire.write(1 << channel);
  Wire.endTransmission();
  lastTCASelected = channel;
}

// --- SETUP ---
void setup() {
  Serial.begin(115200);
  Wire.begin();

#if defined(ARDUINO_ARCH_SAMD) || defined(ESP32) || defined(ARDUINO_TEENSY41) || defined(ARDUINO_TEENSY40) || defined(ARDUINO_TEENSY36) || defined(ARDUINO_TEENSY35)
  analogReadResolution(12);
#endif

  Serial.println();
  Serial.println("Initializing DRV2605 modules (binary stream mode)...");

  for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
    lastTCASelected = -1;
    tcaSelect(i);
    drv[i].begin();

    // Use LRA mode for all channels (change to useERM if needed)
    drv[i].useLRA();

    drv[i].selectLibrary(1);
    drv[i].setMode(DRV2605_MODE_REALTIME);

    uint8_t c3 = drv[i].readRegister8(0x1D);
    c3 |= 0x01;                       // set bit 0 (LRA_OPEN_LOOP = 1)
    drv[i].writeRegister8(0x1D, c3);

    uint8_t lraPeriod = 0x33; // LRA period (measured) 1/f and f for our motor is 160-300hz
    drv[i].writeRegister8(0x20, lraPeriod);  

    amplitudes[i] = 0;
    pulsesPerSample[i] = 0;
    vibrating[i] = false;
    lastToggleTime[i] = micros();
    onTime[i] = 0;
    offTime[i] = 0;

    drv[i].setRealtimeValue(0);
  }

  lastTCASelected = -1;
  Serial.print("Ready. Binary frame size = ");
  Serial.print(FRAME_SIZE);
  Serial.println(" bytes.");
}

// --- FRAME PARSE ---
void parseFrame(const uint8_t* buf) {
  for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
    uint8_t amp = buf[i * 2];
    uint8_t freq = buf[i * 2 + 1];

    if (amp > 127) amp = 127;
    if (freq > 100) freq = 100;

    amplitudes[i] = amp;

    if (freq == 0 || amp == 0) {
      pulsesPerSample[i] = 0;
      onTime[i] = offTime[i] = 0;
      continue;
    }

    // Map 0..100 -> 1..10 pulses
    float targetPulsesF = 1.0f + (freq / 100.0f) * 9.0f;
    uint8_t targetPulses = (uint8_t)round(targetPulsesF);
    if (targetPulses < 1) targetPulses = 1;
    if (targetPulses > 10) targetPulses = 10;

    pulsesPerSample[i] = targetPulses;

    float pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
    unsigned long half = (unsigned long)(pulseTimeF / 2.0f);
    if (half < MIN_PULSE_US) half = MIN_PULSE_US;

    // Ensure we fit into SAMPLE_PERIOD
    unsigned long maxPulsesAllowed = SAMPLE_PERIOD / (2 * half);
    if (maxPulsesAllowed == 0) maxPulsesAllowed = 1;
    if (pulsesPerSample[i] > maxPulsesAllowed) pulsesPerSample[i] = (uint8_t)maxPulsesAllowed;

    // Recompute half using final pulse count
    pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
    half = (unsigned long)(pulseTimeF / 2.0f);
    if (half < MIN_PULSE_US) half = MIN_PULSE_US;

    onTime[i] = half;
    offTime[i] = half;

    vibrating[i] = false;
    lastToggleTime[i] = micros();
  }
}

// --- LOOP ---
void loop() {
  int adcValue = 3000;

  // Read raw bytes into a fixed buffer
  while (Serial.available() > 0) {
    int b = Serial.read();
    if (b < 0) break;
    frameBuf[framePos++] = (uint8_t)b;
    if (framePos >= FRAME_SIZE) {
      parseFrame(frameBuf);
      framePos = 0;
    }
  }

  unsigned long now = micros();

  // Per-channel pulse scheduling
  for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
    if (amplitudes[i] == 0 || pulsesPerSample[i] == 0 || onTime[i] == 0) {
      if (vibrating[i]) {
        tcaSelect(i);
        drv[i].setRealtimeValue(0);
        vibrating[i] = false;
      }
      continue;
    }

    if (vibrating[i]) {
      if (now - lastToggleTime[i] >= onTime[i]) {
        tcaSelect(i);
        drv[i].setRealtimeValue(0);
        vibrating[i] = false;
        lastToggleTime[i] = now;
      }
    } else {
      if (now - lastToggleTime[i] >= offTime[i]) {
        tcaSelect(i);
        drv[i].setRealtimeValue((int)(amplitudes[i] * adcValue / 4095));
        vibrating[i] = true;
        lastToggleTime[i] = now;
      }
    }
  }
}
