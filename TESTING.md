# Тестирование MicLink

## Быстрый старт (если драйвер уже установлен)

1. **PC:** Visual Studio → F5 (`Pc/MicLinkWinUI/MicLinkWinUI.sln`, конфигурация **Debug x64**).
2. **Телефон:** тот же Wi‑Fi, Flutter-приложение `Mobile/miclink`:
   ```powershell
   cd Mobile\miclink
   flutter pub get
   flutter run
   ```
3. Введи **PIN** с экрана PC → статус **«Сопряжено»**.
4. На PC: вкладка **Microphone** — полоска уровня должна двигаться при разговоре в телефон.
5. **Discord** → Настройки → Голос и видео → **Устройство ввода** — выбери то же имя, что в Windows (например **Микрофон (MicLink Virtual Audio)** или **MicLink Microphone**).
6. Говори в телефон — в Discord должна быть активность микрофона.

Опционально: в **Настройках** MicLink включи **«Слышать на колонках ПК»** — проверка потока без Discord.

---

## Одноразовая подготовка драйвера (на машине разработчика)

Нужно один раз. У тебя уже сделано: подпись + `pnputil` + `devgen`, **MicLink Microphone** в Windows.

### 1. Сборка `.sys`

```bat
drivers\scripts\build-miclink-driver.bat
```

Результат: `Pc/MicLinkWinUI/MicLinkWinUI/Assets/Driver/MicLinkVirtualAudio.sys` (+ `.inf`).

Требуется: Visual Studio с **Desktop development with C++**, [WDK](https://learn.microsoft.com/windows-hardware/drivers/download-the-wdk).

### 2. Test signing + перезагрузка

PowerShell **от администратора**:

```bat
bcdedit /set testsigning on
shutdown /r /t 0
```

После перезагрузки — водяной знак **Test Mode** (нормально для dev).

### 3. Подпись пакета драйвера

PowerShell **от администратора**:

```powershell
powershell -ExecutionPolicy Bypass -File drivers\scripts\sign-driver-package.ps1
```

Должны появиться три файла в `Assets/Driver/`:
- `MicLinkVirtualAudio.sys`
- `MicLinkVirtualAudio.inf`
- `MicLinkVirtualAudio.cat`

### 4. Установка в Windows

PowerShell **от администратора**:

```powershell
powershell -ExecutionPolicy Bypass -File drivers\scripts\install-driver-device.ps1
```

Или вручную:

```powershell
& "${env:ProgramFiles(x86)}\Windows Kits\10\Tools\10.0.28000.0\x64\devgen.exe" /add /bus ROOT /hardwareid ROOT\VirtualAudioDriver
pnputil /add-driver "Pc\MicLinkWinUI\MicLinkWinUI\Assets\Driver\MicLinkVirtualAudio.inf" /install
```

Проверка: **Параметры → Звук → Ввод** → **MicLink Microphone**.

> `pnputil` только кладёт драйвер в хранилище. Устройство создаёт **devgen** — MicLink делает это автоматически при «Установить драйвер» (после F5 с актуальным кодом).

---

## Чеклист теста

| # | Шаг | Ожидание |
|---|-----|----------|
| 1 | PC и телефон в одной Wi‑Fi | ✓ |
| 2 | MicLink на PC запущен, PIN на экране | ✓ |
| 3 | Flutter-приложение, ввод PIN | «Сопряжено» |
| 4 | Вкладка Microphone, полоска уровня | двигается |
| 5 | Настройки MicLink → драйвер | «MicLink Microphone — установлен» |
| 6 | Windows → Звук → Ввод | **MicLink Microphone** или **Микрофон (MicLink Virtual Audio)** |
| 7 | Discord → то же имя, что в п.6 | слышен голос с телефона |
| 8 | (опц.) Монитор на колонках | слышен в динамиках ПК |

---

## Альтернатива Discord

**Win + R** → `ms-sound:` или «Диктофон» → запись с **MicLink Microphone**.

---

## MicLink Camera

Не готово. Нужен Windows 11 и media-source (`MFCreateVirtualCamera`). Предупреждение `MicLink.CameraSource не найден` в логе — **можно игнорировать**.

---

## Порты firewall

| Порт | Назначение |
|------|------------|
| TCP **9847** | Сопряжение (PIN, mDNS `_miclink._tcp`) |
| TCP **9848** | PCM-аудио с телефона |

---

## Частые проблемы

### «INF не содержит информации о подписи» (0xE000022F)

Запусти `sign-driver-package.ps1` от администратора. Нужны `.cat` + подписанный `.sys`.

### Драйвер в хранилище, но нет MicLink Microphone

```powershell
powershell -ExecutionPolicy Bypass -File drivers\scripts\install-driver-device.ps1
```

### MicLink Microphone есть, Discord молчит

- Полоска уровня в MicLink двигается? Если нет — проблема Wi‑Fi/сопряжения.
- В Discord выбран именно **MicLink Microphone**, не «Default».
- Перезапусти MicLink после установки драйвера.

### Телефон не подключается

- Один Wi‑Fi, firewall не блокирует 9847/9848.
- Flutter: `flutter doctor`, USB-отладка или эмулятор.

---

## Скрипты (справочник)

| Скрипт | Назначение |
|--------|------------|
| `drivers/scripts/build-miclink-driver.bat` | Сборка `.sys` |
| `drivers/scripts/prepare-driver-package.ps1` | INF из WDK-сборки |
| `drivers/scripts/sign-driver-package.ps1` | Тестовая подпись (Admin) |
| `drivers/scripts/install-driver-device.ps1` | devgen + pnputil (Admin) |
| `drivers/scripts/enable-test-signing.ps1` | `bcdedit testsigning on` (Admin) |
