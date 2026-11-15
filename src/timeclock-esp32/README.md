# timeclock-esp32

Wi-Fi RFID employee time clock: ESP32, PN532 reader, ST7789 TFT, and a buzzer. Punches go to the TimeClock API (`src/TimeClock.Api`) and land in the production `TimePunches` table.

## Setup

1. Open `timeclock-esp32.ino` in Arduino IDE.
2. Set **board** to ESP32 Dev Module and install libraries: Adafruit PN532, DIYables TFT SPI, WiFiManager (tzapu), ArduinoJson 7. The firmware fills about 96% of the default app partition (ESP32 core 3.3). If a later change does not fit, set **Tools → Partition Scheme** to **Huge APP (3MB No OTA/1MB SPIFFS)**. Changing the partition scheme erases the clock's stored files, so let queued punches upload first (the Clocks page shows "Nothing" waiting).
3. Copy `secrets.example.h` to `secrets.h` and set `ADMIN_API_KEY` to the same value as on the Render web service. Optionally set `DEVICE_LOCATION_ID` to the location where the clock is mounted.
4. Upload to the ESP32. On first boot, join the `TimeClock-Setup` Wi-Fi network (password `12345678`) to pick the store Wi-Fi.
5. In the admin app, open **Clocks**. The clock appears there after its first check-in.

## Assigning cards

The clock only knows employees that have a card in the admin app.

1. Scan the new card at the clock. It shows **UNKNOWN CARD** and the card ID, and reports the scan to the admin app.
2. In the admin app, open **Clock Issues** and use the card button on that row to assign it to an employee. You can also enter the ID on the employee's page under **NFC Card**.
3. The admin app sends the change to every clock and says which clocks confirmed it, usually within 15 to 30 seconds.

## Keeping the employee list current

The API cannot open a connection to the clock (it sits behind the store's router), so the clock checks in instead:

- Every 15 seconds the clock posts to `POST /api/devices/check-in` with its device ID, the version of the employee list it has, how many punches and issues are waiting to upload, firmware version, uptime, and Wi-Fi signal.
- The reply is the current list version, a fingerprint of the employee data the clock caches. Assigning, changing, or clearing a card changes it automatically. **Push employee list** in the admin app (Clocks page or Employees page) forces a new one.
- When the versions differ, the clock downloads `GET /api/sync/employees` and checks in again right away. That second check-in is what the admin app waits for before saying the clock is updated.
- A clock that has not checked in for a minute is shown as offline, and a push reports it as not reached. It updates by itself when it reconnects.
- As a fallback, the clock also downloads the full list every 15 minutes.

All network work runs in a background task on the ESP32's second core, so a slow or unreachable API never delays a card scan.

## How a scan becomes a punch

Each scan flips the employee between In and Out. There are no buttons; a break is simply an Out followed by an In.

| Employee's state today | Scan shows    | Punch sent to the API |
|------------------------|---------------|-----------------------|
| Not clocked in today   | CLOCKED IN    | `In`                  |
| Clocked in             | CLOCKED OUT   | `Out`                 |

- State resets at midnight. An employee who forgot to clock out starts the next day with an In, and the admin app shows the earlier day as **Missing punch** (an odd number of punches) for the admin to fix.
- After a punch, the same card is ignored for **1 minute**. Scanning it again shows **ALREADY SCANNED** with the seconds left, and nothing is recorded. This stops a double tap from clocking someone straight back out. Other employees can scan normally meanwhile.
- A card resting on the reader counts as one tap.
- The employee list and today's In/Out state are kept in memory, and beeps play in the background, so the reader is ready for the next card right away.

Punch times are local wall-clock time (Central), sent without an offset, which is how the API stores them. Each punch also carries `source = "ESP32"` and `deviceId = esp32-<MAC>`.

## Beeps

| Sound | Meaning |
|-------|---------|
| One short beep | Punch recorded (CLOCKED IN or CLOCKED OUT), and once at startup to confirm the buzzer works |
| Three quick beeps | Card not registered, scanned again within a minute, clock time not set, or punch could not be saved |

## Monitoring

The check-in every 15 seconds is the clock's heartbeat. It also reports uptime and Wi-Fi signal strength. In the admin app, **Clocks** shows for each clock:

- Online or offline, time since the last heartbeat, and Wi-Fi signal.
- Downtime in the last 24 hours and 7 days. Any gap of more than a minute between heartbeats, or a restart, is recorded as an outage; the history button lists them.
- Whether its employee list is current, and punches waiting to upload.

The standby screen shows **ONLINE** or **OFFLINE** depending on whether the last check-in reached the API.

The beep reflects what happened on the clock. Uploading happens later, so a punch the API rejects afterwards (for example, for a deleted employee) shows up under **Clock Issues** instead.

## Offline storage and upload

- A scan is written to `/punches.jsonl` on LittleFS right away, so clocking in does not wait for the network.
- The ESP32 posts queued punches to `POST /api/timepunches/sync` in batches of 20, right after a punch and every 30 seconds.
- A punch is removed from the file only after the API accepts it. Wi-Fi loss, API errors, and Render cold starts leave it queued for the next try.
- Each punch has its own ID, so a retry after a lost response does not create a duplicate.
- If the API rejects a punch as invalid (for example, the employee was deleted), the API records it under **Clock Issues** in the admin app and the clock drops it so it does not block the rest.
- Unknown-card scans are queued in `/failures.jsonl` and uploaded to `POST /api/clock-failures/sync`, which also shows them under **Clock Issues**.
- The employee list is cached in `/employees.json` with its version, so cards still work while offline.

The clock needs the time from NTP before it can record a punch. If it boots without Wi-Fi, it shows **CLOCK NOT SET** until Wi-Fi returns.

## Wiring (summary)

- TFT SPI: CS 5, DC 27, RST 33
- PN532 I2C: SDA 21, SCL 22, RST 25
- Buzzer (active): GPIO 13
- The Break and Day buttons (GPIO 26 and 32) are no longer used and can be removed.
