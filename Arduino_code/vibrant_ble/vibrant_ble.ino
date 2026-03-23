#include <Wire.h>
#include "Adafruit_DRV2605.h"
#include <math.h>
#include <NimBLEDevice.h>

// ==========================================================
// BLE (NimBLE) binary streaming version
// Packet format:
//   0x01 = PING (no payload)
//   0x02 = START [u32 device_ms]
//   0x03 = FRAME [amp0][freq0]...[ampN][freqN]
//   0x04 = STOP (no payload)
//   0x81 = PONG [u32 device_ms]
// Each amp/freq field is 1 byte:
//   amp  = 0..127
//   freq = 0..100
// ==========================================================

// --- CONFIG ---
const uint8_t NUM_CHANNELS = 4;
const uint8_t TCA_ADDR = 0x70;
const unsigned long SAMPLE_PERIOD = 42667UL;  // 42.667 ms window (microseconds)
const unsigned long MIN_PULSE_US = 2000UL;    // minimum on/off time (2 ms)
const int adcPin = 1;

// BLE UUIDs
static const char* DEVICE_NAME = "VibrantBLE";
static const char* SERVICE_UUID = "6E400001-B5A3-F393-E0A9-E50E24DCCA9E";
static const char* RX_CHAR_UUID = "6E400002-B5A3-F393-E0A9-E50E24DCCA9E"; // write
static const char* TX_CHAR_UUID = "6E400003-B5A3-F393-E0A9-E50E24DCCA9E"; // notify

// Packet types
const uint8_t PKT_PING  = 0x01;
const uint8_t PKT_START = 0x02;
const uint8_t PKT_FRAME = 0x03;
const uint8_t PKT_STOP  = 0x04;
const uint8_t PKT_PONG  = 0x81;

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

// BLE TX characteristic for PONG responses
NimBLECharacteristic* txChar = nullptr;

// Simple frame queue for timing-correct playback
const uint8_t QUEUE_LEN = 64;
uint8_t frameQueue[QUEUE_LEN][FRAME_SIZE];
uint8_t queueHead = 0;
uint8_t queueTail = 0;
uint8_t queueCount = 0;

bool playing = false;
bool playStarted = false;
uint32_t startTimeMs = 0;
uint32_t nextFrameUs = 0;

// --- TCA SELECT ---
void tcaSelect(uint8_t channel) {
  if (channel > 7) return;
  if (lastTCASelected == (int8_t)channel) return;
  Wire.beginTransmission(TCA_ADDR);
  Wire.write(1 << channel);
  Wire.endTransmission();
  lastTCASelected = channel;
}

float mapLog(float freq, float fMin, float fMax, float pMin, float pMax) {
  if (freq < fMin) freq = fMin;
  if (freq > fMax) freq = fMax;

  float t = log10f(freq / fMin) / log10f(fMax / fMin);
  return pMin + t * (pMax - pMin);
}

// --- FRAME PARSE ---
void parseFrame(const uint8_t* buf) {
  for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
    uint8_t amp = buf[i * 2];
    uint8_t freq = buf[i * 2 + 1];
    Serial.println(freq);

    if (amp > 127) amp = 127;
    if (freq > 100) freq = 100;

    amplitudes[i] = amp;

    if (freq == 0 || amp == 0) {
      pulsesPerSample[i] = 0;
      onTime[i] = offTime[i] = 0;
      continue;
    }

    float targetPulsesF = mapLog(freq, 1.0f, 1000.0f, 1.0f, 10.0f);
    uint8_t targetPulses = (uint8_t)round(targetPulsesF);
    if (targetPulses < 1) targetPulses = 1;
    if (targetPulses > 10) targetPulses = 10;
    
    pulsesPerSample[i] = targetPulses;

    float pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
    unsigned long on_half = (unsigned long)(pulseTimeF / 2);
    unsigned long off_half = (unsigned long)(pulseTimeF / 2);

    if (on_half < MIN_PULSE_US) on_half = MIN_PULSE_US;
    if (off_half < MIN_PULSE_US) off_half = MIN_PULSE_US;

    unsigned long maxPulsesAllowed = SAMPLE_PERIOD / (on_half + off_half);
    if (maxPulsesAllowed == 0) maxPulsesAllowed = 1;
    if (pulsesPerSample[i] > maxPulsesAllowed) pulsesPerSample[i] = (uint8_t)maxPulsesAllowed;

    pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
    //on_half = (unsigned long)(pulseTimeF / 10.0f * 9);
    //off_half = (unsigned long)(pulseTimeF / 10.0f);
    if (on_half < MIN_PULSE_US) on_half = MIN_PULSE_US;
    if (off_half < MIN_PULSE_US) off_half = MIN_PULSE_US;
    

    onTime[i] = on_half;
    offTime[i] = off_half;

    vibrating[i] = false;
    lastToggleTime[i] = micros();
  }
}

void clearQueue() {
  queueHead = 0;
  queueTail = 0;
  queueCount = 0;
}

bool enqueueFrame(const uint8_t* buf) {
  if (queueCount >= QUEUE_LEN) return false;
  memcpy(frameQueue[queueTail], buf, FRAME_SIZE);
  queueTail = (uint8_t)((queueTail + 1) % QUEUE_LEN);
  queueCount++;
  return true;
}

bool dequeueFrame(uint8_t* out) {
  if (queueCount == 0) return false;
  memcpy(out, frameQueue[queueHead], FRAME_SIZE);
  queueHead = (uint8_t)((queueHead + 1) % QUEUE_LEN);
  queueCount--;
  return true;
}

void stopAllMotors() {
  for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
    amplitudes[i] = 0;
    pulsesPerSample[i] = 0;
    onTime[i] = 0;
    offTime[i] = 0;
    if (vibrating[i]) {
      tcaSelect(i);
      drv[i].setRealtimeValue(0);
      vibrating[i] = false;
    }
  }
}

void sendPong() {
  if (!txChar) return;
  uint8_t pkt[5];
  pkt[0] = PKT_PONG;
  uint32_t nowMs = millis();
  memcpy(pkt + 1, &nowMs, 4);
  txChar->setValue(pkt, sizeof(pkt));
  txChar->notify();
}

// --- BLE CALLBACK ---
class RxCallbacks : public NimBLECharacteristicCallbacks {
  void onWrite(NimBLECharacteristic* pCharacteristic, NimBLEConnInfo& connInfo) override {
    std::string value = pCharacteristic->getValue();
    if (value.empty()) return;

    const uint8_t* data = (const uint8_t*)value.data();
    size_t len = value.length();
    size_t i = 0;

    while (i < len) {
      uint8_t type = data[i++];

      if (type == PKT_PING) {
        sendPong();
        continue;
      }

      if (type == PKT_STOP) {
        playing = false;
        playStarted = false;
        clearQueue();
        stopAllMotors();
        continue;
      }

      if (type == PKT_START) {
        if (i + 4 > len) break;
        uint32_t tMs = 0;
        memcpy(&tMs, data + i, 4);
        i += 4;
        clearQueue();
        playing = true;
        playStarted = false;
        startTimeMs = tMs;
        nextFrameUs = 0;
        stopAllMotors();
        continue;
      }

      if (type == PKT_FRAME) {
        if (i + FRAME_SIZE > len) break;
        enqueueFrame(data + i);
        i += FRAME_SIZE;
        continue;
      }

      // Unknown packet type, stop parsing
      break;
    }
  }
};

static RxCallbacks rxCallbacks;
class ServerCallbacks : public NimBLEServerCallbacks {
  void onConnect(NimBLEServer* pServer, NimBLEConnInfo& connInfo) override {
    Serial.println("BLE client connected");
    Serial.print(connInfo.getAddress().toString().c_str());
    Serial.print(" handle=");
    Serial.print(connInfo.getConnHandle());
  }

  void onDisconnect(NimBLEServer* pServer, NimBLEConnInfo& connInfo, int reason) override {
    Serial.print("BLE client disconnected:");
   Serial.print(connInfo.getAddress().toString().c_str());
    Serial.print(" reason=");
    Serial.println(reason);
    NimBLEDevice::getAdvertising()->start();
  }
};

static ServerCallbacks serverCallbacks;

// --- SETUP ---
void setup() {
  Serial.begin(115200);
  Wire.begin();

#if defined(ARDUINO_ARCH_SAMD) || defined(ESP32) || defined(ARDUINO_TEENSY41) || defined(ARDUINO_TEENSY40) || defined(ARDUINO_TEENSY36) || defined(ARDUINO_TEENSY35)
  analogReadResolution(12);
#endif

  Serial.println();
  Serial.println("Initializing DRV2605 modules (BLE binary stream mode)...");

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

  // BLE init
  NimBLEDevice::init(DEVICE_NAME);
  NimBLEServer* server = NimBLEDevice::createServer();
  server->setCallbacks(&serverCallbacks);
  NimBLEService* service = server->createService(SERVICE_UUID);
  NimBLECharacteristic* rxChar = service->createCharacteristic(
    RX_CHAR_UUID,
    NIMBLE_PROPERTY::WRITE | NIMBLE_PROPERTY::WRITE_NR
  );
  rxChar->setCallbacks(&rxCallbacks);
  txChar = service->createCharacteristic(
    TX_CHAR_UUID,
    NIMBLE_PROPERTY::NOTIFY
  );
  service->start();

  NimBLEAdvertising* advertising = NimBLEDevice::getAdvertising();
  NimBLEAdvertisementData advData;
  advData.setName(DEVICE_NAME);
  advData.setCompleteServices(BLEUUID(SERVICE_UUID));
  advertising->setAdvertisementData(advData);
  bool advOk = advertising->start();

  if (advOk) {
    Serial.print("BLE ready. Device name: ");
    Serial.println(DEVICE_NAME);
  } else {
    Serial.println("BLE start failed");
  }
  Serial.print("Binary frame size = ");
  Serial.print(FRAME_SIZE);
  Serial.println(" bytes.");
}

// --- LOOP ---
void loop() {
  int adcValue = 3000;

  if (playing) {
    uint32_t nowMs = millis();
    if (!playStarted) {
      if ((int32_t)(nowMs - startTimeMs) >= 0) {
        playStarted = true;
        nextFrameUs = micros();
      }
    }

    if (playStarted) {
      uint32_t nowUs = micros();
      while ((int32_t)(nowUs - nextFrameUs) >= 0) {
        if (queueCount == 0) break;
        uint8_t frame[FRAME_SIZE];
        if (!dequeueFrame(frame)) break;
        parseFrame(frame);
        nextFrameUs += SAMPLE_PERIOD;
        nowUs = micros();
      }
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

