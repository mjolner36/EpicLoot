# EpicLoot — кор-прототип ARPG-роглайта

Изометрический ARPG-роглайт: дерево пассивок = карта мира. Узел атласа — локация, зачистка открывает пассивку.
ТЗ: «ТЗ кор-прототип ARPG-роглайта (Godot 4, C#)».

## Версии

- **Godot 4.7.2 .NET** (`Godot_v4.7.2-stable_mono_win64`), SDK `Godot.NET.Sdk/4.7.2`
- **C#, .NET 8** (`net8.0`), физика Jolt, рендер Forward+
- `nuget.config` в корне указывает на локальные nupkgs из папки Godot

## Правила для ИИ

- Только API Godot 4.x. Никаких `yield`, `export var`, `KinematicBody`, `Spatial` из Godot 3.
- Только C#. Сигналы через `[Signal] public delegate void ...EventHandler(...)`. Аргументы сигналов — только Variant-совместимые типы.
- Все классы-наследники GodotObject — `partial`, имя класса = имя файла.
- Композиция: враг и игрок собираются из нод-компонентов (StatBlock, StatusController, Hurtbox, SkillCaster, Dash, CharacterVisual), без глубокого наследования.
- Данные в Resource (.tres), логика в коде. Числа баланса не хардкодить в скриптах — они в `Data/*.tres` (глобальные — в `Data/game_config.tres`).
- Autoload только три: EventBus, SaveSystem, GameState (в этом порядке).
- Одна задача за раз, после каждой — `dotnet build` без ошибок и самопроверка (см. ниже).
- Сцены держать простыми, ноды создавать из кода там, где это проще, чем редактировать .tscn.
- Ввод регистрируется из кода (`Scripts/Core/InputSetup.cs`), секцию `[input]` в project.godot не править.
- Enum-ы в .tres хранятся числами: новые значения `StatType` добавлять только в конец.

## Проверка

```bash
dotnet build
Godot_v4.7.2-stable_mono_win64_console.exe --headless --path . -- --selftest   # код выхода 0 = PASS
Godot_v4.7.2-stable_mono_win64_console.exe --path . -- --shots                 # скриншоты + замер FPS (+60 врагов) в user://
```

Вывод самопроверки считается проваленным и при exit 0, если в логе есть `ERROR` / `SCRIPT ERROR`.

## Архитектура

| Папка | Что там |
| --- | --- |
| `Scripts/Core` | EventBus, GameState, SaveSystem (autoload), GameConfig, PlayerData, InputSetup, SelfTest |
| `Scripts/Stats` | StatType, Modifier, StatBlock (статы + теги + жизнь), StatusData, StatusEffect, StatusController |
| `Scripts/Combat` | DamageInfo, DamageSystem (конвейер урона), Hitbox (запросы формы), Hurtbox, Projectile, Telegraph, Fx, CharacterVisual |
| `Scripts/Player` | PlayerController, SkillCaster, Dash, IsoCamera |
| `Scripts/Enemies` | EnemyData, EnemyBase, EnemyBrain (FSM), WaveSpawner |
| `Scripts/Skills` | SkillData, SkillExecutor (реализации умений), PassiveProcs (особые эффекты пассивок) |
| `Scripts/Atlas` | AtlasNodeData, LocationModifierData, AtlasGraph, AtlasScreen |
| `Scripts/Arena` | ArenaScene (корень боя), ArenaBuilder (арена + навмеш в рантайме) |
| `Scripts/Items` | ItemBaseData, AffixData, OrbData, LootConfig, ItemInstance, ItemDatabase, ItemGenerator, Crafting, Inventory + RunBag, LootSystem, LootDrop |
| `Scripts/UI` | HUD, EnemyWidgetLayer/EnemyWidget (полосы и статусы над врагами), AtlasView, NodePanel, InventoryScreen, ItemCell, ItemText, ResultScreen, FloatingText, SkillSlotView |

Сцены: `Atlas.tscn` (главная), `Arena.tscn`, `Sandbox.tscn` (полигон с манекенами, F6), `Player.tscn`, `Enemies/{Soldier,Archer,Knight,Dummy}.tscn`.

Данные лута: `Data/loot_config.tres`, `Data/Items/{Bases,Affixes,Orbs}/*.tres`. Иконки статусов и сфер — общий атлас `Assets/UI/icons.png` (ячейки 40×40, в UI рисуются 20×20). Оверлей статусов — `Assets/Materials/status_overlay.gdshader`.

Слои коллизий: 1 World, 2 PlayerBody, 3 EnemyBody, 4 PlayerHurtbox, 5 EnemyHurtbox.
Попадания ищутся запросами формы к Hurtbox (`Hitbox.*`), а не сигналами Area3D: в Jolt зоны по умолчанию не видят статику.

## Принятые решения по неоднозначностям ТЗ

- **Холод**: −30% скорости движения и атаки (фиксированно, не за стак). Стаки (до 5) только копят Заморозку; на пороге Холод снимается и накладывается Заморозка.
- **Пассивки** реализованы как обычные Modifier на специальные статы (`FireBlastCooldown`, `ShardPierce`, `StormEveryN`, `FrozenHitMultiplier`…) + теги `NoChill`/`NoIgnite`. Поведение читает статы, числа — в .tres узлов.
- «Добавляет X% урона как …» считается от базового урона умения до множителей.
- Поджог: 50% итогового урона удара в секунду, тик 0.5 с, тики не критуют и игнорируют сопротивления (только DamageTaken цели).
- Гроза: «каждый 4-й удар» = каждое 4-е применение умения; молния 20 урона (стат StormDamage) по 3 ближайшим в 8 м.
- Разряд: перескок — мгновенные удары по ближайшим целям в 8 м (SkillData.ChainRange).
- Раскол (Абсолютный ноль): ×2 по замороженной цели, 50% нанесённого урона холодом соседям в 2 м, заморозка снимается.
- Элита появляется, когда после 3-й волны живых меньше `NextWaveThreshold`. Победа — смерть элиты (оставшиеся враги не важны).
- Сложность: +15% Increased к MaxLife и Damage врагов за шаг (складывается с «Хрупкими костями»).
- Броня: снижение физ. урона = A / (A + 5 × урон).
- Esc в бою — сдаться (как смерть: без награды).

- **Отображение статусов**: полосы и иконки — один экранный слой (`EnemyWidgetLayer`), позиции через `UnprojectPosition`, пул виджетов (берётся на EnemySpawned, возвращается на EnemyDied). Оверлей — один общий ShaderMaterial на статус (мерцание/вспышки в шейдере): per-entity материалы ломали батчинг и роняли FPS до 44.
- Урон тиков Поджога показывается одним числом раз в секунду, тики не дают вспышку попадания.

### Лут и крафт

- **Лут элиты/босса сразу в сумку**: после смерти элиты бой ставится на паузу, поднять с земли его было бы нельзя. Не влезшее в сумку показывается на экране победы как потерянное.
- **Полный тайник** (60 ячеек): не поместившиеся при победе предметы явно перечислены на экране победы как потерянные.
- **Повторное прохождение** узла: пассивки нет, лут падает как обычно.
- **Оружие заменяет** базовый урон Удара мечом (`SkillData.BaseDamageStat` = WeaponDamage). Новый профиль начинается с обычным мечом (`LootConfig.StarterEquipment`).
- **Уровень предмета** = шаг узла, минимум 1. Тир выбирается равновероятно среди открытых уровнем.
- **Порча** повышает тир без учёта уровня; если все аффиксы уже T1 или кандидатов нет — «ничего не изменилось». Осквернённый предмет отклоняет все сферы.
- Сфера тратится только при успешном применении.
- Полигон (Sandbox) — без лута. Esc в бою — сдаться: как смерть, сумка сгорает.
- Сейв версии 2: `completed` + `inventory` (stash по индексам ячеек, equipped по слотам, orbs). Сейв версии 1 читается (профиль создаётся заново). `--selftest` и `--shots` сохраняют сырой сейв и возвращают его в конце.

## Этап 10 (модель и анимации)

Не сделан: нужны внешние ассеты. Части экипировки уже названы как в модели (`SK_Helmet`, `SK_Tunic`, `SK_Belt`, `SK_Boots`, `Sword`, `Bow`) и включаются `CharacterVisual.ShowOnlyParts` по экипировке игрока и `EnemyData.VisibleParts`; заморозка вызывает `SetAnimationPaused`. Точка подмены готова — `VisualScene` в `PlayerData` / `EnemyData`:
если сцена задана, `CharacterVisual` инстанцирует её вместо серых примитивов (оверлеи статусов работают на всех GeometryInstance3D).
Нужно положить `Assets/Models/SkeletonSoldier.glb` и анимации Mixamo (Without Skin) в `Assets/Animations/`, настроить BoneMap (SkeletonProfileHumanoid) и AnimationLibrary, затем добавить AnimationTree в CharacterVisual.
