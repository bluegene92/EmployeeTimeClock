
#include <SPI.h>
#include <Wire.h>
#include <WiFi.h>
#include <WiFiManager.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <time.h>
#include <esp_system.h>
#include <Adafruit_PN532.h>
#include <DIYables_TFT_SPI.h>
#include <LittleFS.h>
#include <ArduinoJson.h>

// Copy secrets.example.h to secrets.h and fill it in. secrets.h is git-ignored.
#if __has_include("secrets.h")
#include "secrets.h"
#endif

#ifndef API_BASE_URL
#define API_BASE_URL "https://timeclock-api-yems.onrender.com"
#endif
#ifndef ADMIN_API_KEY
#define ADMIN_API_KEY ""
#endif
// Location ID (GUID from the admin app) where this clock is mounted.
// Leave empty to record each punch at the employee's home location.
#ifndef DEVICE_LOCATION_ID
#define DEVICE_LOCATION_ID ""
#endif

// Display
#define TFT_CS 5
#define TFT_DC 27
#define TFT_RST 33
DIYables_ST7789_SPI display(240, 320, TFT_CS, TFT_DC, TFT_RST);

// RFID
#define PN532_IRQ 19
#define PN532_RESET 25
Adafruit_PN532 nfc(PN532_IRQ, PN532_RESET, &Wire);

// Buttons and buzzer
#define BTN_BREAK 26
#define BTN_DAY 32
#define BUZZER 13

#define BLACK 0x0000
#define WHITE 0xFFFF
#define GREEN 0x07E0
#define RED 0xF800
#define YELLOW 0xFFE0

// Files on LittleFS
const char* EMPLOYEE_FILE = "/employees.json"; // cached copy of GET /api/sync/employees
const char* STATE_FILE = "/state.json";        // last punch per employee, decides In vs. Break/Day
const char* QUEUE_FILE = "/punches.jsonl";     // one punch per line, waiting to be uploaded

const unsigned long EMPLOYEE_SYNC_INTERVAL = 900000; // 15 minutes
const unsigned long UPLOAD_INTERVAL = 30000;         // retry pending punches every 30 seconds
const unsigned long CHOICE_TIMEOUT = 15000;
const int UPLOAD_BATCH = 20;

struct CachedEmployee {
  String id;
  String name;
  String locationId;
};

bool rfidReady = false;
bool waitingForChoice = false;
bool showingMessage = false;

CachedEmployee pendingEmployee;
String previousTime = "";
String deviceId = "";

unsigned long messageUntil = 0;
unsigned long lastClockUpdate = 0;
unsigned long lastScanAt = 0;
unsigned long choiceStartedAt = 0;
unsigned long lastEmployeeSync = 0;
unsigned long nextUploadAt = 0;

bool previousBreak = HIGH;
bool previousDay = HIGH;

void beep(unsigned int duration = 150) {
  digitalWrite(BUZZER, HIGH);
  delay(duration);
  digitalWrite(BUZZER, LOW);
}

void showMessage(String title, String detail, uint16_t color, unsigned long durationMs = 2500) {
  display.fillScreen(BLACK);
  display.setTextSize(2);
  display.setTextColor(color);
  display.setCursor(12, 80);
  display.println(title);
  display.setTextColor(WHITE);
  display.setCursor(12, 135);
  display.println(detail);

  showingMessage = true;
  messageUntil = millis() + durationMs;
}

void showStandby() {
  display.fillScreen(BLACK);
  display.setTextSize(2);
  display.setTextColor(WHITE);
  display.setCursor(20, 25);
  display.print("--:--:-- --");
  display.setCursor(20, 100);
  display.println("RFID TIME CLOCK");
  display.setCursor(25, 180);
  display.println("Scan Your Card");
  previousTime = "";
  showingMessage = false;
}

void showChoice() {
  display.fillScreen(BLACK);
  display.setTextSize(2);
  display.setTextColor(WHITE);
  display.setCursor(10, 55);
  display.println(pendingEmployee.name);
  display.setCursor(10, 115);
  display.println("Choose Clock Out:");
  display.setTextColor(YELLOW);
  display.setCursor(10, 170);
  display.println("BREAK: Button 1");
  display.setTextColor(GREEN);
  display.setCursor(10, 210);
  display.println("DAY: Button 2");
  showingMessage = false;
}

void updateClock() {
  if (showingMessage || waitingForChoice) return;

  struct tm timeinfo;
  if (!getLocalTime(&timeinfo, 50)) return;

  char buffer[20];
  strftime(buffer, sizeof(buffer), "%I:%M:%S %p", &timeinfo);

  if (previousTime != String(buffer)) {
    display.setTextSize(2);
    display.setTextColor(WHITE, BLACK);
    display.setCursor(20, 25);
    display.print(buffer);
    previousTime = String(buffer);
  }
}

void connectWiFi() {
  showMessage("Wi-Fi Setup", "Please wait...", YELLOW);
  WiFi.mode(WIFI_STA);
  WiFi.setAutoReconnect(true);

  WiFiManager wm;
  wm.setConfigPortalTimeout(180);
  wm.setConnectTimeout(15);

  bool connected = wm.autoConnect(
    "TimeClock-Setup", "12345678"
  );

  if (connected) {
    Serial.println("Wi-Fi connected");
    Serial.println(WiFi.localIP());
  } else {
    Serial.println("Wi-Fi setup timed out");
  }
}

bool apiReady() {
  if (WiFi.status() != WL_CONNECTED) return false;
  if (strlen(ADMIN_API_KEY) == 0) {
    Serial.println("ADMIN_API_KEY is empty. Set it in secrets.h.");
    return false;
  }
  return true;
}

// ---------------------------------------------------------------------------
// Local wall-clock time. The API stores punch times as local time with no
// offset, for example 2026-11-13T08:30:00.
// ---------------------------------------------------------------------------

bool localNow(String& isoTimestamp, String& day) {
  struct tm t;
  if (!getLocalTime(&t, 100)) return false;
  if (t.tm_year + 1900 < 2024) return false; // NTP has not set the clock yet

  char iso[20];
  char date[11];
  strftime(iso, sizeof(iso), "%Y-%m-%dT%H:%M:%S", &t);
  strftime(date, sizeof(date), "%Y-%m-%d", &t);
  isoTimestamp = iso;
  day = date;
  return true;
}

// Random v4 GUID. The API skips a punch whose ID it already has, so an
// upload can be retried after a lost response without creating a duplicate.
String newPunchId() {
  uint8_t b[16];
  for (int i = 0; i < 16; i += 4) {
    uint32_t r = esp_random();
    memcpy(&b[i], &r, 4);
  }
  b[6] = (b[6] & 0x0F) | 0x40;
  b[8] = (b[8] & 0x3F) | 0x80;

  char s[37];
  snprintf(s, sizeof(s),
    "%02x%02x%02x%02x-%02x%02x-%02x%02x-%02x%02x-%02x%02x%02x%02x%02x%02x",
    b[0], b[1], b[2], b[3], b[4], b[5], b[6], b[7],
    b[8], b[9], b[10], b[11], b[12], b[13], b[14], b[15]);
  return String(s);
}

// ---------------------------------------------------------------------------
// Employee cache
// ---------------------------------------------------------------------------

bool downloadEmployeeList() {
  if (!apiReady()) return false;

  WiFiClientSecure client;
  client.setInsecure(); // No certificate pinning; the admin key still protects the API.

  HTTPClient http;
  http.useHTTP10(true);
  http.setTimeout(15000);

  if (!http.begin(client, String(API_BASE_URL) + "/api/sync/employees")) {
    Serial.println("HTTP connection failed");
    return false;
  }
  http.addHeader("X-Admin-Api-Key", ADMIN_API_KEY);

  int code = http.GET();
  Serial.print("Employee list HTTP code: ");
  Serial.println(code);

  if (code != HTTP_CODE_OK) {
    http.end();
    return false;
  }

  JsonDocument filter;
  filter["employees"][0]["id"] = true;
  filter["employees"][0]["firstName"] = true;
  filter["employees"][0]["lastName"] = true;
  filter["employees"][0]["cardUid"] = true;
  filter["employees"][0]["locationId"] = true;

  JsonDocument response;
  DeserializationError error = deserializeJson(
    response, http.getStream(), DeserializationOption::Filter(filter));
  http.end();

  if (error || !response["employees"].is<JsonArray>()) {
    Serial.print("Invalid employee list: ");
    Serial.println(error.c_str());
    return false;
  }

  JsonDocument cache;
  JsonArray list = cache["employees"].to<JsonArray>();
  int withCard = 0;

  for (JsonObject employee : response["employees"].as<JsonArray>()) {
    String uid = employee["cardUid"] | "";
    if (uid.length() == 0) continue; // the clock can only identify employees with a card

    JsonObject row = list.add<JsonObject>();
    row["id"] = employee["id"].as<String>();
    row["name"] = String(employee["firstName"] | "") + " " + String(employee["lastName"] | "");
    row["uid"] = uid;
    row["locationId"] = String(employee["locationId"] | "");
    withCard++;
  }

  File file = LittleFS.open("/employees.tmp", "w");
  if (!file) {
    Serial.println("Cannot create cache file");
    return false;
  }
  size_t written = serializeJson(cache, file);
  file.close();

  if (written == 0) {
    LittleFS.remove("/employees.tmp");
    return false;
  }

  LittleFS.remove(EMPLOYEE_FILE);
  if (!LittleFS.rename("/employees.tmp", EMPLOYEE_FILE)) {
    Serial.println("Cannot save employee cache");
    return false;
  }

  Serial.printf("Employee list saved: %d with a card\n", withCard);
  return true;
}

bool findLocalEmployee(String uid, CachedEmployee& out) {
  File file = LittleFS.open(EMPLOYEE_FILE, "r");
  if (!file) {
    Serial.println("No local employee list");
    return false;
  }

  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, file);
  file.close();

  if (error) {
    Serial.println("Cannot read employee cache");
    return false;
  }

  uid.toUpperCase();

  for (JsonObject employee : doc["employees"].as<JsonArray>()) {
    if (uid == employee["uid"].as<String>()) {
      out.id = employee["id"].as<String>();
      out.name = employee["name"].as<String>();
      out.locationId = employee["locationId"].as<String>();
      return true;
    }
  }
  return false;
}

// ---------------------------------------------------------------------------
// Punch state: IN, BREAK, or OUT, and the day it was set.
// A shift never crosses midnight, so state from an earlier day is ignored.
// ---------------------------------------------------------------------------

String loadState(const String& employeeId, const String& today) {
  File file = LittleFS.open(STATE_FILE, "r");
  if (!file) return "OUT";

  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, file);
  file.close();
  if (error) return "OUT";

  JsonObject entry = doc[employeeId];
  if (entry.isNull() || entry["d"].as<String>() != today) return "OUT";
  return entry["s"].as<String>();
}

void saveState(const String& employeeId, const String& state, const String& today) {
  JsonDocument previous;
  File in = LittleFS.open(STATE_FILE, "r");
  if (in) {
    deserializeJson(previous, in);
    in.close();
  }

  // Keep only today's entries so the file stays small.
  JsonDocument doc;
  JsonObject root = doc.to<JsonObject>();
  for (JsonPair kv : previous.as<JsonObject>()) {
    if (kv.value()["d"].as<String>() == today) root[kv.key()] = kv.value();
  }

  JsonObject entry = root[employeeId].to<JsonObject>();
  entry["s"] = state;
  entry["d"] = today;

  File out = LittleFS.open(STATE_FILE, "w");
  if (!out) {
    Serial.println("Cannot save punch state");
    return;
  }
  serializeJson(doc, out);
  out.close();
}

// ---------------------------------------------------------------------------
// Punch queue. Each line is a TimePunch body for POST /api/timepunches/sync.
// ---------------------------------------------------------------------------

bool queuePunch(const CachedEmployee& employee, const char* punchType, const String& punchedAt) {
  JsonDocument punch;
  punch["id"] = newPunchId();
  punch["employeeId"] = employee.id;
  punch["punchType"] = punchType; // "In" or "Out"
  punch["punchedAt"] = punchedAt;

  String locationId = strlen(DEVICE_LOCATION_ID) > 0 ? String(DEVICE_LOCATION_ID) : employee.locationId;
  if (locationId.length() > 0) punch["locationId"] = locationId;
  else punch["locationId"] = nullptr;

  punch["source"] = "ESP32";
  punch["deviceId"] = deviceId;

  File file = LittleFS.open(QUEUE_FILE, "a");
  if (!file) {
    Serial.println("Cannot open punch queue");
    return false;
  }
  serializeJson(punch, file);
  file.print('\n');
  file.close();

  Serial.print("Queued punch: ");
  serializeJson(punch, Serial);
  Serial.println();

  nextUploadAt = millis() + 3000;
  return true;
}

int readQueue(String* lines, int maxLines) {
  File file = LittleFS.open(QUEUE_FILE, "r");
  if (!file) return 0;

  int count = 0;
  while (file.available() && count < maxLines) {
    String line = file.readStringUntil('\n');
    line.trim();
    if (line.length() > 0) lines[count++] = line;
  }
  file.close();
  return count;
}

// Removes the first `count` non-empty lines from the queue.
void dropFromQueue(int count) {
  if (count <= 0) return;

  File in = LittleFS.open(QUEUE_FILE, "r");
  if (!in) return;
  File out = LittleFS.open("/punches.tmp", "w");
  if (!out) {
    in.close();
    return;
  }

  int skipped = 0;
  while (in.available()) {
    String line = in.readStringUntil('\n');
    line.trim();
    if (line.length() == 0) continue;
    if (skipped < count) {
      skipped++;
      continue;
    }
    out.print(line);
    out.print('\n');
  }
  in.close();
  out.close();

  LittleFS.remove(QUEUE_FILE);
  LittleFS.rename("/punches.tmp", QUEUE_FILE);
}

int postPunches(const String& body) {
  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.setTimeout(10000);
  if (!http.begin(client, String(API_BASE_URL) + "/api/timepunches/sync")) return -1;

  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Admin-Api-Key", ADMIN_API_KEY);

  int code = http.POST(body);
  Serial.printf("Punch upload HTTP code: %d\n", code);
  if (code > 0) Serial.println(http.getString().substring(0, 200));
  http.end();
  return code;
}

// Sends queued punches. A punch is removed only after the API accepts it,
// or rejects it as invalid (400), for example an employee who was deleted.
void uploadQueue() {
  if (!apiReady()) return;

  String lines[UPLOAD_BATCH];
  int count = readQueue(lines, UPLOAD_BATCH);
  if (count == 0) return;

  String body = "{\"punches\":[";
  for (int i = 0; i < count; i++) {
    if (i > 0) body += ",";
    body += lines[i];
  }
  body += "]}";

  int code = postPunches(body);
  if (code == HTTP_CODE_OK) {
    dropFromQueue(count);
    return;
  }
  if (code != HTTP_CODE_BAD_REQUEST) return; // offline, server error, or bad key: keep and retry

  // One punch in the batch is invalid. Send them one at a time so the good ones still go through.
  int handled = 0;
  for (int i = 0; i < count; i++) {
    int single = postPunches("{\"punches\":[" + lines[i] + "]}");
    if (single == HTTP_CODE_BAD_REQUEST) {
      Serial.println("Dropping rejected punch: " + lines[i]);
    } else if (single != HTTP_CODE_OK) {
      break;
    }
    handled++;
  }
  dropFromQueue(handled);
}

// ---------------------------------------------------------------------------
// Clock-in flow
// ---------------------------------------------------------------------------

void recordPunch(const CachedEmployee& employee, const char* punchType, const char* newState,
                 const String& title, uint16_t color) {
  String punchedAt;
  String today;
  if (!localNow(punchedAt, today)) {
    showMessage("CLOCK NOT SET", "Check Wi-Fi", RED);
    return;
  }

  if (!queuePunch(employee, punchType, punchedAt)) {
    showMessage("SAVE FAILED", "Try again", RED);
    return;
  }
  saveState(employee.id, newState, today);
  showMessage(title, employee.name, color);
}

void checkButtons() {
  bool breakNow = digitalRead(BTN_BREAK);
  bool dayNow = digitalRead(BTN_DAY);

  if (previousBreak == HIGH && breakNow == LOW) {
    Serial.println("OUT-BREAK pressed");
    beep();

    if (waitingForChoice) {
      waitingForChoice = false;
      recordPunch(pendingEmployee, "Out", "BREAK", "ON BREAK", YELLOW);
    }
  }

  if (previousDay == HIGH && dayNow == LOW) {
    Serial.println("OUT-DAY pressed");
    beep();

    if (waitingForChoice) {
      waitingForChoice = false;
      recordPunch(pendingEmployee, "Out", "OUT", "CLOCKED OUT", GREEN);
    }
  }

  previousBreak = breakNow;
  previousDay = dayNow;
}

void checkRFID() {
  if (!rfidReady || waitingForChoice || showingMessage) return;

  uint8_t uid[7];
  uint8_t uidLength;

  if (!nfc.readPassiveTargetID(
      PN532_MIFARE_ISO14443A,
      uid, &uidLength, 50)) return;

  if (millis() - lastScanAt < 2500) return;
  lastScanAt = millis();

  String cardUID = "";
  for (uint8_t i = 0; i < uidLength; i++) {
    if (uid[i] < 16) cardUID += "0";
    cardUID += String(uid[i], HEX);
  }
  cardUID.toUpperCase();

  Serial.println("Card UID: " + cardUID);
  beep();

  CachedEmployee employee;
  if (!findLocalEmployee(cardUID, employee)) {
    // Shown long enough to copy into the admin app's NFC Card field.
    showMessage("UNKNOWN CARD", cardUID, RED, 8000);
    return;
  }

  String punchedAt;
  String today;
  if (!localNow(punchedAt, today)) {
    showMessage("CLOCK NOT SET", "Check Wi-Fi", RED);
    return;
  }

  String state = loadState(employee.id, today);
  Serial.println("Employee: " + employee.name + " state: " + state);

  if (state == "IN") {
    pendingEmployee = employee;
    waitingForChoice = true;
    choiceStartedAt = millis();
    showChoice();
    return;
  }

  bool endingBreak = state == "BREAK";
  recordPunch(employee, "In", "IN", endingBreak ? "BREAK ENDED" : "CLOCKED IN", GREEN);
}

void setup() {
  Serial.begin(115200);
  delay(1500);

  if (LittleFS.begin(true)) {
    Serial.println("LittleFS ready!");
  } else {
    Serial.println("LittleFS initialization failed!");
  }

  pinMode(BTN_BREAK, INPUT_PULLUP);
  pinMode(BTN_DAY, INPUT_PULLUP);
  pinMode(BUZZER, OUTPUT);
  digitalWrite(BUZZER, LOW);

  SPI.begin();
  Wire.begin(21, 22);

  display.begin();
  display.setRotation(1);
  display.fillScreen(BLACK);

  showMessage("TIME CLOCK", "Starting...", WHITE);
  delay(1000);

  connectWiFi();
  deviceId = "esp32-" + WiFi.macAddress();

  // SNTP keeps retrying in the background, so the clock still sets
  // itself if Wi-Fi comes up later.
  configTzTime(
    "CST6CDT,M3.2.0/2",
    "pool.ntp.org",
    "time.nist.gov"
  );

  if (downloadEmployeeList()) {
    lastEmployeeSync = millis();
  }

  nfc.begin();

  if (nfc.getFirmwareVersion()) {
    nfc.SAMConfig();
    rfidReady = true;
    Serial.println("PN532 detected!");
  } else {
    Serial.println("PN532 NOT detected!");
  }

  nextUploadAt = millis();
  showStandby();
}

void loop() {
  checkButtons();

  if (waitingForChoice && millis() - choiceStartedAt >= CHOICE_TIMEOUT) {
    waitingForChoice = false;
    showMessage("CANCELLED", "Scan again", WHITE);
  }

  if (showingMessage &&
      (long)(millis() - messageUntil) >= 0) {
    showStandby();
  }

  if (millis() - lastClockUpdate >= 1000) {
    lastClockUpdate = millis();
    updateClock();
  }

  checkRFID();

  // Network work only while nobody is at the clock, so scans stay fast.
  bool idle = !waitingForChoice && !showingMessage;

  if (idle && (long)(millis() - nextUploadAt) >= 0) {
    nextUploadAt = millis() + UPLOAD_INTERVAL;
    uploadQueue();
  }

  if (idle && millis() - lastEmployeeSync >= EMPLOYEE_SYNC_INTERVAL) {
    lastEmployeeSync = millis();
    Serial.println("Refreshing employee list...");
    downloadEmployeeList();
  }

  delay(20);
}
