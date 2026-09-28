# scan-turntable

Поворотный стол для фотограмметрии / 3D-сканирования. Без дисплея: управление по USB-Serial и через Wi‑Fi точку доступа.

## Оборудование

- Мотор: Minebea Mitsumi **PM42L-048-EPAO** ×**2** (маркировка **EM-182 TB9608E**; 48 шагов/об, 7.5°; Ø42×22.2 мм, вал Ø3 мм; bipolar) — азимут + наклон
- Шкив / шестерня на валу: **spur pinion m=2 z=12** (азимут → венец платформы); опционально GT2 12T для старых сборок
- Платформа сканирования: **Ø200 мм** с **зубчатым венцом** по ободу (печать на Ender 3 плашмя)
- Драйвер: **EasyDriver** (по умолчанию 1/8 шага → 384 микрошага/об; ток ~600 мА)
- Контроллер: **ESP32-C3**

## Прошивка

PlatformIO-проект (`platformio.ini`, `src/main.cpp`). Сборка и прошивка:

```bash
pio run -t upload
```

## Проводка ESP32-C3 → EasyDriver

Общая земля обязательна.

| ESP32-C3 | EasyDriver |
|----------|------------|
| GPIO4    | STEP       |
| GPIO5    | DIR        |
| GPIO6    | ENABLE (HIGH = катушки выкл.) |
| GPIO7    | кнопка на GND (старт съёмки, необязательно) |

MS1/MS2 не подключать — на плате уже 1/8. Питание мотора: 12 В на M+/GND EasyDriver (8–30 В). ESP32 питается от USB. Не подавать 5 V EasyDriver на пины 3.3 V ESP. Ток: потенциометр по TP1 ≈ 3.6 V (~600 мА).

## Управление

**Wi‑Fi AP:** `ScanTable` / `scan1234` → http://192.168.4.1 (веб-интерфейс).

**Serial (115200):**

- `SCAN <кадров> <пауза_мс>` — съёмка по кругу
- `JOG <градусы>` — поворот
- `SPEED <микрошагов/с>` — скорость
- `STOP` — стоп

## CAD

SolidWorks в `cad/` (контур мотора — `file_07_40.gif`; азимут — шестерня × венец, см. `GEAR_rim_pinion_notes.txt`).

### Мотор (user-owned) и привод
- `Motor_PM42L_048_EPAO.SLDPRT` — **перерисован пользователем**; скрипты **не** пересобирают этот файл
- `Pinion_Spur_Z12_M2.SLDPRT` — шестерня m=2, z=12, bore Ø3, L=8; торец вала +2 мм
- `Motor_PM42L_048_EPAO_Pinion.SLDASM` — мотор + шестерня
- `Pulley_GT2_12T.SLDPRT` / `Motor_PM42L_048_EPAO_Pulley.SLDASM` — прежний вариант с ремнём GT2 (архив)

### Двухосевой стол (как Revopoint A230), 2× PM42 — печать под Ender 3
Сборка: `Turntable_Revopoint2Motor.SLDASM`  
Motor1 = азимут (шестерня → зубчатый венец Ø200), Motor2 = наклон U-люльки.  
Корпус закрытый (Base_Housing + крышки). Заметки: `PRINT_Ender3_notes.txt`, `GEAR_rim_pinion_notes.txt`.

| Файл | Габарит | Стол 220×220 |
|------|---------|--------------|
| **`Platform_RimGear_200.SLDPRT`** | **tip Ø200 × 14 мм, z=98, m=2** | OK |
| `Base_Housing.SLDPRT` | Ø200 × 36 | OK |
| `Cover_Base_Top.SLDPRT` | Ø200 × 4 | OK |
| `Yoke_Cradle_Base.SLDPRT` | ~192×55×8 | OK |
| `Yoke_Cradle_Arm_L/R.SLDPRT` | 28×48×12 | OK |
| `Cover_Arm_Motor.SLDPRT` | 55×58×22 | OK |
| `Pinion_Spur_Z12_M2.SLDPRT` | Ø28 × ~12 | OK |
| `Shaft_Tilt.SLDPRT` | Ø8 × 190 | лучше металлический пруток |

FDM: PLA/PETG. Цель по габариту как Revopoint: **Ø200 × ~H82**.
