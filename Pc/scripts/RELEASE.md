# Сборка portable MicLink для Windows (x64)

## Полный релиз (рекомендуется)

PowerShell из корня репозитория:

```powershell
powershell -ExecutionPolicy Bypass -File Pc\scripts\build-release.ps1
```

Скрипт последовательно: драйвер → publish PC → APK Android → zip в `release/v0.1.0/`.

Опции:

```powershell
# Только пересобрать PC и zip (драйвер и APK уже есть)
.\Pc\scripts\build-release.ps1 -SkipDriver -SkipAndroid

# С подписью test-сертификата драйвера (PowerShell от администратора)
.\Pc\scripts\build-release.ps1 -SignDriver
```

**Артефакты:**

| Файл | Описание |
|------|----------|
| `release/v0.1.0/MicLink-win-x64-v0.1.0.zip` | Portable Windows x64 (~83 MB) |
| `release/v0.1.0/MicLink-android-v0.1.0.apk` | Android APK (~44 MB) |
| `release/v0.1.0/RELEASE-NOTES.txt` | Краткая инструкция |

## Только PC (publish)

```powershell
powershell -ExecutionPolicy Bypass -File Pc\scripts\publish-portable.ps1
```

Результат: `Pc/MicLinkWinUI/MicLinkWinUI/bin/Release/net8.0-windows10.0.19041.0/win-x64/publish/`

В папке `publish` должны быть `Assets/Driver/` (`.sys`, `.inf`) и `Assets/Scripts/install-driver.ps1`.

## Inno Setup (опционально)

Если установлен [Inno Setup 6](https://jrsoftware.org/isinfo.php):

```powershell
iscc Pc\installer\MicLinkSetup.iss
```

Installer: `Pc/installer/output/MicLinkSetup-0.1.0-x64.exe`

## Требования у пользователя

- Windows 10 1809+ / Windows 11, x64
- **Драйвер микрофона** — один раз: test signing + установка `MicLinkVirtualAudio` (см. `TESTING.md`)
- Разрешение брандмауэра при первом запуске (TCP 9847/9848)

## Один exe-файл

Single-file exe для WinUI 3 часто проблемен (распаковка, MSIX). Рекомендуется **папка publish** как portable zip.

Для GitHub Releases: упакуйте `publish` в `MicLink-win-x64.zip`.

## Flutter (Android)

```powershell
cd Mobile\miclink
flutter build apk --release
```

APK: `Mobile\miclink\build\app\outputs\flutter-apk\app-release.apk`
