# LovePandas — правила работы с кодом

Таскер для пар в виде 3D-игры. Замысел и механики — `docs/GDD.md`, контент — `docs/CONTENT_MVP.md`.
«Почему так решено» живёт в личной базе знаний: `D:\SecondBrain\wiki\lovepandas.md`.

## Окружение

- Unity **6000.6.3f1** (Unity 6.6), `C:\Program Files\Unity\Hub\Editor\6000.6.3f1`, модули Android (SDK, NDK, JDK).
  Проект создан из шаблона `urp-blank`.
- Телефон для проверки — OnePlus 7 по adb (`...\AndroidPlayer\SDK\platform-tools\adb.exe`).
- Рендер — URP. UI — UI Toolkit, собирается кодом.
- Blender и ComfyUI есть на машине пользователя — для моделей и концептов.

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
- Все операции с монетами — только в `GameService`. С Firebase они переедут в Cloud Functions, клиент будет лишь вызывать их.
- Хранилище за интерфейсом `IGameStore`. Сейчас `LocalStore` (JSON в `persistentDataPath`) и режим «два игрока на одном телефоне» — кнопка ⇄ вверху.

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

## Соглашения

- Код и комментарии — как в существующих файлах: комментарии по-русски, коротко, про «почему».
- Бинарные ассеты — через Git LFS (см. `.gitattributes`).
- Коммиты — короткие, по-английски, как в истории репозитория.
