
#include <SPI.h>
#include <Wire.h>
#include <WiFi.h>
#include <WiFiManager.h>
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <time.h>
#include <esp_system.h>
#include <esp_timer.h>
#include <Adafruit_PN532.h>
#include <DIYables_TFT_SPI.h>
#include <LittleFS.h>
#include <ArduinoJson.h>
#include <map>
#include <vector>

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

#define FIRMWARE_VERSION "2026.11.14b"

// Display
#define TFT_CS 5
#define TFT_DC 27
#define TFT_RST 33
DIYables_ST7789_SPI display(240, 320, TFT_CS, TFT_DC, TFT_RST);

// RFID
#define PN532_IRQ 19
#define PN532_RESET 25
Adafruit_PN532 nfc(PN532_IRQ, PN532_RESET, &Wire);

#define BUZZER 13

#define BLACK 0x0000
#define WHITE 0xFFFF
#define GREEN 0x07E0
#define RED 0xF800
#define YELLOW 0xFFE0
#define GRAY 0x8410

// Files on LittleFS
const char* EMPLOYEE_FILE = "/employees.json"; // cached copy of GET /api/sync/employees, with its version
const char* STATE_FILE = "/state.json";        // IN or OUT per employee for today
const char* QUEUE_FILE = "/punches.jsonl";     // one punch per line, waiting to be uploaded
const char* FAILURE_FILE = "/failures.jsonl";  // unknown-card scans, waiting to be uploaded

// The API cannot reach the clock, so the clock checks in. The check-in is the
// heartbeat the admin app monitors, and its reply says which employee list is current.
const unsigned long CHECK_IN_INTERVAL = 15000;
const unsigned long UPLOAD_INTERVAL = 30000;          // retry pending punches every 30 seconds
const unsigned long EMPLOYEE_SYNC_INTERVAL = 900000;  // full refresh even if check-ins fail
const int UPLOAD_BATCH = 20;

// After a punch the same card is ignored for a minute, so a double tap
// does not clock the employee straight back out.
const unsigned long SCAN_COOLDOWN = 60000;
// The reader reports a card resting on it every loop; repeats within this
// window are the same tap, not a new one.
const unsigned long SAME_TAP_WINDOW = 1500;

struct CachedEmployee {
  String id;
  String name;
  String uid;
  String locationId;
};

// Main loop (core 1) owns the screen, reader, and buzzer. The network task
// (core 0) does all HTTP, so a slow or offline API never delays a scan.
// fsLock guards every LittleFS access; cacheLock guards the in-memory employee list.
SemaphoreHandle_t fsLock;
SemaphoreHandle_t cacheLock;
volatile bool uploadRequested = false;
volatile bool checkInRequested = false;
volatile bool serverReachable = false;

std::vector<CachedEmployee> employees; // lookup happens in memory, never from flash during a scan

struct DayState {
  String state; // "IN" or "OUT"
  String day;
};
std::map<String, DayState> punchState; // by employee ID; main loop only

std::map<String, unsigned long> lastPunchAt;     // by card UID; main loop only
std::map<String, unsigned long> lastReportedAt;  // unknown cards, by UID; main loop only
String lastTapUid = "";
unsigned long lastTapAt = 0;

bool rfidReady = false;
bool showingMessage = false;
bool shownReachable = false;

String previousTime = "";
String deviceId = "";

unsigned long messageUntil = 0;
unsigned long lastClockUpdate = 0;

struct LockGuard {
  SemaphoreHandle_t lock;
  explicit LockGuard(SemaphoreHandle_t l) : lock(l) { xSemaphoreTake(lock, portMAX_DELAY); }
  ~LockGuard() { xSemaphoreGive(lock); }
};
#define FS_GUARD LockGuard fsGuard(fsLock)

// ---------------------------------------------------------------------------
// Buzzer: one short beep = punch recorded,
// three quick beeps = card not registered or punch not recorded.
// Played from loop() so a beep never holds up the reader.
// ---------------------------------------------------------------------------

const uint16_t SUCCESS_PATTERN[] = {150};
const uint16_t FAIL_PATTERN[] = {90, 80, 90, 80, 90}; // on, off, on, off, on

const uint16_t* beepPattern = nullptr;
int beepSteps = 0;
int beepStep = 0;
unsigned long beepStepEndsAt = 0;

void startBeep(const uint16_t* pattern, int steps) {
  beepPattern = pattern;
  beepSteps = steps;
  beepStep = 0;
  beepStepEndsAt = millis() + pattern[0];
  digitalWrite(BUZZER, HIGH);
}

void beepSuccess() { startBeep(SUCCESS_PATTERN, 1); }
void beepFail() { startBeep(FAIL_PATTERN, 5); }

void updateBuzzer() {
  if (!beepPattern || (long)(millis() - beepStepEndsAt) < 0) return;
  beepStep++;
  if (beepStep >= beepSteps) {
    digitalWrite(BUZZER, LOW);
    beepPattern = nullptr;
    return;
  }
  digitalWrite(BUZZER, beepStep % 2 == 0 ? HIGH : LOW); // even steps are tones
  beepStepEndsAt = millis() + beepPattern[beepStep];
}

// ---------------------------------------------------------------------------
// Screen
// ---------------------------------------------------------------------------

void showMessage(const String& title, const String& detail, uint16_t color,
                 unsigned long durationMs = 2500, const String& note = "") {
  display.fillScreen(BLACK);
  display.setTextSize(2);
  display.setTextColor(color);
  display.setCursor(12, 80);
  display.println(title);
  display.setTextColor(WHITE);
  display.setCursor(12, 135);
  display.println(detail);
  if (note.length() > 0) {
    display.setTextColor(GRAY);
    display.setCursor(12, 185);
    display.println(note);
  }

  showingMessage = true;
  messageUntil = millis() + durationMs;
}

void drawConnection() {
  shownReachable = serverReachable;
  display.setTextSize(1);
  display.setTextColor(shownReachable ? GREEN : RED, BLACK);
  display.setCursor(20, 225);
  display.print(shownReachable ? "ONLINE " : "OFFLINE");
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
  drawConnection();
  previousTime = "";
  showingMessage = false;
}

void updateClock() {
  if (showingMessage) return;
  if (shownReachable != serverReachable) drawConnection();

  struct tm timeinfo;
  if (!readLocalTime(timeinfo)) return;

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
    static bool warned = false;
    if (!warned) Serial.println("ADMIN_API_KEY is empty. Set it in secrets.h.");
    warned = true;
    return false;
  }
  return true;
}

// ---------------------------------------------------------------------------
// Local wall-clock time. The API stores punch times as local time with no
// offset, for example 2026-11-13T08:30:00.
// ---------------------------------------------------------------------------

// Never waits: getLocalTime() would retry for up to its timeout when unset.
bool readLocalTime(struct tm& t) {
  time_t now = time(nullptr);
  localtime_r(&now, &t);
  return t.tm_year + 1900 >= 2024; // NTP has not set the clock yet
}

bool localNow(String& isoTimestamp, String& day) {
  struct tm t;
  if (!readLocalTime(t)) return false;

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
// HTTP (network task only)
// ---------------------------------------------------------------------------

int postJson(const char* path, const String& body, String* response = nullptr) {
  WiFiClientSecure client;
  client.setInsecure(); // No certificate pinning; the admin key still protects the API.

  HTTPClient http;
  http.setTimeout(10000);
  if (!http.begin(client, String(API_BASE_URL) + path)) return -1;

  http.addHeader("Content-Type", "application/json");
  http.addHeader("X-Admin-Api-Key", ADMIN_API_KEY);

  int code = http.POST(body);
  if (code > 0) {
    String text = http.getString();
    if (response) *response = text;
    if (code != HTTP_CODE_OK) Serial.printf("POST %s HTTP %d: %s\n", path, code, text.substring(0, 200).c_str());
  } else {
    Serial.printf("POST %s failed: %d\n", path, code);
  }
  http.end();
  return code;
}

// ---------------------------------------------------------------------------
// Employee cache: kept in memory for instant lookups, and on flash so the
// clock still knows everyone after a restart without Wi-Fi.
// ---------------------------------------------------------------------------

void replaceEmployees(std::vector<CachedEmployee>& next) {
  LockGuard guard(cacheLock);
  employees.swap(next);
}

// Returns the version stored with the file.
String loadEmployeeFile() {
  JsonDocument doc;
  {
    FS_GUARD;
    File file = LittleFS.open(EMPLOYEE_FILE, "r");
    if (!file) {
      Serial.println("No local employee list");
      return "";
    }
    DeserializationError error = deserializeJson(doc, file);
    file.close();
    if (error) {
      Serial.println("Cannot read employee cache");
      return "";
    }
  }

  std::vector<CachedEmployee> next;
  for (JsonObject employee : doc["employees"].as<JsonArray>()) {
    next.push_back({
      employee["id"].as<String>(),
      employee["name"].as<String>(),
      employee["uid"].as<String>(),
      employee["locationId"].as<String>()
    });
  }
  Serial.printf("Loaded %d employees from flash\n", (int)next.size());
  replaceEmployees(next);
  return doc["version"] | "";
}

// Network task. Downloads GET /api/sync/employees, saves it, and swaps it in.
// Returns the new version, or "" on failure.
String downloadEmployeeList() {
  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.useHTTP10(true);
  http.setTimeout(15000);

  if (!http.begin(client, String(API_BASE_URL) + "/api/sync/employees")) {
    Serial.println("HTTP connection failed");
    return "";
  }
  http.addHeader("X-Admin-Api-Key", ADMIN_API_KEY);

  int code = http.GET();
  Serial.print("Employee list HTTP code: ");
  Serial.println(code);

  if (code != HTTP_CODE_OK) {
    http.end();
    return "";
  }

  JsonDocument filter;
  filter["version"] = true;
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
    return "";
  }

  String version = response["version"] | "";
  JsonDocument cache;
  cache["version"] = version;
  JsonArray list = cache["employees"].to<JsonArray>();
  std::vector<CachedEmployee> next;

  for (JsonObject employee : response["employees"].as<JsonArray>()) {
    String uid = employee["cardUid"] | "";
    if (uid.length() == 0) continue; // the clock can only identify employees with a card
    uid.toUpperCase();

    CachedEmployee e = {
      employee["id"].as<String>(),
      String(employee["firstName"] | "") + " " + String(employee["lastName"] | ""),
      uid,
      String(employee["locationId"] | "")
    };
    JsonObject row = list.add<JsonObject>();
    row["id"] = e.id;
    row["name"] = e.name;
    row["uid"] = e.uid;
    row["locationId"] = e.locationId;
    next.push_back(e);
  }

  size_t count = next.size();
  replaceEmployees(next);

  FS_GUARD;
  File file = LittleFS.open("/employees.tmp", "w");
  if (!file) {
    Serial.println("Cannot create cache file");
    return version; // in memory already; flash copy is refreshed next time
  }
  size_t written = serializeJson(cache, file);
  file.close();

  if (written == 0) {
    LittleFS.remove("/employees.tmp");
  } else {
    LittleFS.remove(EMPLOYEE_FILE);
    if (!LittleFS.rename("/employees.tmp", EMPLOYEE_FILE)) Serial.println("Cannot save employee cache");
  }

  Serial.printf("Employee list saved: %d with a card, version %s\n", (int)count, version.c_str());
  return version;
}

bool findEmployee(const String& uid, CachedEmployee& out) {
  LockGuard guard(cacheLock);
  for (const CachedEmployee& e : employees) {
    if (e.uid == uid) {
      out = e;
      return true;
    }
  }
  return false;
}

// ---------------------------------------------------------------------------
// Punch state: IN or OUT for today. Each scan flips it. A shift never crosses
// midnight, so state from an earlier day counts as OUT. An employee who forgets
// to clock out starts the next day with a scan In, and the admin app flags the
// day with an odd number of punches.
// ---------------------------------------------------------------------------

void loadStateFile() {
  FS_GUARD;
  File file = LittleFS.open(STATE_FILE, "r");
  if (!file) return;

  JsonDocument doc;
  DeserializationError error = deserializeJson(doc, file);
  file.close();
  if (error) return;

  for (JsonPair kv : doc.as<JsonObject>()) {
    punchState[String(kv.key().c_str())] = {kv.value()["s"].as<String>(), kv.value()["d"].as<String>()};
  }
}

bool isClockedIn(const String& employeeId, const String& today) {
  auto it = punchState.find(employeeId);
  return it != punchState.end() && it->second.day == today && it->second.state == "IN";
}

void saveState(const String& employeeId, const String& state, const String& today) {
  punchState[employeeId] = {state, today};

  // Keep only today's entries so the file stays small.
  JsonDocument doc;
  for (auto it = punchState.begin(); it != punchState.end();) {
    if (it->second.day != today) {
      it = punchState.erase(it);
      continue;
    }
    JsonObject entry = doc[it->first].to<JsonObject>();
    entry["s"] = it->second.state;
    entry["d"] = it->second.day;
    ++it;
  }

  FS_GUARD;
  File out = LittleFS.open(STATE_FILE, "w");
  if (!out) {
    Serial.println("Cannot save punch state");
    return;
  }
  serializeJson(doc, out);
  out.close();
}

// ---------------------------------------------------------------------------
// Upload queues. Each line of punches.jsonl is a TimePunch body for
// POST /api/timepunches/sync; each line of failures.jsonl is a clock failure.
// ---------------------------------------------------------------------------

bool appendLine(const char* path, JsonDocument& doc) {
  FS_GUARD;
  File file = LittleFS.open(path, "a");
  if (!file) return false;
  size_t written = serializeJson(doc, file);
  file.print('\n');
  file.close();
  return written > 0;
}

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

  if (!appendLine(QUEUE_FILE, punch)) {
    Serial.println("Cannot write punch queue");
    return false;
  }

  Serial.print("Queued punch: ");
  serializeJson(punch, Serial);
  Serial.println();

  uploadRequested = true;
  return true;
}

// Logged in the admin app under Clock Issues. The scan time is left out
// when the clock has not been set yet.
void queueUnknownCard(const String& cardUid) {
  JsonDocument failure;
  failure["id"] = newPunchId();
  failure["reason"] = "UnknownCard";
  failure["cardUid"] = cardUid;

  String occurredAt;
  String today;
  if (localNow(occurredAt, today)) failure["occurredAt"] = occurredAt;
  else failure["occurredAt"] = nullptr;

  if (strlen(DEVICE_LOCATION_ID) > 0) failure["locationId"] = DEVICE_LOCATION_ID;
  else failure["locationId"] = nullptr;

  failure["source"] = "ESP32";
  failure["deviceId"] = deviceId;

  if (!appendLine(FAILURE_FILE, failure)) Serial.println("Cannot write failure queue");
  uploadRequested = true;
}

int readQueue(const char* path, String* lines, int maxLines) {
  FS_GUARD;
  File file = LittleFS.open(path, "r");
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

int countQueue(const char* path) {
  FS_GUARD;
  File file = LittleFS.open(path, "r");
  if (!file) return 0;

  int count = 0;
  while (file.available()) {
    String line = file.readStringUntil('\n');
    line.trim();
    if (line.length() > 0) count++;
  }
  file.close();
  return count;
}

// Removes the first `count` non-empty lines from a queue file. Holds the lock
// for the whole rewrite so a punch appended meanwhile is not lost.
void dropFromQueue(const char* path, int count) {
  if (count <= 0) return;

  FS_GUARD;
  File in = LittleFS.open(path, "r");
  if (!in) return;
  File out = LittleFS.open("/queue.tmp", "w");
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

  LittleFS.remove(path);
  LittleFS.rename("/queue.tmp", path);
}

String joinLines(const char* key, String* lines, int count) {
  String body = String("{\"") + key + "\":[";
  for (int i = 0; i < count; i++) {
    if (i > 0) body += ",";
    body += lines[i];
  }
  body += "]}";
  return body;
}

// A punch is removed only after the API accepts it, or rejects it as invalid
// (400), for example an employee who was deleted. The API logs each rejected
// punch under Clock Issues in the admin app.
void uploadPunches() {
  String lines[UPLOAD_BATCH];
  int count = readQueue(QUEUE_FILE, lines, UPLOAD_BATCH);
  if (count == 0) return;

  int code = postJson("/api/timepunches/sync", joinLines("punches", lines, count));
  if (code == HTTP_CODE_OK) {
    Serial.printf("Uploaded %d punch(es)\n", count);
    dropFromQueue(QUEUE_FILE, count);
    return;
  }
  if (code != HTTP_CODE_BAD_REQUEST) return; // offline, server error, or bad key: keep and retry

  // One punch in the batch is invalid. Send them one at a time so the good ones still go through.
  int handled = 0;
  for (int i = 0; i < count; i++) {
    int single = postJson("/api/timepunches/sync", joinLines("punches", &lines[i], 1));
    if (single == HTTP_CODE_BAD_REQUEST) {
      Serial.println("Punch rejected by the API: " + lines[i]);
    } else if (single != HTTP_CODE_OK) {
      break;
    }
    handled++;
  }
  dropFromQueue(QUEUE_FILE, handled);
}

void uploadFailures() {
  String lines[UPLOAD_BATCH];
  int count = readQueue(FAILURE_FILE, lines, UPLOAD_BATCH);
  if (count == 0) return;

  int code = postJson("/api/clock-failures/sync", joinLines("failures", lines, count));
  // 400 means the file holds something the API cannot read; drop it rather than retry forever.
  if (code == HTTP_CODE_OK || code == HTTP_CODE_BAD_REQUEST) dropFromQueue(FAILURE_FILE, count);
}

// ---------------------------------------------------------------------------
// Check-in (heartbeat): tells the API this clock is alive and which employee
// list it has. Returns the version the API says is current, or "" if it failed.
// ---------------------------------------------------------------------------

String checkIn(const String& listVersion) {
  JsonDocument body;
  body["deviceId"] = deviceId;
  body["employeeListVersion"] = listVersion;
  body["pendingPunches"] = countQueue(QUEUE_FILE);
  body["pendingFailures"] = countQueue(FAILURE_FILE);
  body["firmwareVersion"] = FIRMWARE_VERSION;
  body["uptimeSeconds"] = (uint32_t)(esp_timer_get_time() / 1000000LL);
  body["wifiRssi"] = WiFi.RSSI();
  if (strlen(DEVICE_LOCATION_ID) > 0) body["locationId"] = DEVICE_LOCATION_ID;
  else body["locationId"] = nullptr;

  String payload;
  serializeJson(body, payload);

  String response;
  if (postJson("/api/devices/check-in", payload, &response) != HTTP_CODE_OK) {
    serverReachable = false;
    return "";
  }
  serverReachable = true;

  JsonDocument reply;
  if (deserializeJson(reply, response)) return "";
  return reply["employeeListVersion"] | "";
}

bool isDue(unsigned long at) {
  return (long)(millis() - at) >= 0;
}

void networkTask(void* param) {
  String listVersion = *(String*)param;
  delete (String*)param;
  unsigned long nextUpload = millis();
  unsigned long nextCheckIn = millis();
  unsigned long nextFullRefresh = millis(); // download once at boot even if check-in fails

  for (;;) {
    if (!apiReady()) {
      serverReachable = false;
    } else {
      if (uploadRequested || isDue(nextUpload)) {
        uploadRequested = false;
        nextUpload = millis() + UPLOAD_INTERVAL;
        uploadPunches();
        uploadFailures();
      }

      bool refreshDue = isDue(nextFullRefresh);
      if (checkInRequested || refreshDue || isDue(nextCheckIn)) {
        checkInRequested = false;
        nextCheckIn = millis() + CHECK_IN_INTERVAL;
        String serverVersion = checkIn(listVersion);

        bool changed = serverVersion.length() > 0 && serverVersion != listVersion;
        if (changed || refreshDue) {
          if (changed) Serial.println("Employee list changed on the server, downloading...");
          String downloaded = downloadEmployeeList();
          if (downloaded.length() > 0) {
            listVersion = downloaded;
            nextFullRefresh = millis() + EMPLOYEE_SYNC_INTERVAL;
            checkIn(listVersion); // confirms the new list to the admin app right away
          } else if (refreshDue) {
            nextFullRefresh = millis() + CHECK_IN_INTERVAL;
          }
        }
      }
    }
    vTaskDelay(pdMS_TO_TICKS(250));
  }
}

// ---------------------------------------------------------------------------
// Scan flow (main loop): each scan flips the employee between In and Out.
// ---------------------------------------------------------------------------

String readCard() {
  uint8_t uid[7];
  uint8_t uidLength;
  if (!nfc.readPassiveTargetID(PN532_MIFARE_ISO14443A, uid, &uidLength, 30)) return "";

  String cardUid = "";
  for (uint8_t i = 0; i < uidLength; i++) {
    if (uid[i] < 16) cardUid += "0";
    cardUid += String(uid[i], HEX);
  }
  cardUid.toUpperCase();
  return cardUid;
}

void forgetOlderThan(std::map<String, unsigned long>& scans, unsigned long age) {
  for (auto it = scans.begin(); it != scans.end();) {
    if (millis() - it->second >= age) it = scans.erase(it);
    else ++it;
  }
}

// "14:05" -> "2:05 PM"
String displayTime(const String& isoTimestamp) {
  int hour = isoTimestamp.substring(11, 13).toInt();
  String minutes = isoTimestamp.substring(14, 16);
  int hour12 = hour % 12 == 0 ? 12 : hour % 12;
  return String(hour12) + ":" + minutes + (hour < 12 ? " AM" : " PM");
}

void checkRFID() {
  if (!rfidReady) return;

  String cardUid = readCard();
  if (cardUid.length() == 0) return;

  bool sameTap = cardUid == lastTapUid && millis() - lastTapAt < SAME_TAP_WINDOW;
  lastTapUid = cardUid;
  lastTapAt = millis();
  if (sameTap) return;

  Serial.println("Card UID: " + cardUid);
  forgetOlderThan(lastPunchAt, SCAN_COOLDOWN);
  forgetOlderThan(lastReportedAt, SCAN_COOLDOWN);

  CachedEmployee employee;
  if (!findEmployee(cardUid, employee)) {
    // Shown long enough to copy into the admin app's NFC Card field.
    showMessage("UNKNOWN CARD", cardUid, RED, 8000);
    beepFail();
    if (lastReportedAt.find(cardUid) == lastReportedAt.end()) {
      lastReportedAt[cardUid] = millis(); // report repeated taps only once a minute
      queueUnknownCard(cardUid);
    }
    checkInRequested = true; // picks up a card the admin just assigned
    return;
  }

  auto recent = lastPunchAt.find(cardUid);
  if (recent != lastPunchAt.end()) {
    unsigned long waitSeconds = (SCAN_COOLDOWN - (millis() - recent->second) + 999) / 1000;
    showMessage("ALREADY SCANNED", employee.name, YELLOW, 2500, "Wait " + String(waitSeconds) + "s");
    beepFail();
    return;
  }

  String punchedAt;
  String today;
  if (!localNow(punchedAt, today)) {
    showMessage("CLOCK NOT SET", "Check Wi-Fi", RED);
    beepFail();
    return;
  }

  bool clockingOut = isClockedIn(employee.id, today);
  if (!queuePunch(employee, clockingOut ? "Out" : "In", punchedAt)) {
    showMessage("SAVE FAILED", "Try again", RED);
    beepFail();
    return;
  }

  lastPunchAt[cardUid] = millis();
  showMessage(clockingOut ? "CLOCKED OUT" : "CLOCKED IN", employee.name, GREEN, 2500, displayTime(punchedAt));
  beepSuccess();
  saveState(employee.id, clockingOut ? "OUT" : "IN", today);
  Serial.println(employee.name + (clockingOut ? " clocked out" : " clocked in"));
}

void setup() {
  Serial.begin(115200);
  delay(1500);

  fsLock = xSemaphoreCreateMutex();
  cacheLock = xSemaphoreCreateMutex();

  if (LittleFS.begin(true)) {
    Serial.println("LittleFS ready!");
  } else {
    Serial.println("LittleFS initialization failed!");
  }

  pinMode(BUZZER, OUTPUT);
  digitalWrite(BUZZER, LOW);

  SPI.begin();
  Wire.begin(21, 22);

  display.begin();
  display.setRotation(1);
  display.fillScreen(BLACK);

  showMessage("TIME CLOCK", "Starting...", WHITE);

  String* listVersion = new String(loadEmployeeFile());
  loadStateFile();

  connectWiFi();
  deviceId = "esp32-" + WiFi.macAddress();
  Serial.println("Device ID: " + deviceId + ", firmware " FIRMWARE_VERSION);

  // SNTP keeps retrying in the background, so the clock still sets
  // itself if Wi-Fi comes up later.
  configTzTime(
    "CST6CDT,M3.2.0/2",
    "pool.ntp.org",
    "time.nist.gov"
  );

  nfc.begin();

  if (nfc.getFirmwareVersion()) {
    nfc.SAMConfig();
    nfc.setPassiveActivationRetries(0x01); // return quickly when no card is present
    rfidReady = true;
    Serial.println("PN532 detected!");
  } else {
    Serial.println("PN532 NOT detected!");
  }

  // TLS needs a large stack.
  xTaskCreatePinnedToCore(networkTask, "network", 16384, listVersion, 1, nullptr, 0);

  showStandby();
}

void loop() {
  updateBuzzer();

  if (showingMessage && (long)(millis() - messageUntil) >= 0) {
    showStandby();
  }

  if (millis() - lastClockUpdate >= 1000) {
    lastClockUpdate = millis();
    updateClock();
  }

  checkRFID();
  updateBuzzer();
  delay(5);
}
