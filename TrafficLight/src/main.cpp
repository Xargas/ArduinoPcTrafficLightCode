#include <Arduino.h>

#define LED_ONBOARD GPIO_NUM_8

#define LED_RED    GPIO_NUM_2
#define LED_YELLOW GPIO_NUM_1
#define LED_GREEN  GPIO_NUM_0

#define TIMEOUT_BLINK_MS 500



unsigned long lastPacketTime;

bool blinkTrafficLamp = true;
bool blinkTrafficLampState = false;
unsigned long lastBlinkTime;
bool x;


void setTrafficLight(bool green, bool yellow, bool red)
{
  digitalWrite(LED_GREEN,  green  ? LOW : HIGH);
  digitalWrite(LED_YELLOW, yellow ? LOW : HIGH);
  digitalWrite(LED_RED,    red    ? LOW : HIGH);
}

void setup()
{
  Serial.begin(115200);

  pinMode(LED_GREEN, OUTPUT);
  pinMode(LED_YELLOW, OUTPUT);
  pinMode(LED_RED, OUTPUT);
  pinMode(LED_ONBOARD, OUTPUT);

  setTrafficLight(false, false, true);

  lastPacketTime = millis();
  lastBlinkTime = millis();
}

void loop()
{
  if (Serial.available() > 0)
  {
    String data = Serial.readStringUntil('\n');
    data.trim();

    if (data.startsWith("set")) 
    {
      blinkTrafficLamp = false;
      lastPacketTime = millis(); 
      char color = data.charAt(4);
      
      if (color == 'g')      setTrafficLight(true, false, false);
      else if (color == 'y') setTrafficLight(false, true, false);
      else if (color == 'r') setTrafficLight(false, false, true);
      else                   blinkTrafficLamp = true;

      Serial.write("set done\n");
    }
  }

  if ((blinkTrafficLamp) && (millis() - lastBlinkTime > TIMEOUT_BLINK_MS))
  {
    lastBlinkTime = millis();
    blinkTrafficLampState = !blinkTrafficLampState;
    setTrafficLight(false, blinkTrafficLampState, false);
  }
}
