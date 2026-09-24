# LovePandas — правила работы с кодом

Таскер для пар в виде 3D-игры. Замысел и механики — `docs/GDD.md`, контент — `docs/CONTENT_MVP.md`.
«Почему так решено» живёт в личной базе знаний: `D:\SecondBrain\wiki\lovepandas.md`.

## Окружение

- Unity **6000.6.3f1** (Unity 6.6), `C:\Program Files\Unity\Hub\Editor\6000.6.3f1`, модули Android (SDK, NDK, JDK).
  Проект создан из шаблона `urp-blank`.
- Телефон для проверки — OnePlus 7 по adb (`...\AndroidPlayer\SDK\platform-tools\adb.exe`).
- Рендер — URP. UI — UI Toolkit, собирается кодом.
- Blender и ComfyUI есть на машине пользователя — для моделей и концептов.

## Сервер (`server/`)

Node 24 + TypeScript (запускается напрямую, без сборки) + встроенный `node:sqlite`. Зависимостей нет.
**Все правила и монеты — только здесь**, клиент лишь вызывает API и показывает `state`.

```
cd server && npm test          # тесты логики (node:test, база в памяти)
cd server && npm start         # :8787, база data/lovepandas.db (DB_PATH, PORT, TZ_OFFSET_MIN)
```

- `src/game.ts` — вся логика: регистрация, пара по коду, эскроу заданий, магазин, бонус, `state()`.
- `src/index.ts` — HTTP-маршруты. Ответ на любое действие — `{ state }` или `{ error }`.
- `GET /state?since=<version>` отдаёт 304, если у пары ничего не менялось — клиент опрашивает раз в 4 с.
- Прод: Railway, проект `lovepandas`, сервис `server`, https://server-production-1b47.up.railway.app.
  Собирается из GitHub (ветка `claude/unity-game-idea-6xp658`, root `/server`), пересборка только на изменения в `server/**`.
  База — volume `/data` (`DB_PATH=/data/lovepandas.db`), `PORT=8787`, память Node ограничена 128 МБ.
- **Пуш с изменениями в `server/` = деплой.** Выкатывать только по команде пользователя.

## Устройство кода

```
Assets/LovePandas/
  Scripts/Core/   — чистая логика без MonoBehaviour: модели, экономика, GameService, хранилище
  Scripts/View/   — 3D: CharacterView (панда-заглушка с сокетами слотов), Materials
  Scripts/UI/     — AppUI: главный экран и шторки Доска / Магазин / Гардероб
  Scripts/App/    — Bootstrap: собирает сцену кодом при старте
  Resources/UI/   — App.uss (стили), Theme.tss (тема по умолчанию)
```

- Сцена собирается в `Bootstrap` через `RuntimeInitializeOnLoadMethod` — сцена в редакторе пустая, в git не конфликтует.
- `Core/ApiClient` — HTTP к серверу, анонимный токен в PlayerPrefs. `Core/GameService` — состояние с сервера и вызовы действий.
- Адрес сервера — `Resources/server_url.txt` (прод на Railway). Для теста телефона с сервером на ПК: там IP ПК
  (`http://192.168.0.20:8787`) и `InsecureHttpOption.AlwaysAllowed` в `BuildTools.Setup` — обратно не коммитить.
- Тестовые ключи ПК-сборки: `-lpServer http://127.0.0.1:8787`, `-lpProfile <имя>` (второй игрок), `-lpToken <токен>`.

## Сборка

```
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -quit -projectPath D:\LovePandas -buildTarget Android -logFile <лог> -executeMethod LovePandas.Editor.BuildTools.BuildAndroid
```

APK кладётся в `Builds/LovePandas.apk` (папка в `.gitignore`).

**Проверка экранов без телефона:** `BuildTools.BuildWindows`, затем
`Builds\Win\LovePandas.exe -screen-fullscreen 0 -screen-width 540 -screen-height 960 -lpShot <папка> -lpScreens home,board,shop,wardrobe`
— снимет скриншоты экранов и закроется (`App/AutoShot.cs`).

Грабли:
- Цвет задаётся через `View.Materials` от `Resources/Materials/Base.mat`. Стандартный материал примитива в сборке пурпурный — шейдер вырезается.
- Эмодзи в шрифте нет — иконки рисуются элементами UI (см. монетку `.coin`).
- Standalone использует уровень качества Mobile: у PC-рендерера из шаблона SSAO падает в сборке и даёт чёрный экран.

## Соглашения

- Код и комментарии — как в существующих файлах: комментарии по-русски, коротко, про «почему».
- Бинарные ассеты — через Git LFS (см. `.gitattributes`).
- Коммиты — короткие, по-английски, как в истории репозитория.
