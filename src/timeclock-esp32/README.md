# timeclock-esp32

Wi-Fi RFID employee time clock: ESP32, PN532 reader, ST7789 TFT, and break/end-of-day buttons. Punches go to the TimeClock API (`src/TimeClock.Api`) and land in the production `TimePunches` table.

## Setup

1. Open `timeclock-esp32.ino` in Arduino IDE.
2. Set **board** to ESP32 Dev Module and install libraries: Adafruit PN532, DIYables TFT SPI, WiFiManager, ArduinoJson 7.
3. Copy `secrets.example.h` to `secrets.h` and set `ADMIN_API_KEY` to the same value as on the Render web service. Optionally set `DEVICE_LOCATION_ID` to the location where the clock is mounted.
4. Upload to the ESP32. On first boot, join the `TimeClock-Setup` Wi-Fi network (password `12345678`) to pick the store Wi-Fi.

## Assigning cards

The clock only knows employees that have a card in the admin app.

1. Scan the new card at the clock. It shows **UNKNOWN CARD** and the card ID.
2. In the admin app, open the employee and enter that ID under **NFC Card**.
3. The clock picks up the change on its next employee refresh (every 15 minutes, or on restart).

## How a scan becomes a punch

| Employee's state today | Scan / button | Punch sent to the API |
|------------------------|---------------|-----------------------|
| Not clocked in         | Scan          | `In`                  |
| Working                | Scan, then Break | `Out` (state: on break) |
| Working                | Scan, then Day   | `Out` (state: clocked out) |
| On break               | Scan          | `In`                  |

Punch times are local wall-clock time (Central), sent without an offset, which is how the API stores them. Each punch also carries `source = "ESP32"` and `deviceId = esp32-<MAC>`.

## Offline storage and upload

- A scan is written to `/punches.jsonl` on LittleFS right away, so clocking in does not wait for the network.
- While nobody is at the clock, the ESP32 posts queued punches to `POST /api/timepunches/sync` in batches of 20, every 30 seconds.
- A punch is removed from the file only after the API accepts it. Wi-Fi loss, API errors, and Render cold starts leave it queued for the next try.
- Each punch has its own ID, so a retry after a lost response does not create a duplicate.
- If the API rejects a punch as invalid (for example, the employee was deleted), that punch is dropped and logged to Serial so it does not block the rest.
- The employee list (`GET /api/sync/employees`) is cached in `/employees.json`, so cards still work while offline.

The clock needs the time from NTP before it can record a punch. If it boots without Wi-Fi, it shows **CLOCK NOT SET** until Wi-Fi returns.

## Wiring (summary)

- TFT SPI: CS 5, DC 27, RST 33
- PN532 I2C: SDA 21, SCL 22, RST 25
- Break button: GPIO 26 → GND
- End day button: GPIO 32 → GND
- Buzzer: GPIO 13
