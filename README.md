# Волшебный мир (Magic World) 🌰

> Шифр проекта: **ACORN**

Детская приключенческая 3D-игра, которую делают папа и двое детей.

Эльф или волшебник исследует волшебный лес, убирает мусор, собирает магические кристаллы и превращается в зверят. Боссов побеждают не силой, а **починкой**: отремонтированные предметы дают боссу отпор, а побеждённого босса расколдовывают.

## Как запустить

1. Установите **Unity 6000.3.28f1** через Unity Hub с модулями **Linux Build Support** и **Windows Build Support**. На всех компьютерах должна быть одна и та же версия (записана в `ProjectSettings/ProjectVersion.txt`).
2. Установите [Git LFS](https://git-lfs.com) и выполните `git lfs install`.
3. Склонируйте репозиторий:
   ```bash
   git clone git@github.com:vidiaievd/acorn.git
   ```
4. В Unity Hub: **Add** → выберите папку репозитория → откройте проект.
5. Нажмите ▶ **Play** в редакторе.

### Настройка слияния сцен (один раз на компьютер)

Сцены и префабы — это текстовые YAML-файлы, их умеет сливать UnityYAMLMerge. Команда (путь к Unity поправьте под свой компьютер):

```bash
git config merge.unityyamlmerge.name "Unity YAML Merge"
git config merge.unityyamlmerge.driver '"/путь/к/Unity/Editor/Data/Tools/UnityYAMLMerge" merge -p %O %A %B %A'
```

Пример пути: `Unity/Hub/Editor/6000.3.28f1/Editor/Data/Tools/UnityYAMLMerge`.

## Документы

- [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md): идея игры и принятые решения
- [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md): план по этапам и шагам

## Структура

| Папка | Что внутри |
|---|---|
| `Assets/_Project/` | Наш код и ассеты: `Art`, `Audio`, `Data`, `Prefabs`, `Scenes`, `Scripts`, `Shaders`, `VFX`, `UI` |
| `Assets/ThirdParty/` | Скачанные паки (их не правим, копируем в `_Project`) |
| `Packages/` | Список пакетов Unity (`manifest.json`) |
| `ProjectSettings/` | Настройки проекта (текстовые, видны в Git) |
| `art_source/` | Исходники рисунков Krita и Blender (хранятся в LFS) |
| `docs/` | Документы проекта и референсы концептов |

Папки `Library/`, `Logs/`, `UserSettings/` создаёт Unity сама и в Git они не попадают.

## Коммиты

Начинайте сообщение коммита с ID шага из плана, например: `E3.4: синий кристалл маны`.

Ветки и теги:
- `main` — текущая игра на Unity.
- `godot-prototype` — первый 2.5D-прототип на Godot (только для истории).
- Тег `godot-demo-1` — последняя версия Godot-прототипа.

## Стек

Unity 6000.3.28f1 · C# · Universal Render Pipeline (URP) · Input System · Cinemachine · ProBuilder · Polybrush · Terrain Tools · AI Navigation · VFX Graph · Shader Graph · Mixamo · Krita · Audacity · Git LFS
