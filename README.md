# AndroidAppBridge

Windows-приложение для личного резервного копирования и переноса Android-приложений между собственными устройствами через USB / ADB.

Разработчик: **Aleksandr Nikitin aka Niuanli**. Интерфейс: русский и английский. Версия: **0.5.1**.

## Возможности

- Сохранение основного APK и всех установленных split APK с исходными подписями.
- Импорт отдельных APK и OBB с компьютера без подключения исходного устройства.
- Копирование доступных OBB-файлов по выбору пользователя.
- Проверка размеров и SHA-256 перед установкой резервной копии.
- Предварительная проверка минимальной версии Android, архитектуры и свободного места.
- Последовательное подключение устройств: два USB-подключения одновременно не требуются.
- Поиск по имени пакета, журнал операций, отмена и отображение прогресса.
- Переключение RU/EN с сохранением выбора в `language.txt` рядом с программой.

## Требования и запуск

Для готовой сборки нужны Windows и .NET Desktop Runtime 8. Для сборки из исходников нужен .NET SDK 8 или более поздний SDK с поддержкой `net8.0-windows`.

Распакуйте весь архив приложения и откройте `AndroidAppBridge.exe`. Не переносите EXE отдельно от DLL и папки `platform-tools`.

Включите отладку по USB и разрешите доступ компьютеру на устройстве. В настройках для разработчиков включите «Установка через USB» (Install via USB), если такой пункт есть. Держите экран разблокированным и подтвердите запрос установки.

## Перенос приложения

1. Подключите исходное устройство, разблокируйте его и подтвердите доступ компьютеру.
2. Нажмите «Обновить устройства», выберите «Источник» и загрузите список приложений.
3. Найдите приложение по имени пакета, выделите его и нажмите «Сохранить выбранное приложение».
4. Дождитесь завершения. Копия сохраняется в `Backups` рядом с программой.
5. Подключите получателя и выберите его в поле «Получатель». Для ранее сохранённой копии нажмите «Открыть сохранённую копию…» и выберите папку с `backup.json`.
6. Нажмите «Установить на выбранное устройство» и подтвердите установку на его экране.
7. Нажмите «Запустить приложение» и проверьте изображение, звук и управление на устройстве.

Индикатор извлечения показывает процент текущего файла и фактически записанный объём. На этапах проверки и установки используется индикатор ожидания. 100% означает завершённое сохранение или перенос; поиск устройств и загрузка списка возвращают состояние готовности с 0%.

## Импорт APK + OBB с компьютера

1. Нажмите «Импорт APK + OBB…» в блоке установки и выберите основной APK. Если приложение состоит из нескольких APK, выберите основной и все необходимые split APK вместе.
2. Программа определит пакет из AndroidManifest.xml. Подходящие OBB ищутся рядом с APK, в подпапке с именем пакета и в `Android/obb/<package>` под папкой APK.
3. Если OBB не найден, автоматически откроется окно выбора файла. Укажите OBB вручную или отмените выбор, чтобы импортировать только APK. Кнопка «Выбрать OBB…» позволяет изменить выбор; контекстное меню списка позволяет его очистить.
4. Нажмите «Импортировать файлы». Программа скопирует файлы в новую папку `Backups`, вычислит SHA-256 и запишет `backup.json`. Исходные файлы остаются на месте.
5. Выберите получателя и нажмите «Установить на выбранное устройство». Сначала устанавливаются APK, затем OBB копируются в `/sdcard/Android/obb/<package>/` с исходными именами.

Имена OBB должны соответствовать `main.<versionCode>.<package>.obb` или `patch.<versionCode>.<package>.obb`. Версия в имени OBB может отличаться от текущего APK: Android допускает повторное использование файлов расширения после обновления приложения. Смешивание APK разных пакетов или версий и OBB другого пакета отклоняется.

Импорт не адаптирует старые APK к новой версии Android. Ограничения target SDK, подписи, ABI и лицензии по-прежнему проверяются установщиком и самим приложением.

## Ограничения

Закрытые данные, сохранения, учётные записи, ключи, содержимое `Android/data` и дополнительные скачивания не копируются. Недоступные OBB отмечаются в журнале.

Копия может содержать только архитектуру и языковые split-пакеты исходного устройства. Проверка ABI и minSdk не гарантирует работу на другом устройстве, в том числе совместимость нативных библиотек со страницами памяти 16 КБ. Приложения с серверной авторизацией или проверкой лицензии могут потребовать вход или отказаться работать.

Программа не изменяет и не переподписывает APK, не обходит лицензионные проверки или ограничения старого target SDK и не удаляет установленные приложения.

При обновлении используется `adb install-multiple -r`: Android сохраняет данные, если разрешает обновление. OBB-файлы с теми же именами заменяются после подтверждения. Установка APK и передача OBB — отдельные этапы: ошибка или отмена передачи OBB оставляет установленное приложение. Незавершённый экспорт не содержит `backup.json` и не принимается для установки.

Ошибка `INSTALL_FAILED_USER_RESTRICTED: Install canceled by user` означает, что устройство отклонило установку. Проверьте разрешения, разблокируйте экран и подтвердите запрос; повторное извлечение APK не устраняет ограничения устройства.

## Сборка из исходников

Нужен .NET SDK 8 и Windows для WinForms. Из корня репозитория:

```powershell
dotnet restore AndroidAppBridge.sln
dotnet build AndroidAppBridge.sln -c Release --no-restore
dotnet test tests/AndroidAppBridge.Tests/AndroidAppBridge.Tests.csproj -c Release --no-build
```

Для работы с устройствами положите `platform-tools` рядом с EXE в `src/AndroidAppBridge.WinForms/bin/Release/net8.0-windows`. ADB не хранится в Git; официальный источник указан в [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

### Проверки интерфейса

```powershell
$exe = Join-Path $PWD 'src/AndroidAppBridge.WinForms/bin/Release/net8.0-windows/AndroidAppBridge.exe'
Start-Process -FilePath $exe -ArgumentList '--ui-check' -Wait
Get-Content src/AndroidAppBridge.WinForms/bin/Release/net8.0-windows/ui-check-result.txt
dotnet format whitespace AndroidAppBridge.sln --no-restore --verify-no-changes
```

Unit-тесты находятся в отдельном NUnit-проекте и не входят в готовое приложение. Они проверяют доменные правила, манифесты APK, контрольные суммы, пути, отмену, ADB-аргументы и сценарии переноса через подменяемые зависимости.

`--ui-check` проверяет поиск, оба языка, окно «О программе», доступность действий и геометрию элементов. Масштабы 100%, 125% и 150% имитируются через размеры и шрифты; реальная смена DPI монитора проверяется вручную. Команда сохраняет снимки и результат рядом с EXE. `--preview` сохраняет снимок интерфейса и демонстрацию прогресса. Эти команды не подключаются к Android-устройствам.

GitHub Actions выполняет сборку, NUnit-тесты и проверки интерфейса на Windows. Отчёт TRX и снимки сохраняются как артефакты запуска.

### Пакет для распространения

```powershell
./scripts/package.ps1 -PlatformToolsPath 'C:\Android\platform-tools'
```

Версия задаётся в `Directory.Build.props`. Скрипт создаёт ZIP в `artifacts`, включая только файлы приложения, его слои, ADB и оригинальные уведомления. Бэкапы, тестовые сборки, настройки и снимки не включаются.

## Структура проекта

| Папка | Назначение |
| --- | --- |
| `src/AndroidAppBridge.Domain` | Модели и правила пакетов/совместимости |
| `src/AndroidAppBridge.Application` | Сценарии переноса и контракты внешних операций |
| `src/AndroidAppBridge.Infrastructure` | ADB, резервные копии, APK/OBB и настройки |
| `src/AndroidAppBridge.WinForms` | Формы, контролы, локализация и сборка зависимостей |
| `tests/AndroidAppBridge.Tests` | NUnit-тесты и синтетические данные |
| `scripts` | Упаковка приложения |
| `docs` | Архитектура и обоснование паттернов |

Подробности разделения ответственности, GRASP, применённых паттернов и правил переноса — в [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). История версий — в [CHANGELOG.md](CHANGELOG.md).

## Назначение и права

AndroidAppBridge предназначен для личного резервного копирования и переноса приложений, приобретённых или полученных законным путём, между собственными устройствами.

Программа не предназначена для пиратства, обхода лицензий или незаконного распространения приложений. Все права на переносимые приложения и их содержимое принадлежат соответствующим правообладателям.

Автор не поощряет и осуждает незаконное копирование и распространение чужой интеллектуальной собственности. Просим использовать программу исключительно для личных нужд и резервного копирования с соблюдением применимых законов и условий лицензий.

Пользователь самостоятельно несёт ответственность за свои действия. Автор не несёт ответственности за незаконную деятельность, осуществляемую с использованием программы.

© 2026 Aleksandr Nikitin aka Niuanli. Все права защищены. Условия приведены в [LICENSE](LICENSE). Публикация исходников не предоставляет разрешения на их изменение или распространение. Сведения о сторонних компонентах — в [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## English

AndroidAppBridge is a Windows utility for personal backups and transferring lawfully obtained Android apps between your own devices over USB / ADB. It preserves original APK signatures and can copy accessible OBB files. Private app data, saves and accounts are not transferred. Installation and runtime compatibility depend on the target device.

Developer: **Aleksandr Nikitin aka Niuanli**. The interface supports Russian and English. Build with .NET SDK 8 on Windows; compiled releases require .NET Desktop Runtime 8. Run `dotnet test tests/AndroidAppBridge.Tests/AndroidAppBridge.Tests.csproj` for unit tests and `--ui-check` for visual checks. Use `scripts/package.ps1` with a locally downloaded Windows Platform-Tools directory to create a release ZIP.

Use **Import APK + OBB…** to select a local base APK and optional split APKs. Matching OBB files are located next to the APK or in its package subfolder. If none are found, a file picker opens automatically; cancel that picker to import APK only. Import creates a verified backup, which can then be installed using the regular device installation workflow.

All rights reserved. This source publication is not an open-source license. See [LICENSE](LICENSE) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
