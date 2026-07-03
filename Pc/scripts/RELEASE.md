# Сборка portable MicLink для Windows (x64)

PowerShell из корня репозитория:

```powershell
powershell -ExecutionPolicy Bypass -File Pc\scripts\publish-portable.ps1
```

Результат: `Pc\MicLinkWinUI\MicLinkWinUI\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\`

Скопируйте всю папку `publish` — это portable-сборка (self-contained, не нужен установленный .NET).

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
