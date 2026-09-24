# LovePandas — правила работы с кодом

Таскер для пар в виде 3D-игры. Замысел и механики — `docs/GDD.md`, контент — `docs/CONTENT_MVP.md`.
«Почему так решено» живёт в личной базе знаний: `D:\SecondBrain\wiki\lovepandas.md`.

## Окружение

- Unity **6000.6.3f1** (Unity 6.6), `C:\Program Files\Unity\Hub\Editor\6000.6.3f1`, модули Android (SDK, NDK, JDK).
  Проект создан из шаблона `urp-blank`.
- Телефон для проверки — OnePlus 7 по adb (`...\AndroidPlayer\SDK\platform-tools\adb.exe`).
- Рендер — URP. UI — UI Toolkit, собирается кодом.
- Blender и ComfyUI есть на машине пользователя — для моделей и концептов.

## Модели (`Art/build_panda.py`)

Панды и предметы одежды строятся скриптом Blender, не руками:
```
"C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe" -b --python Art/build_panda.py -- Assets/LovePandas/Resources/Models [папка_превью]
```
- `panda_m.fbx`, `panda_f.fbx` — общий скелет (`BONES`): кости тела, хвост из 4 костей, сокеты `Socket_*` под одежду.
- `Items/<itemId>.fbx` — предмет с origin в точке своего сокета; имя файла = id предмета в каталоге сервера.
- Окрас — цвета вершин: палитра в sRGB, в атрибут пишется linear (`lin()`); альфа < 1 — свечение.
- Импорт: `Editor/ModelImport.cs` (запекание осей, без материалов и анимаций). Материал ставит код — `LovePandas/Toon`.
- В Unity предмет вешается через контейнер (`CharacterView.MakeItem`): внутренний поворот FBX не трогать.
- Новый предмет с моделью: функция `item_*` в скрипте + запись в `server/src/catalog.ts`.

`Art/build_ui.py` — 3D-интерфейс и окружение: доска инвентаря `Models/UI/board.fbx` (пустышки `Slot_0..5`,
`Tab_0..3`, `Close` — точки для вещей, медальонов и крестика), кольцо подсветки `ring.fbx`, платформа `Models/Env/platform.fbx`.

Текстуры из ComfyUI (Flux Krea): фон `Resources/Backgrounds`, плиты платформы `Resources/Textures/stone.png`,
листья `Resources/Foliage` (вырезаны из белого фона заливкой от краёв). Исходники генераций — `docs/concepts/`.

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
- **Ветки:** `dev` — работа, пушить можно всегда; `main` — прод, Railway собирает сервер только из неё.
  **Мерж `dev` → `main` = выкат**, только по команде пользователя.

## Dev-цикл (проверка на телефонах без выката)

1. Сервер на ПК: `cd server && npm start` (база `server/data/`, в git не попадает).
2. `sh tools/dev-phones.sh` — собирает «LovePandas Dev» (`com.redpandaart.lovepandas.dev`, метка DEV,
   сервер из `Resources/server_url_dev.txt` = IP ПК) и ставит на все телефоны по adb. Боевое приложение не трогается.
3. Правка сервера → перезапустить `npm start`; правка клиента → снова `tools/dev-phones.sh`.
4. Всё хорошо → мерж `dev` в `main` по команде пользователя, затем боевой APK (`BuildAndroid`).

Если IP ПК сменился — поправить `server_url_dev.txt`. Dev-сборка меняет PlayerSettings только на время билда.

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
- Адрес сервера — `Resources/server_url.txt` (прод на Railway); в dev-сборке (`LP_DEV`) — `server_url_dev.txt`.
- Не использовать `GameObject.CreatePrimitive`: физика вырезана из сборки, коллайдер не создаётся. Есть `View/Shapes`.
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
