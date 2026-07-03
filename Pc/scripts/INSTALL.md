# Установка MicLink

## Установщик (рекомендуется)

```powershell
powershell -ExecutionPolicy Bypass -File Pc\scripts\publish-portable.ps1
# Inno Setup 6:
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" Pc\installer\MicLinkSetup.iss
```

Результат: `Pc\installer\output\MicLinkSetup-0.1.0-x64.exe`

Установщик:
1. Копирует файлы в `Program Files\MicLink`
2. Запускает установку драйвера (UAC один раз)
3. Создаёт ярлык

При первом запуске приложение также предложит установить драйвер.

---

## Драйвер без BIOS и без отключения Secure Boot

**В BIOS ничего менять не нужно.** Secure Boot можно оставить включённым.

### Вариант 1 — Test signing (бесплатно, для beta)

Один раз от администратора:

```powershell
bcdedit /set testsigning on
shutdown /r /t 0
```

После перезагрузки — водяной знак «Test Mode» в углу экрана. Это **не** Secure Boot и **не** BIOS.

Затем установите MicLink — драйвер подписан test-сертификатом (`drivers\scripts\sign-driver-package.ps1` при сборке).

### Вариант 2 — Production (без Test Mode, Secure Boot ON)

Нужна **attestation-подпись Microsoft** (сама подпись бесплатна через [Hardware Dev Center](https://partner.microsoft.com/dashboard/hardware)):

1. Аккаунт Microsoft Partner (бесплатная регистрация)
2. **Code signing certificate** — один из:
   - EV-сертификат (~$200–400/год у DigiCert/Sectigo), или
   - [Azure Trusted Signing](https://azure.microsoft.com/products/trusted-signing) (дешевле, есть программа для open source)
3. Сборка `.sys` + `.inf` + `.cat` → загрузка в Partner Center → attestation signing
4. Подписанный `.cat` кладётся в `Assets/Driver/` — пользователи ставят **без Test Mode**

Полностью бесплатно **без Test Mode** для custom kernel driver в Windows 10/11 **невозможно** — политика Microsoft. Минимум: test signing (beta) или сертификат (production).

### Почему нельзя «просто exe без драйвера»

Виртуальный микрофон на уровне системы = kernel driver. User-mode приложение не может создать устройство ввода в Windows без драйвера.

---

## Portable zip

```powershell
powershell -ExecutionPolicy Bypass -File Pc\scripts\publish-portable.ps1
```

Zip папки `publish\` → GitHub Releases.

---

## Android

```powershell
cd Mobile\miclink
flutter build apk --release
```

APK: `build\app\outputs\flutter-apk\app-release.apk`
