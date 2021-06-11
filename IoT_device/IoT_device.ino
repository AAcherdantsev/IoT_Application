#define GMT 7        // смещение (Барнаул +7)
#define NTP_ADDRESS  "europe.pool.ntp.org" // сервер времени

#define DAWN_BRIGHT 255  // макс. яркость рассвета
#define DAWN_TIMEOUT 1   // сколько рассвет светит после времени будильника, минут

#define BRIGHTNESS 150      // стандартная максимальная яркость
#define CURRENT_LIMIT 3000  

#define WIDTH 16            // ширина матрицы
#define HEIGHT 16           // высота матрицы

#define ESP_MODE 1  // 1 - раздаем вифи 0 - цепляемся к роутеру
byte IP_AP[] = {192, 168, 4, 9};   // статический IP точки доступа

#define AC_SSID "Wi-fi For connect to a router"
#define AC_PASS "12345678"

#define AP_SSID "IoT-Device"
#define AP_PASS "12345678"
#define AP_PORT 8888

#define LED_PIN D4  // цифровой пин для ленты
#define BTN_PIN D2  // цифровой пин для кнопки
#define MODE_AMOUNT 18 

#define NUM_LEDS WIDTH * HEIGHT

#define FASTLED_INTERRUPT_RETRY_COUNT 0
#define FASTLED_ALLOW_INTERRUPTS 0
#define FASTLED_ESP8266_RAW_PIN_ORDER
#define NTP_INTERVAL 60 * 1000    // обновление времени (1 минута)

#include "timer2Minim.h"
#include <FastLED.h>
#include <ESP8266WiFi.h>
#include <DNSServer.h>
#include <ESP8266WebServer.h>
#include <WiFiManager.h>
#include <WiFiUdp.h>
#include <EEPROM.h>
#include <NTPClient.h>
#include <GyverButton.h>
#include "fonts.h"

CRGB leds[NUM_LEDS];
WiFiServer server(80);
WiFiUDP Udp;
WiFiUDP ntpUDP;
NTPClient timeClient(ntpUDP, NTP_ADDRESS, GMT * 3600, NTP_INTERVAL);
timerMinim timeTimer(1000);
timerMinim timeStrTimer(120);
GButton touch(BTN_PIN, LOW_PULL, NORM_OPEN);

const char* autoConnectSSID = AC_SSID;
const char* autoConnectPass = AC_PASS;
const char AP_NameChar[] = AP_SSID;
const char WiFiPassword[] = AP_PASS;
unsigned int localPort = AP_PORT;
char packetBuffer[UDP_TX_PACKET_MAX_SIZE + 1]; // буфер для хранения входящего пакета
String inputBuffer;
static const byte maxDim = max(WIDTH, HEIGHT);

struct // для эффектов
{
  byte brightness = 50;
  byte speed = 30;
  byte scale = 40;
} modes[MODE_AMOUNT];

struct // для будильников
{
  boolean state = false;
  int time = 0;
} alarm[7];

const byte dawnOffsets[] = {5, 10, 15, 20, 25, 30, 40, 50, 60}; // выбор времени до будильника
byte dawnMode;
boolean dawnFlag = false;
float thisTime;
boolean manualOff = false;
boolean sendSettings_flag = false;

int8_t currentMode = 0;
boolean loadingFlag = true;
boolean ONflag = true;
uint32_t eepromTimer;
boolean settChanged = false;

unsigned char matrixValue[8][16];
String lampIP = "";
byte hrs, mins, secs;
byte days;
String timeStr = "00:00";

void setup() {
  ESP.wdtDisable();

  FastLED.addLeds<WS2812B, LED_PIN, GRB>(leds, NUM_LEDS);
  FastLED.setBrightness(BRIGHTNESS);
  FastLED.setMaxPowerInVoltsAndMilliamps(5, CURRENT_LIMIT); // поставить лимит по току
  FastLED.show();

  touch.setStepTimeout(100);
  touch.setClickTimeout(500);

  Serial.begin(115200);
  Serial.println();
  delay(1000);

  // WI-FI
  if (ESP_MODE == 0) 
  {    // режим точки доступа
    WiFi.softAPConfig(IPAddress(IP_AP[0], IP_AP[1], IP_AP[2], IP_AP[3]),
                      IPAddress(192, 168, 4, 1),
                      IPAddress(255, 255, 255, 0));

    WiFi.softAP(AP_NameChar, WiFiPassword);
    IPAddress myIP = WiFi.softAPIP();
    Serial.print("Раздаем Wi-Fi");
    Serial.print("IP : ");
    Serial.println(myIP);
    server.begin();
  } 
  else 
  { // подключаемся к роутеру
    Serial.print("Подключение к WiFi...");
    WiFiManager wifiManager;
    wifiManager.setDebugOutput(false);
    if (digitalRead(BTN_PIN)) wifiManager.resetSettings();
    wifiManager.autoConnect(autoConnectSSID, autoConnectPass);
    Serial.print("Подключено. IP : ");
    Serial.println(WiFi.localIP());
    lampIP = WiFi.localIP().toString();
  }
  Serial.printf("UDP сервер слушает порт № %d\n", localPort);
  Udp.begin(localPort);

  // EEPROM
  EEPROM.begin(202);
  delay(50);
  if (EEPROM.read(198) != 20) 
  { // первый запуск
    EEPROM.write(198, 20);
    EEPROM.commit();
    for (byte i = 0; i < MODE_AMOUNT; i++) 
    {
      EEPROM.put(3 * i + 40, modes[i]);
      EEPROM.commit();
    }
    for (byte i = 0; i < 7; i++) 
    {
      EEPROM.write(5 * i, alarm[i].state); // рассвет
      eeWriteInt(5 * i + 1, alarm[i].time);
      EEPROM.commit();
    }
    EEPROM.write(199, 0);   // рассвет
    EEPROM.write(200, 0);   // режим
    EEPROM.commit();
  }
  for (byte i = 0; i < MODE_AMOUNT; i++) 
  {
    EEPROM.get(3 * i + 40, modes[i]);
  }
  for (byte i = 0; i < 7; i++) 
  {
    alarm[i].state = EEPROM.read(5 * i);
    alarm[i].time = eeGetInt(5 * i + 1);
  }
  dawnMode = EEPROM.read(199);
  currentMode = (int8_t)EEPROM.read(200);

  // отправляем настройки
  sendSettings();
  timeClient.begin();
  memset(matrixValue, 0, sizeof(matrixValue));
  randomSeed(micros());
  
  // получаем время
  byte count = 0;
  while (count < 5) {
    if (timeClient.update()) {
      hrs = timeClient.getHours();
      mins = timeClient.getMinutes();
      secs = timeClient.getSeconds();
      days = timeClient.getDay();
      break;
    }
    count++;
    delay(500);
  }
  updTime();
}

void loop() 
{
  parseUDP();
  effectsTick();
  eepromTick();
  timeTick();
  buttonTick();
  ESP.wdtFeed(); 
  yield();      
}

void eeWriteInt(int pos, int val) 
{
  byte* p = (byte*) &val;
  EEPROM.write(pos, *p);
  EEPROM.write(pos + 1, *(p + 1));
  EEPROM.write(pos + 2, *(p + 2));
  EEPROM.write(pos + 3, *(p + 3));
  EEPROM.commit();
}

int eeGetInt(int pos) 
{
  int val;
  byte* p = (byte*) &val;
  *p        = EEPROM.read(pos);
  *(p + 1)  = EEPROM.read(pos + 1);
  *(p + 2)  = EEPROM.read(pos + 2);
  *(p + 3)  = EEPROM.read(pos + 3);
  return val;
}
