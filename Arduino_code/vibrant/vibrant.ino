#include <Wire.h>
#include "Adafruit_DRV2605.h"


// --- CONFIG ---
const uint8_t NUM_CHANNELS = 4;               // change to your number (you used 4 earlier)
const uint8_t TCA_ADDR = 0x70;                // multiplexer address
const unsigned long SAMPLE_PERIOD = 42000UL;  // 42 ms window in microseconds
const unsigned long MIN_PULSE_US = 2000UL;    // minimum on/off time (2 ms) to avoid clicks
const int adcPin = 1;


// --- DATA ---
Adafruit_DRV2605 drv[NUM_CHANNELS];
uint8_t amplitudes[NUM_CHANNELS];       // 0..127
uint8_t pulsesPerSample[NUM_CHANNELS];  // 0 = off, otherwise 1..10 pulses in SAMPLE_PERIOD
bool vibrating[NUM_CHANNELS];
unsigned long lastToggleTime[NUM_CHANNELS];
unsigned long onTime[NUM_CHANNELS];
unsigned long offTime[NUM_CHANNELS];


int ratedVoltage = 141;
int overdriveClamp = 169;


// for efficient muxing
int8_t lastTCASelected = -1;


// serial buffer (non-blocking)
String serialLine = "";


// --- TCA SELECT ---
void tcaSelect(uint8_t channel) {
 if (channel > 7) return;
 if (lastTCASelected == (int8_t)channel) return;  // already selected
 Wire.beginTransmission(TCA_ADDR);
 Wire.write(1 << channel);
 Wire.endTransmission();
 lastTCASelected = channel;
}


// --- Setup ---
void setup() {
 Serial.begin(115200);
 Wire.begin();
 analogReadResolution(12);
 Serial.println();
 Serial.println("Initializing DRV2605 moddrules (stable pulsing mode)...");


 for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
   lastTCASelected = -1;  // ensure tcaSelect will actually select
   tcaSelect(i);

   
   drv[i].begin();


   // Use LRA mode for channel 0 (if applicable), keep others default ERM
   drv[i].useLRA();

   
   //drv[i].selectLibrary(1);
   drv[i].setMode(DRV2605_MODE_REALTIME);
   uint8_t c3 = drv[i].readRegister8(0x1D);
   c3 |= 0x01;                       // set bit 0 (LRA_OPEN_LOOP = 1)
   drv[i].writeRegister8(0x1D, c3);

   uint8_t lraPeriod = 0x3C; // LRA period (measured)
   drv[i].writeRegister8(0x20, lraPeriod);  
   // init state
   amplitudes[i] = 0;
   pulsesPerSample[i] = 0;
   vibrating[i] = false;
   lastToggleTime[i] = micros();
   onTime[i] = 0;
   offTime[i] = 0;

   drv[i].setRealtimeValue(0);
 }


 // reset selection cache
 lastTCASelected = -1;


 Serial.println("Ready. Streaming format: a,b;c,d;e,f;g,h  (amp 0-127, freq 0-100)");
}


// --- loop ---
void loop() {
 int adcValue = 1023;
 // Non-blocking serial read into serialLine; handle on newline
 while (Serial.available()) {
   char c = (char)Serial.read();
   if (c == '\r') continue;  // ignore CR
   if (c == '\n') {
     if (serialLine.length() > 0) {
       parseInput(serialLine);
       serialLine = "";
     }
   } else {
     serialLine += c;
     // limit length to avoid runaway
     if (serialLine.length() > 200) serialLine = serialLine.substring(serialLine.length() - 200);
   }
 }


 unsigned long now = micros();


 // For each channel, update pulses if needed
 for (uint8_t i = 0; i < NUM_CHANNELS; i++) {


  //if (i==0){
   // If motor off or zero timing, ensure it's off (write only if state changed)
   if (amplitudes[i] == 0 || pulsesPerSample[i] == 0 || onTime[i] == 0) {
     if (vibrating[i]) {
       // select + write off only when necessary
       tcaSelect(i);
       drv[i].setRealtimeValue(0);
       vibrating[i] = false;
     }
     continue;
   }


   // Check toggle
   if (vibrating[i]) {
     // currently ON, wait for onTime then turn off
     if (now - lastToggleTime[i] >= onTime[i]) {
       tcaSelect(i);
       drv[i].setRealtimeValue(0);
       vibrating[i] = false;
       lastToggleTime[i] = now;
     }
   } else {
     // currently OFF, wait for offTime then turn on
     if (now - lastToggleTime[i] >= offTime[i]) {
       tcaSelect(i);
       // set amplitude (we avoid repeated writes by only writing on transitions)
       drv[i].setRealtimeValue((int)(amplitudes[i] * adcValue / 2728));
       vibrating[i] = true;
       lastToggleTime[i] = now;
     }
   }
//  }
//  else {
//     tcaSelect(i);
//     drv[i].setRealtimeValue(0);
//     vibrating[i] = false;
//   }

  tcaSelect(i);

  uint8_t rtp = drv[i].readRegister8(0x00);
  Serial.print("CH"); Serial.print(i);
  Serial.print(" stat = ");
  Serial.println(rtp);
 }
}


// --- parseInput ---
// Input format: "a,b;c,d;e,f;g,h" (up to NUM_CHANNELS segments)
void parseInput(String input) {
 input.trim();
 if (input.length() == 0) return;


 int start = 0;
 for (uint8_t i = 0; i < NUM_CHANNELS; i++) {
   int semi = input.indexOf(';', start);
   String segment = (semi == -1) ? input.substring(start) : input.substring(start, semi);
   segment.trim();


   if (segment.length() > 0) {
     int comma = segment.indexOf(',');
     if (comma > 0) {
       // amplitude 0..127
       int amp = segment.substring(0, comma).toInt();
       amp = constrain(amp, 0, 127);
       amplitudes[i] = (uint8_t)amp;


       // frequency input 0..100 -> perceptible pulses 0..10 (0 means off)
       float freqInput = segment.substring(comma + 1).toFloat();
       freqInput = constrain(freqInput, 0.0, 100.0);


       if (freqInput <= 0.0 || amplitudes[i] == 0) {
         pulsesPerSample[i] = 0;
         onTime[i] = offTime[i] = 0;
       } else {
         // map 0..100 -> 1..10 pulses (perceptible)
         float targetPulsesF = 1.0 + (freqInput / 100.0) * 9.0;  // 1..10
         uint8_t targetPulses = (uint8_t)round(targetPulsesF);
         if (targetPulses < 1) targetPulses = 1;
         pulsesPerSample[i] = targetPulses;


         // compute pulse time (total period per pulse), then half for on/off
         float pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
         unsigned long half = (unsigned long)(pulseTimeF / 2.0);


         // enforce minimum on/off time to avoid clicks
         if (half < MIN_PULSE_US) half = MIN_PULSE_US;


         // Also ensure we still fit into SAMPLE_PERIOD for the number of pulses:
         // recompute pulsesPerSample if too many for min half.
         unsigned long maxPulsesAllowed = SAMPLE_PERIOD / (2 * half);
         if (maxPulsesAllowed == 0) maxPulsesAllowed = 1;
         if (pulsesPerSample[i] > maxPulsesAllowed) pulsesPerSample[i] = (uint8_t)maxPulsesAllowed;


         // recompute half using final pulsesPerSample
         pulseTimeF = (float)SAMPLE_PERIOD / (float)pulsesPerSample[i];
         half = (unsigned long)(pulseTimeF / 2.0);
         if (half < MIN_PULSE_US) half = MIN_PULSE_US;


         onTime[i] = half;
         offTime[i] = half;


         // Reset toggling so we start fresh on new pattern
         vibrating[i] = false;
         lastToggleTime[i] = micros();
       }
     }
   }


   if (semi == -1) break;
   start = semi + 1;
 }


//  Serial.print("Parsed: ");
//  for (uint8_t j = 0; j < NUM_CHANNELS; j++) {
//    Serial.print("[CH");
//    Serial.print(j);
//    Serial.print(" A=");
//    Serial.print(amplitudes[j]);
//    Serial.print(" P=");
//    Serial.print(pulsesPerSample[j]);
//    Serial.print(" on=");
//    Serial.print(onTime[j]);
//    Serial.print("]");
//  }
//  Serial.println();
}
