// Поворотный стол для фотограмметрии.
// Мотор: Minebea Mitsumi PM42L-048-EPAO — 48 полных шагов/об (7.5°).
// Драйвер: EasyDriver (schmalzhaus.com), по умолчанию 1/8 шага → 384 микрошага/об.
// Плата: ESP32-C3, дисплея нет. Управление: USB-Serial и Wi-Fi точка ScanTable.
//
// Провода ESP32-C3 → EasyDriver (общая земля обязательна):
//   GPIO4  STEP
//   GPIO5  DIR
//   GPIO6  ENABLE   (HIGH = катушки выключены)
//   GPIO7  кнопка на GND, старт съёмки (необязательно)
//   MS1 и MS2 не подключать — на плате уже 1/8
// Питание мотора: 12 В на M+ / GND EasyDriver (допустимо 8–30 В).
// ESP32 питается от USB. Не сажать 5V EasyDriver на пины 3.3 В ESP.
// Ток: крутить потенциометр, пока на TP1 не будет около 3.6 В (~600 мА).
// Шелкография MIN/MAX на EasyDriver часто перевёрнута — верить только вольтметру на TP1.

#include <Arduino.h>
#include <WiFi.h>
#include <WebServer.h>

static const int PIN_STEP = 4;
static const int PIN_DIR = 5;
static const int PIN_EN = 6;
static const int PIN_BTN = 7;

static const int FULL_STEPS = 48;
static const int MICRO = 8;
static const int STEPS_PER_REV = FULL_STEPS * MICRO;  // 384

static const char *AP_SSID = "ScanTable";
static const char *AP_PASS = "scan1234";

static WebServer server(80);

static bool dirPositive = true;
static int shots = 48;
static uint32_t dwellMs = 1500;
static uint32_t stepIntervalUs = 4000;  // 250 микрошагов/с

static long stepsLeft = 0;
static uint32_t lastStepUs = 0;
static bool pulseHigh = false;

enum RunMode { MODE_IDLE, MODE_JOG, MODE_DWELL, MODE_MOVE };
static RunMode mode = MODE_IDLE;
static int scanShot = 0;
static uint32_t dwellUntil = 0;

static String serialLine;

static void setEnabled(bool on) {
  digitalWrite(PIN_EN, on ? LOW : HIGH);
}

static void setDir(bool positive) {
  dirPositive = positive;
  digitalWrite(PIN_DIR, positive ? HIGH : LOW);
}

static long stepsForDegrees(float deg) {
  return lroundf(deg * STEPS_PER_REV / 360.0f);
}

static void startSteps(long n) {
  if (n < 0) {
    setDir(false);
    n = -n;
  } else if (n > 0) {
    setDir(true);
  }
  stepsLeft = n;
  if (n > 0) setEnabled(true);
}

static void stopAll() {
  stepsLeft = 0;
  pulseHigh = false;
  digitalWrite(PIN_STEP, LOW);
  mode = MODE_IDLE;
  scanShot = 0;
  setEnabled(false);
}

static void beginScan() {
  if (shots < 1) shots = 1;
  if (shots > STEPS_PER_REV) shots = STEPS_PER_REV;
  scanShot = 0;
  mode = MODE_DWELL;
  dwellUntil = millis() + dwellMs;
  setEnabled(true);
  Serial.printf("scan start shots=%d dwell=%lu ms\n", shots, (unsigned long)dwellMs);
  Serial.println("shot 1");
}

static void beginJogDeg(float deg) {
  mode = MODE_JOG;
  startSteps(stepsForDegrees(deg));
  Serial.printf("jog %.2f deg (%ld steps)\n", deg, stepsForDegrees(deg));
}

static void serviceStepper() {
  if (stepsLeft <= 0) {
    if (pulseHigh) {
      digitalWrite(PIN_STEP, LOW);
      pulseHigh = false;
    }
    return;
  }

  uint32_t now = micros();
  if (pulseHigh) {
    if (now - lastStepUs >= 20) {
      digitalWrite(PIN_STEP, LOW);
      pulseHigh = false;
      lastStepUs = now;
      stepsLeft--;
    }
    return;
  }

  if (now - lastStepUs < stepIntervalUs) return;
  digitalWrite(PIN_STEP, HIGH);
  pulseHigh = true;
  lastStepUs = now;
}

static void serviceMotion() {
  if (mode == MODE_JOG && stepsLeft <= 0 && !pulseHigh) {
    mode = MODE_IDLE;
    setEnabled(false);
    Serial.println("jog done");
    return;
  }

  if (mode == MODE_MOVE && stepsLeft <= 0 && !pulseHigh) {
    mode = MODE_DWELL;
    dwellUntil = millis() + dwellMs;
    Serial.printf("shot %d\n", scanShot + 1);
    return;
  }

  if (mode != MODE_DWELL || millis() < dwellUntil) return;

  scanShot++;
  if (scanShot >= shots) {
    Serial.println("scan done");
    stopAll();
    return;
  }

  long prev = (long)(scanShot - 1) * STEPS_PER_REV / shots;
  long next = (long)scanShot * STEPS_PER_REV / shots;
  mode = MODE_MOVE;
  startSteps(next - prev);
}

static const char PAGE[] PROGMEM = R"HTML(
<!DOCTYPE html>
<html lang="ru">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Стол 3D-скана</title>
<style>
  body{font-family:sans-serif;background:#111;color:#eee;margin:0;padding:16px}
  h1{font-size:1.2rem}
  label{display:block;margin:10px 0 4px;color:#aaa}
  input{width:100%;box-sizing:border-box;padding:10px;font-size:1rem;background:#222;color:#fff;border:1px solid #444}
  button{width:100%;margin-top:10px;padding:12px;font-size:1rem;border:0;border-radius:8px}
  .go{background:#2a7;color:#fff}.stop{background:#a33;color:#fff}.jog{background:#246;color:#fff}
  #st{margin-top:14px;color:#9cf}
</style>
</head>
<body>
<h1>Поворотный стол</h1>
<label>Кадров на оборот</label>
<input id="shots" type="number" min="1" max="384" value="48">
<label>Пауза на кадр, мс</label>
<input id="dwell" type="number" min="100" max="20000" value="1500">
<button class="go" onclick="go()">Старт съёмки</button>
<button class="stop" onclick="cmd('/stop')">Стоп</button>
<button class="jog" onclick="cmd('/jog?deg=15')">+15°</button>
<button class="jog" onclick="cmd('/jog?deg=-15')">−15°</button>
<button class="jog" onclick="cmd('/jog?deg=360')">Полный оборот</button>
<p id="st"></p>
<script>
function cmd(u){fetch(u).then(r=>r.text()).then(t=>{document.getElementById('st').textContent=t})}
function go(){
  const s=document.getElementById('shots').value;
  const d=document.getElementById('dwell').value;
  cmd('/scan?shots='+s+'&dwell='+d);
}
setInterval(()=>fetch('/status').then(r=>r.text()).then(t=>{document.getElementById('st').textContent=t}),1000);
</script>
</body>
</html>
)HTML";

static String statusText() {
  const char *m = "idle";
  if (mode == MODE_DWELL) m = "dwell";
  if (mode == MODE_MOVE) m = "scan";
  if (mode == MODE_JOG) m = "jog";
  int shown = (mode == MODE_IDLE) ? 0 : scanShot + 1;
  char buf[160];
  snprintf(buf, sizeof(buf), "mode=%s shot=%d/%d steps_left=%ld",
           m, shown, shots, stepsLeft);
  return String(buf);
}

static void handleRoot() { server.send_P(200, "text/html", PAGE); }

static void handleStatus() { server.send(200, "text/plain", statusText()); }

static void handleStop() {
  stopAll();
  server.send(200, "text/plain", "stopped");
}

static void handleScan() {
  if (server.hasArg("shots")) shots = server.arg("shots").toInt();
  if (server.hasArg("dwell")) dwellMs = (uint32_t)server.arg("dwell").toInt();
  if (shots < 1) shots = 1;
  if (shots > STEPS_PER_REV) shots = STEPS_PER_REV;
  if (dwellMs < 50) dwellMs = 50;
  stopAll();
  beginScan();
  server.send(200, "text/plain", statusText());
}

static void handleJog() {
  float deg = server.hasArg("deg") ? server.arg("deg").toFloat() : 15.0f;
  stopAll();
  beginJogDeg(deg);
  server.send(200, "text/plain", statusText());
}

static void handleSerial() {
  while (Serial.available()) {
    char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c != '\n') {
      if (serialLine.length() < 80) serialLine += c;
      continue;
    }
    serialLine.trim();
    String u = serialLine;
    u.toUpperCase();
    if (u.startsWith("SCAN")) {
      int s = shots;
      int d = (int)dwellMs;
      sscanf(serialLine.c_str(), "%*s %d %d", &s, &d);
      shots = s;
      dwellMs = (uint32_t)d;
      stopAll();
      beginScan();
    } else if (u.startsWith("JOG")) {
      float deg = 15;
      sscanf(serialLine.c_str(), "%*s %f", &deg);
      stopAll();
      beginJogDeg(deg);
    } else if (u == "STOP") {
      stopAll();
      Serial.println("stopped");
    } else if (u.startsWith("SPEED")) {
      int sps = 250;
      sscanf(serialLine.c_str(), "%*s %d", &sps);
      if (sps < 20) sps = 20;
      if (sps > 2000) sps = 2000;
      stepIntervalUs = 1000000UL / (uint32_t)sps;
      Serial.printf("speed %d ustep/s\n", sps);
    } else {
      Serial.println("SCAN <кадров> <пауза_мс>");
      Serial.println("JOG <градусы>");
      Serial.println("SPEED <микрошагов/с>");
      Serial.println("STOP");
    }
    serialLine = "";
  }
}

static bool btnWasDown = false;
static uint32_t btnLockUntil = 0;

static void serviceButton() {
  if (millis() < btnLockUntil) return;
  bool down = digitalRead(PIN_BTN) == LOW;
  if (down && !btnWasDown) {
    btnLockUntil = millis() + 250;
    if (mode == MODE_IDLE) beginScan();
    else stopAll();
  }
  btnWasDown = down;
}

void setup() {
  pinMode(PIN_STEP, OUTPUT);
  pinMode(PIN_DIR, OUTPUT);
  pinMode(PIN_EN, OUTPUT);
  pinMode(PIN_BTN, INPUT_PULLUP);
  digitalWrite(PIN_STEP, LOW);
  setDir(true);
  setEnabled(false);

  Serial.begin(115200);
  delay(300);
  Serial.println("scan-turntable ESP32-C3");
  Serial.println("PM42L-048-EPAO 48 step, EasyDriver 1/8 -> 384 ustep/rev");
  Serial.println("SCAN 48 1500 | JOG 15 | SPEED 250 | STOP");

  WiFi.mode(WIFI_AP);
  WiFi.softAP(AP_SSID, AP_PASS);
  Serial.print("AP ");
  Serial.print(AP_SSID);
  Serial.print("  http://");
  Serial.println(WiFi.softAPIP());

  server.on("/", handleRoot);
  server.on("/status", handleStatus);
  server.on("/scan", handleScan);
  server.on("/jog", handleJog);
  server.on("/stop", handleStop);
  server.begin();
}

void loop() {
  server.handleClient();
  handleSerial();
  serviceButton();
  serviceStepper();
  serviceMotion();
}
