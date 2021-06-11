// служебные функции

// залить все
void fillAll(CRGB color) 
{
  for (int i = 0; i < NUM_LEDS; i++) 
  {
    leds[i] = color;
  }
}

// функция отрисовки точки по координатам X Y
void drawPixelXY(int8_t x, int8_t y, CRGB color) 
{
  if (x < 0 || x > WIDTH - 1 || y < 0 || y > HEIGHT - 1) return;
  int thisPixel = getPixelNumber(x, y) ;
  leds[thisPixel + 1] = color;
  
}

// функция получения цвета пикселя по его номеру
uint32_t getPixColor(int thisSegm) 
{
  int thisPixel = thisSegm ;
  if (thisPixel < 0 || thisPixel > NUM_LEDS - 1) return 0;
  return (((uint32_t)leds[thisPixel].r << 16) | ((long)leds[thisPixel].g << 8 ) | (long)leds[thisPixel].b);
}

// функция получения цвета пикселя в матрице по его координатам
uint32_t getPixColorXY(int8_t x, int8_t y) {
  return getPixColor(getPixelNumber(x, y));
}

#define _WIDTH WIDTH
#define THIS_X x
#define THIS_Y y

// получить номер пикселя в ленте по координатам
uint16_t getPixelNumber(int8_t x, int8_t y) {
  if ((THIS_Y % 2 == 0)) {               // если чётная строка
    return (THIS_Y * _WIDTH + THIS_X);
  } else {                               // если нечётная строка
    return (THIS_Y * _WIDTH + _WIDTH - THIS_X - 1);
  }
}
