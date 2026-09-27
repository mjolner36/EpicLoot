using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using EpicLoot.Arena;
using EpicLoot.Atlas;
using EpicLoot.Combat;
using EpicLoot.Enemies;
using EpicLoot.Items;
using EpicLoot.UI;
using EpicLoot.Stats;

namespace EpicLoot.Core;

/// <summary>
/// Автопроверки для headless-запуска: godot --headless --path . -- --selftest
/// Код выхода 0 — всё прошло.
/// </summary>
public static class SelfTest
{
	private static int _failures;
	private static int _checks;

	private static void Check(bool ok, string what)
	{
		_checks++;
		if (ok) GD.Print($"  ok   {what}");
		else
		{
			_failures++;
			GD.PrintErr($"  FAIL {what}");
		}
	}

	private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.001f;

	public static async Task<int> Run(GameState gs)
	{
		GD.Print("=== SELFTEST ===");
		// Сейв игрока (атлас, тайник, экипировка, сферы) сохраняется как есть и возвращается в конце.
		var rawSave = SaveSystem.Instance.ReadRaw();
		SaveSystem.Instance.WriteRaw(null);
		gs.LoadGame();

		TestFormula();
		TestData(gs);
		TestDamageMath(gs);
		TestItems(gs);
		await TestArena(gs);

		SaveSystem.Instance.WriteRaw(rawSave);
		gs.LoadGame();

		GD.Print(_failures == 0 ? $"=== SELFTEST PASS ({_checks} checks) ===" : $"=== SELFTEST FAIL ({_failures}/{_checks}) ===");
		return _failures == 0 ? 0 : 1;
	}

	private static void TestFormula()
	{
		GD.Print("[formula]");
		var s = new StatBlock();
		s.SetBase(StatType.MaxLife, 10);
		s.AddModifier(StatType.MaxLife, ModifierType.Flat, 5, "a");
		s.AddModifier(StatType.MaxLife, ModifierType.Increased, 0.5f, "a");
		s.AddModifier(StatType.MaxLife, ModifierType.Increased, 0.5f, "b");
		s.AddModifier(StatType.MaxLife, ModifierType.More, 0.2f, "b");
		s.AddModifier(StatType.MaxLife, ModifierType.More, 0.5f, "c");
		// (10 + 5) × (1 + 0.5 + 0.5) × 1.2 × 1.5 = 54
		Check(Near(s.Get(StatType.MaxLife), 54f), $"(10+5)×(1+1.0)×1.2×1.5 = 54 → {s.Get(StatType.MaxLife)}");
		s.RemoveBySource("b");
		// (15) × 1.5 × 1.5 = 33.75
		Check(Near(s.Get(StatType.MaxLife), 33.75f), $"снятие источника 'b' → 33.75 → {s.Get(StatType.MaxLife)}");
		s.AddTag(Tags.Frozen, "x");
		s.AddTag(Tags.Frozen, "y");
		s.RemoveBySource("x");
		Check(s.HasTag(Tags.Frozen), "тег держится, пока есть хоть один источник");
		s.RemoveBySource("y");
		Check(!s.HasTag(Tags.Frozen), "тег снят вместе с последним источником");
		Check(Near(s.Get(StatType.Damage), 1f) && Near(s.Get(StatType.CritChance), 0f), "нейтральные значения по умолчанию");
		s.Free();
	}

	private static void TestData(GameState gs)
	{
		GD.Print("[data]");
		var cfg = gs.Config;
		Check(cfg != null, "game_config.tres загружен");
		Check(cfg.WaveSizes.SequenceEqual(new[] { 10, 15, 20 }), $"волны 10/15/20 → {string.Join("/", cfg.WaveSizes)}");
		Check(Near(cfg.DifficultyPerStep, 0.15f), "сложность +15% за шаг");
		Check(cfg.Player?.Skills.Length == 3 && cfg.Player.Skills.All(s => s != null && s.TotalBaseDamage > 0), "3 умения игрока с уроном > 0");
		Check(cfg.Player.Dash != null && Near(cfg.Player.Dash.DashDistance, 5f), "dash 5 м");
		var ps = new StatBlock();
		cfg.Player.ApplyTo(ps);
		Check(Near(ps.Get(StatType.MaxLife), 100) && Near(ps.Get(StatType.MoveSpeed), 5) && Near(ps.Get(StatType.CritChance), 0.05f)
			&& Near(ps.Get(StatType.CritMultiplier), 1.5f), "базовые статы игрока из .tres");
		ps.Free();
		Check(cfg.Chill?.Modifiers.Length == 2 && cfg.Chill.MaxStacks == 5, "Холод: 2 модификатора, 5 стаков");
		Check(cfg.Shock?.Modifiers.Length == 1 && cfg.Shock.Modifiers[0].Stat == StatType.DamageTaken, "Шок: +DamageTaken");
		Check(cfg.Ignite != null && Near(cfg.Ignite.Duration, 4f) && Near(cfg.Ignite.DamagePerSecondFraction, 0.5f), "Поджог 4 с, 50%/с");
		Check(cfg.Freeze != null && cfg.Freeze.Tag == Tags.Frozen, "Заморозка ставит тег Frozen");
		foreach (var scene in new[] { cfg.SoldierScene, cfg.ArcherScene, cfg.KnightScene, cfg.DummyScene })
		{
			var e = scene?.Instantiate<EnemyBase>();
			Check(e?.Data != null && e.Data.MaxLife > 0, $"сцена врага {scene?.ResourcePath} → {e?.Data?.Id}");
			e?.Free();
		}

		var atlas = gs.Atlas;
		Check(atlas.Nodes.Count == 16, $"16 узлов атласа → {atlas.Nodes.Count}");
		Check(atlas.Nodes.Values.Count(n => n.Type == AtlasNodeType.Start) == 1, "один стартовый узел");
		Check(atlas.Nodes.Values.Count(n => n.Type == AtlasNodeType.Keystone) == 3, "три keystone");
		Check(atlas.Depth.Count == 16, "все узлы достижимы от старта");
		bool symmetric = atlas.Nodes.Values.All(n => n.Neighbors.All(x => atlas.Nodes.ContainsKey(x) && atlas.Nodes[x].Neighbors.Contains(n.Id)));
		Check(symmetric, "соседи указаны симметрично");
		Check(atlas.Nodes.Values.Where(n => n.Type != AtlasNodeType.Start).All(n => n.PassiveModifiers.Length > 0 && n.PassiveDescription.Length > 0),
			"у каждого узла есть пассивка-модификаторы и описание");
		var f1 = atlas.Get("fire_1");
		Check(f1 != null && f1.PassiveModifiers[0].Stat == StatType.FireDamage && f1.PassiveModifiers[0].Type == ModifierType.Increased
			&& Near(f1.PassiveModifiers[0].Value, 0.15f), "fire_1: +15% increased FireDamage");
		var c5 = atlas.Get("cold_5");
		Check(c5 != null && c5.GrantedTags.Contains(Tags.NoIgnite), "cold_5 даёт тег NoIgnite");
		Check(atlas.Nodes.Values.Where(n => n.Type != AtlasNodeType.Start).All(n => n.Tier == atlas.DepthOf(n.Id)),
			"тир узла = шаг от старта (T1 … T5 keystone)");
		Check(Enumerable.Range(1, 5).All(t => atlas.Nodes.Values.Count(n => n.Tier == t) == 3), "по 3 узла каждого тира");

		var mods = gs.Items.MapMods;
		Check(mods.Count == 8, $"8 модов карт → {mods.Count}");
		MapModData Mod(string id) => gs.Items.MapMod(id);
		Check(Near(Mod("crowd").EnemyCountMultiplier, 1.5f) && Near(Mod("crowd").QuantityBonus, 0.2f) && Near(Mod("crowd").RarityBonus, 0f), "Толпа: ×1.5 врагов, +20% кол.");
		Check(Near(Mod("marksmen").ArcherShareMultiplier, 2f) && Mod("marksmen").EnemyModifiers.Any(m => m.Stat == StatType.ProjectileSpeed && Near(m.Value, 0.3f)),
			"Меткие лучники: вдвое больше лучников, стрелы +30%");
		Check(Mod("elite_escort").EliteEscort == 5 && Near(Mod("elite_escort").RarityBonus, 0.15f), "Элита с эскортом: 5 солдат, +15% редк.");
		Check(Mod("fireproof").EnemyModifiers.Any(m => m.Stat == StatType.FireResistance && Near(m.Value, 0.5f))
			&& Mod("coldproof").EnemyModifiers.Any(m => m.Stat == StatType.ColdResistance && Near(m.Value, 0.5f)), "сопротивления огню / холоду 50%");
		Check(Mod("hardened").EnemyModifiers.Any(m => m.Stat == StatType.MaxLife && Near(m.Value, 0.4f)) && Near(Mod("hardened").QuantityBonus, 0.15f), "Закалённые: +40% жизни");
		Check(Mod("brittle_bones").EnemyModifiers.Length == 2 && Mod("fast_dead").EnemyModifiers.Any(m => m.Stat == StatType.MoveSpeed), "Хрупкие кости и Быстрые мертвецы");
	}

	private static void TestDamageMath(GameState gs)
	{
		GD.Print("[passives]");
		var s = new StatBlock();
		gs.Config.Player.ApplyTo(s);
		s.AddModifiers(gs.Atlas.Get("cold_5").PassiveModifiers, "p");
		Check(Near(s.Get(StatType.FrozenHitMultiplier), 2f) && Near(s.Get(StatType.ShatterSplash), 0.5f), "Абсолютный ноль: ×2 по замороженным, 50% раскол");
		s.AddModifiers(gs.Atlas.Get("lightning_5").PassiveModifiers, "p5");
		Check(Near(s.Get(StatType.MaxLife), 70f), $"Гроза: −30% жизни → {s.Get(StatType.MaxLife)}");
		s.AddModifiers(gs.Atlas.Get("fire_4").PassiveModifiers, "p4");
		Check(Near(s.Get(StatType.FireBlastCooldown), 0.8f), "−20% кулдауна Огненного взрыва");
		s.Free();
	}

	private static void TestItems(GameState gs)
	{
		GD.Print("[items]");
		var db = gs.Items;
		var cfg = gs.Config.Loot;
		var gen = new ItemGenerator(db, new RandomNumberGenerator { Seed = 12345 });
		Check(db.Bases.Count == 7 && db.Bases.Select(b => b.Slot).Distinct().Count() == 7, $"7 баз предметов на 7 слотов → {db.Bases.Count}");
		Check(db.Affixes.Count >= 15, $"аффиксов {db.Affixes.Count}");
		Check(db.Affixes.Count(a => a.IsPrefix) >= 7 && db.Affixes.Count(a => !a.IsPrefix) >= 7, $"префиксов {db.Affixes.Count(a => a.IsPrefix)}, суффиксов {db.Affixes.Count(a => !a.IsPrefix)}");
		Check(db.Affixes.All(a => a.TierCount == cfg.MaxTier && !string.IsNullOrEmpty(a.DisplayName)), "у каждого аффикса 5 тиров и имя");
		// T1 слабый, T5 сильный: середина диапазона растёт по модулю (кулдаун — отрицательный).
		bool ascending = db.Affixes.All(a => Enumerable.Range(1, a.TierCount - 1).All(t =>
			Mathf.Abs(a.TierMin[t] + a.TierMax[t]) >= Mathf.Abs(a.TierMin[t - 1] + a.TierMax[t - 1])));
		Check(ascending, "тиры аффиксов растут от T1 к T5");
		var fire = db.Affix("fire_damage");
		Check(fire != null && Near(fire.TierMin[0], 0.05f) && Near(fire.TierMax[0], 0.08f) && Near(fire.TierMin[4], 0.27f) && Near(fire.TierMax[4], 0.35f),
			"урон огнём: T1 5-8%, T5 27-35%");
		Check(db.Affixes.All(a => db.ShardFor(a) != null), "у каждого аффикса свой осколок");
		Check(db.GlyphsAndRunes.Count() == 5 && System.Enum.GetValues<CurrencyAction>().Where(x => x != CurrencyAction.Shard).All(x => db.ByAction(x) != null),
			"2 глифа и 3 руны");
		Check(db.Currency.All(c => c.Icon != null && c.Icon.GetSize() == new Vector2(40, 40)), "иконки валюты 40×40 из атласа");
		foreach (var st in new[] { gs.Config.Ignite, gs.Config.Chill, gs.Config.Freeze, gs.Config.Shock })
			Check(st.Icon != null && st.Icon.GetSize() == new Vector2(40, 40), $"иконка статуса {st.Id}");
		Check(gs.Config.Freeze.ApplyText == "Заморожен!" && gs.Config.Shock.ApplyText == "Шок!" && gs.Config.Ignite.ApplyText == "",
			"всплывающий текст только у Заморозки и Шока");
		var sword = db.Base("sword");
		Check(sword.BaseStat == StatType.WeaponDamage && Near(sword.BaseMin, 10) && Near(sword.BaseMax, 16) && sword.ModelPart == "Sword", "меч: 10-16 урона, часть Sword");
		var life = db.Affix("life");
		Check(life.AllowedSlots.Contains((int)ItemSlot.Helmet) && !life.AllowedSlots.Contains((int)ItemSlot.Weapon), "аффикс жизни: шлем да, оружие нет");

		foreach (ItemSlot slot in System.Enum.GetValues<ItemSlot>())
		{
			int p = db.Affixes.Count(a => a.Allows(slot) && a.IsPrefix), sfx = db.Affixes.Count(a => a.Allows(slot) && !a.IsPrefix);
			Check(p >= cfg.MaxPrefixes && sfx >= cfg.MaxSuffixes, $"слот {slot}: {p} префиксов, {sfx} суффиксов (редкому нужно 2+2)");
		}

		// Фазз генерации: число аффиксов по редкости выпадения, без повторов, слоты, 2+2, тиры по тиру карты, значения, ПК.
		int bad = 0;
		string firstBad = null;
		var rarities = new int[3];
		for (int tier = 1; tier <= 5; tier++)
		for (int i = 0; i < 300; i++)
		{
			var dropRarity = gen.RollRarity();
			var item = gen.Generate(tier, dropRarity);
			rarities[(int)dropRarity]++;
			var (min, max) = gen.AffixRange(dropRarity);
			var pk = dropRarity switch { Rarity.Magic => cfg.MagicPotential, Rarity.Rare => cfg.RarePotential, _ => cfg.NormalPotential };
			string err = null;
			if (item.Affixes.Count < min || item.Affixes.Count > max) err = $"аффиксов {item.Affixes.Count} у {dropRarity}";
			else if (item.Rarity != dropRarity) err = $"редкость {item.Rarity} ≠ {dropRarity}";
			else if (item.Affixes.Select(a => a.Affix).Distinct().Count() != item.Affixes.Count) err = "повтор аффикса";
			else if (item.Affixes.Any(a => !a.Affix.Allows(item.Slot))) err = "аффикс не для слота";
			else if (item.Prefixes > cfg.MaxPrefixes || item.Suffixes > cfg.MaxSuffixes) err = "больше 2 префиксов или суффиксов";
			else if (item.Affixes.Any(a => a.Tier < 1 || a.Tier > gen.MaxDropTier(tier))) err = $"тир выше лимита карты T{tier}";
			else if (item.Affixes.Any(a => a.Value < Mathf.Min(a.Affix.TierMin[a.Tier - 1], a.Affix.TierMax[a.Tier - 1]) - 0.011f
				|| a.Value > Mathf.Max(a.Affix.TierMin[a.Tier - 1], a.Affix.TierMax[a.Tier - 1]) + 0.011f)) err = "значение вне тира";
			else if (item.Base.BaseStat != StatType.None && (item.BaseValue < item.Base.BaseMin - 0.011f || item.BaseValue > item.Base.BaseMax + 0.011f)) err = "база вне диапазона";
			else if (item.Potential < pk.X || item.Potential > pk.Y) err = $"ПК {item.Potential} вне {pk}";
			else if (item.Level != tier) err = "уровень ≠ тир карты";
			if (err != null && bad++ == 0) firstBad = $"{item.Name} T{tier}: {err}";
		}
		Check(bad == 0, bad == 0 ? "1500 предметов: аффиксы, редкость, тиры и ПК корректны" : $"ошибок {bad}, первая: {firstBad}");
		Check(rarities.All(r => r > 0), $"выпадают все редкости: {string.Join("/", rarities)}");
		Check(cfg.MaxAffixTierByMapTier.SequenceEqual(new[] { 1, 2, 3, 3, 4 }), "лимит тира аффиксов: T1→1, T2→2, T3-T4→3, T5→4");
		var t5drops = Enumerable.Range(0, 200).Select(_ => gen.Generate(5, Rarity.Rare)).SelectMany(i => i.Affixes).ToList();
		Check(t5drops.Any(a => a.Tier == 4) && t5drops.All(a => a.Tier <= 4), "карта T5: аффиксы до T4, T5 только крафтом");
		Check(Enumerable.Range(0, 100).Select(_ => gen.Generate(1, Rarity.Rare)).SelectMany(i => i.Affixes).All(a => a.Tier == 1), "карта T1: только T1");

		TestCrafting(gs, gen);
		TestMaps(gs, gen);
		TestInventoryData(gs, gen);
	}

	private static void TestCrafting(GameState gs, ItemGenerator gen)
	{
		GD.Print("[crafting]");
		var db = gs.Items;
		var cfg = gs.Config.Loot;
		CraftCurrencyData Shard(string affix) => db.ShardFor(db.Affix(affix));
		var hope = db.ByAction(CurrencyAction.GlyphHope);
		var chaos = db.ByAction(CurrencyAction.GlyphChaos);
		var removal = db.ByAction(CurrencyAction.RuneRemoval);
		var refinement = db.ByAction(CurrencyAction.RuneRefinement);
		var discovery = db.ByAction(CurrencyAction.RuneDiscovery);
		ForgeResult Forge(object t, CraftCurrencyData s = null, CraftCurrencyData g = null, CraftCurrencyData r = null) => Crafting.Forge(t, s, g, r, gen, db);

		// Осколок: добавляет на T1, повышает тир, тратит 1-8 ПК.
		var helm = gen.Generate(1, Rarity.Normal, db.Base("helmet"));
		int pk = helm.Potential;
		var res = Forge(helm, Shard("life"));
		Check(res.Ok && helm.Roll(db.Affix("life"))?.Tier == 1 && helm.Rarity == Rarity.Magic, $"осколок: жизнь добавлена на T1 («{res.Message}»)");
		Check(res.PotentialSpent is >= 1 and <= 8 && helm.Potential == pk - res.PotentialSpent && res.Consumed.SequenceEqual(new[] { Shard("life") }),
			$"ковка тратит 1-8 ПК: {pk} → {helm.Potential}");
		res = Forge(helm, Shard("life"));
		Check(res.Ok && helm.Roll(db.Affix("life")).Tier == 2 && res.Message.Contains("T1 → T2"), $"осколок: тир 1 → 2 («{res.Message}»)");
		var lifeRoll = helm.Roll(db.Affix("life"));
		Check(lifeRoll.Value >= db.Affix("life").TierMin[1] && lifeRoll.Value <= db.Affix("life").TierMax[1], "значение пересчитано в диапазоне нового тира");
		helm.Potential = 100;
		while (lifeRoll.Tier < cfg.MaxTier && Forge(helm, Shard("life")).Ok) { }
		Check(lifeRoll.Tier == 5 && helm.Rarity == Rarity.Exalted, "тир растёт до T5, предмет становится возвышенным");
		Check(!Forge(helm, Shard("life")).Ok, "выше T5 не повышается");
		Check(!Forge(gen.Generate(1, Rarity.Normal, db.Base("sword")), Shard("life")).Ok, "осколок чужого слота отклоняется (жизнь на мече)");

		// Слоты: 2 префикса + 2 суффикса.
		var ring = gen.Generate(1, Rarity.Normal, db.Base("ring"));
		ring.Potential = 100;
		Forge(ring, Shard("fire_damage"));
		Forge(ring, Shard("cold_damage"));
		res = Forge(ring, Shard("lightning_damage"));
		Check(ring.Prefixes == 2 && !res.Ok && res.Message.Contains("префикс"), $"третий префикс не добавляется («{res.Message}»)");
		Check(Forge(ring, Shard("crit_chance")).Ok && Forge(ring, Shard("attack_speed")).Ok && !Forge(ring, Shard("ailment_chance")).Ok && ring.Affixes.Count == 4,
			"2 суффикса, пятый аффикс не добавляется");
		ring.Potential = 0;
		res = Forge(ring, Shard("fire_damage"));
		Check(!res.Ok && res.Message.Contains("Потенциал"), "на 0 ПК осколки не работают");
		Check(Forge(ring, null, null, refinement).Ok, "на 0 ПК руны работают");

		// Глиф надежды: ~25% ковок без траты ПК.
		int saved = 0;
		var amulet = gen.Generate(1, Rarity.Normal, db.Base("amulet"));
		for (int i = 0; i < 400; i++)
		{
			amulet.Affixes.Clear();
			amulet.Potential = 50;
			var r = Forge(amulet, Shard("damage"), hope);
			if (r.HopeSaved && r.PotentialSpent == 0 && amulet.Potential == 50) saved++;
			if (!r.Consumed.Contains(hope)) saved = -1000;
		}
		Check(saved is > 60 and < 140, $"Глиф надежды: ПК не потрачен в {saved}/400 (≈25%), глиф расходуется");
		Check(!Forge(amulet, null, hope).Ok, "глиф без осколка не работает");

		// Глиф хаоса: тир +1, но аффикс меняется на другой того же типа.
		var boots = gen.Generate(1, Rarity.Normal, db.Base("boots"));
		boots.Potential = 100;
		Forge(boots, Shard("move_speed"));
		res = Forge(boots, Shard("move_speed"), chaos);
		var changed = boots.Affixes.Single();
		Check(res.Ok && changed.Affix != db.Affix("move_speed") && changed.Tier == 2 && !changed.Affix.IsPrefix && changed.Affix.Allows(ItemSlot.Boots),
			$"Глиф хаоса: скорость T1 → {changed.Affix.DisplayName} T{changed.Tier} («{res.Message}»)");
		Check(!Forge(gen.Generate(1, Rarity.Normal, db.Base("boots")), Shard("move_speed"), chaos).Ok, "Глиф хаоса требует имеющийся аффикс");

		// Руны.
		var chest = gen.Generate(3, Rarity.Rare, db.Base("chest"));
		int before = chest.Affixes.Count;
		res = Forge(chest, null, null, removal);
		Check(res.Ok && chest.Affixes.Count == before - 1 && res.Returned.Values.Sum() == 1 && res.Returned.Keys.All(k => db.Currencies(k)?.Kind == CurrencyKind.Shard),
			$"Руна удаления: −1 аффикс, возвращён 1 осколок («{res.Message}»)");
		var tiers = chest.Affixes.Select(a => (a.Affix, a.Tier)).ToList();
		bool rerolled = false;
		for (int i = 0; i < 10; i++)
		{
			var vals = chest.Affixes.Select(a => a.Value).ToList();
			Forge(chest, null, null, refinement);
			rerolled |= !vals.SequenceEqual(chest.Affixes.Select(a => a.Value));
		}
		Check(rerolled && chest.Affixes.Select(a => (a.Affix, a.Tier)).SequenceEqual(tiers)
			&& chest.Affixes.All(a => a.Value >= Mathf.Min(a.Affix.TierMin[a.Tier - 1], a.Affix.TierMax[a.Tier - 1]) - 0.011f
				&& a.Value <= Mathf.Max(a.Affix.TierMin[a.Tier - 1], a.Affix.TierMax[a.Tier - 1]) + 0.011f),
			"Руна перековки: значения переброшены в пределах тиров");
		Check(!Forge(chest, null, null, discovery).Ok, "Руна открытия отклоняет предмет с аффиксами");
		var empty = gen.Generate(1, Rarity.Normal, db.Base("belt"));
		res = Forge(empty, null, null, discovery);
		Check(res.Ok && empty.Affixes.Count is >= 2 and <= 4 && empty.Affixes.All(a => a.Tier <= 2), $"Руна открытия: {empty.Affixes.Count} аффиксов T1-T2");
		Check(!Forge(gen.Generate(1, Rarity.Normal, db.Base("belt")), null, null, removal).Ok, "Руна удаления отклоняет предмет без аффиксов");
		Check(!Forge(empty, Shard("life"), null, removal).Ok, "руна и осколок вместе не применяются");

		// Разборка: T1-T2 по 1, T3-T4 по 2, T5 по 3; 30% потерять один; шанс глифа/руны.
		var sample = gen.Generate(1, Rarity.Normal, db.Base("ring"));
		gen.AddAffix(sample, db.Affix("fire_damage"), 1);
		gen.AddAffix(sample, db.Affix("cold_damage"), 3);
		gen.AddAffix(sample, db.Affix("crit_chance"), 5);
		int lost = 0, extra = 0, wrong = 0;
		for (int i = 0; i < 400; i++)
		{
			var g = Crafting.Disassemble(sample, gen, db);
			int shards = g.Where(kv => db.Currencies(kv.Key).Kind == CurrencyKind.Shard).Sum(kv => kv.Value);
			if (shards == 5) lost++;
			else if (shards != 6) wrong++;
			if (g.Keys.Any(k => db.Currencies(k).Kind != CurrencyKind.Shard)) extra++;
			if (g.Keys.Any(k => db.Currencies(k).Kind == CurrencyKind.Shard && !sample.HasAffix(db.Currencies(k).Affix))) wrong++;
		}
		Check(wrong == 0, "разборка: осколки только аффиксов предмета, 1+2+3 = 6");
		Check(lost is > 80 and < 160, $"разборка: один осколок потерян в {lost}/400 (≈30%)");
		Check(extra is > 15 and < 90, $"разборка: глиф или руна в {extra}/400 (≈19% у редкого — два броска по 10%)");
	}

	private static void TestMaps(GameState gs, ItemGenerator gen)
	{
		GD.Print("[maps]");
		var db = gs.Items;
		var node = gs.Atlas.Get("fire_3");
		ForgeResult Forge(MapInstance m, CraftCurrencyData s = null, CraftCurrencyData g = null, CraftCurrencyData r = null) => Crafting.Forge(m, s, g, r, gen, db);
		var mapRarities = new HashSet<Rarity>();
		for (int i = 0; i < 200; i++)
		{
			var m = gen.GenerateMap(node);
			mapRarities.Add(m.Rarity);
			if (m.Tier != 3 || m.Mods.Distinct().Count() != m.Mods.Count || m.Mods.Count > 4) mapRarities.Add((Rarity)99);
		}
		Check(mapRarities.SetEquals(new[] { Rarity.Normal, Rarity.Magic, Rarity.Rare }), "карты: обычные без модов, магические 1-2, редкие 3-4, тир = тир узла");

		var map = new MapInstance { Node = node, Tier = node.Tier };
		var res = Forge(map, null, null, db.ByAction(CurrencyAction.RuneDiscovery));
		Check(res.Ok && map.Mods.Count is >= 2 and <= 4, $"Руна открытия на карту: модов {map.Mods.Count}");
		Check(Near(map.Quantity, map.Mods.Sum(m => m.QuantityBonus)) && map.Quantity > 0, $"бонус карты к луту: +{map.Quantity * 100:0}% / +{map.RarityBonus * 100:0}%");
		int n = map.Mods.Count;
		Check(Forge(map, null, null, db.ByAction(CurrencyAction.RuneRemoval)).Ok && map.Mods.Count == n - 1, "Руна удаления убирает мод");
		var modsBefore = map.Mods.ToList();
		Check(!Forge(map, null, null, db.ByAction(CurrencyAction.RuneRefinement)).Ok, "Руна перековки без Глифа хаоса на карте не работает");
		res = Forge(map, null, db.ByAction(CurrencyAction.GlyphChaos), db.ByAction(CurrencyAction.RuneRefinement));
		Check(res.Ok && res.Consumed.Count == 2 && map.Mods.Count == modsBefore.Count && !map.Mods.SequenceEqual(modsBefore) && map.Mods.Distinct().Count() == map.Mods.Count,
			$"Глиф хаоса + Руна перековки меняют мод («{res.Message}»)");
		Check(!Forge(map, db.Shards.First()).Ok && !Forge(map, null, db.ByAction(CurrencyAction.GlyphHope), db.ByAction(CurrencyAction.RuneRemoval)).Ok,
			"осколки и Глиф надежды на карты не действуют");

		// Выпадение карт: только пройденные и соседние с ними, тир N и N+1.
		var completed = new HashSet<string> { "fire_1", "fire_2" };
		var eligible = LootSystem.EligibleMapNodes(gs.Atlas, completed, 2).Select(x => x.Id).ToHashSet();
		Check(eligible.SetEquals(new[] { "fire_2", "fire_3" }), $"карта T2 после fire_1-2: {string.Join(",", eligible)}");
		eligible = LootSystem.EligibleMapNodes(gs.Atlas, new HashSet<string>(), 1).Select(x => x.Id).ToHashSet();
		Check(eligible.SetEquals(new[] { "fire_1", "cold_1", "lightning_1" }), "новый атлас: падают только карты T1 у старта");
		Check(gs.CanOpenFree(gs.Atlas.Get("cold_1")) && !gs.CanOpenFree(gs.Atlas.Get("cold_2")) && !gs.CanOpenFree(gs.Atlas.Get("start")), "бесплатно открываются только T1");
	}

	private static void TestInventoryData(GameState gs, ItemGenerator gen)
	{
		GD.Print("[inventory]");
		var db = gs.Items;
		var cfg = gs.Config.Loot;
		var inv = gs.Profile;
		var ring = gen.Generate(2, Rarity.Rare, db.Base("ring"));
		var ring2 = gen.Generate(2, Rarity.Magic, db.Base("ring"));
		inv.AddToStash(ring);
		inv.AddToStash(ring2);
		int ri = System.Array.IndexOf(inv.Stash, ring);
		Check(inv.Equip(ri) && inv.Equipped[ItemSlot.Ring] == ring && inv.Stash[ri] == null, "надеть кольцо из тайника");
		int r2 = System.Array.IndexOf(inv.Stash, ring2);
		Check(inv.Equip(r2) && inv.Equipped[ItemSlot.Ring] == ring2 && inv.Stash[r2] == ring, "надеть второе: старое встаёт на его место");
		inv.AddToStash(gen.Generate(1, Rarity.Normal, db.Base("boots")));
		int bootsIdx = System.Array.FindIndex(inv.Stash, s => s?.Slot == ItemSlot.Boots);
		Check(!inv.Unequip(ItemSlot.Ring, bootsIdx), "снять кольцо в ячейку с сапогами нельзя");
		inv.Take(bootsIdx);
		inv.Move(r2, 59);
		Check(inv.Stash[59] == ring && inv.Stash[r2] == null, "перемещение в ячейку");
		var shard = db.Shards.First();
		inv.AddCurrency(shard.Id, 2);
		inv.AddCurrency("rune_removal", 1);
		var map = gen.GenerateMap(gs.Atlas.Get("cold_2"), Rarity.Rare);
		inv.Maps.Add(map);
		var json = Json.Stringify(inv.ToDict());
		var copy = new Inventory(cfg.StashSize);
		copy.LoadFrom(Json.ParseString(json).AsGodotDictionary(), db, gs.Atlas);
		Check(Json.Stringify(copy.ToDict()) == json, "JSON тайника, экипировки, валюты и карт: загрузка = сохранение");
		var loadedRing = copy.Stash[59];
		Check(loadedRing.Uid == ring.Uid && loadedRing.Potential == ring.Potential && loadedRing.Affixes.Select(a => (a.Affix, a.Tier)).SequenceEqual(ring.Affixes.Select(a => (a.Affix, a.Tier)))
			&& copy.CurrencyCount(shard) == 2 && copy.CurrencyCount("rune_removal") == 1,
			"после загрузки: аффиксы с тирами, ПК, валюта на месте");
		Check(copy.Maps.Count == 1 && copy.Maps[0].Node == map.Node && copy.Maps[0].Mods.SequenceEqual(map.Mods), "после загрузки: карта с модами на месте");
		inv.Take(59);
		inv.Equipped.Remove(ItemSlot.Ring);
		inv.AddCurrency(shard.Id, -2);
		inv.AddCurrency("rune_removal", -1);
		inv.Maps.Remove(map);
		Check(inv.Stash[59] == null && inv.CurrencyTotal == 0 && inv.Maps.Count == 0, "тайник очищен после проверки");
	}

	private static async Task Frames(GameState gs, int n)
	{
		for (int i = 0; i < n; i++)
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.PhysicsFrame);
	}

	private static async Task TestArena(GameState gs)
	{
		GD.Print("[arena]");
		gs.Completed.Clear();
		gs.Completed.Add("fire_1");
		gs.EnterNode("cold_1");
		ArenaScene arena = null;
		for (int i = 0; i < 60 && arena == null; i++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			arena = gs.GetTree().CurrentScene as ArenaScene;
		}
		Check(arena != null, "переход атлас → арена");
		if (arena == null) return;

		Check(arena.Navigation.NavigationMesh.GetPolygonCount() > 0, $"навмеш запечён: {arena.Navigation.NavigationMesh.GetPolygonCount()} полигонов");
		int obstacles = arena.Navigation.GetChildren().Count(c => c is StaticBody3D b && !b.Name.ToString().StartsWith("Wall") && b.Name != "Floor");
		Check(obstacles >= gs.Config.ObstacleMin && obstacles <= gs.Config.ObstacleMax, $"препятствий {obstacles}");

		var player = arena.Player;
		Check(Near(player.Stats.Get(StatType.FireDamage), 1.15f), "пассивка fire_1 действует в бою: FireDamage ×1.15");

		await Frames(gs, 120);
		Check(EnemyBase.Alive.Count > 0, $"волна заспавнилась: {EnemyBase.Alive.Count} врагов");
		Check(arena.Spawner.Wave == 1, "идёт волна 1");
		var enemy = EnemyBase.Alive.FirstOrDefault();
		if (enemy == null) return;
		Check(Near(enemy.Stats.Get(StatType.MaxLife), enemy.Data.MaxLife), $"карта T1: жизнь врагов без бонуса сложности → {enemy.Stats.Get(StatType.MaxLife)}");
		bool anyPath = EnemyBase.Alive.Any(e => e.Agent.GetCurrentNavigationPath().Length > 0);
		Check(anyPath, "агенты получили путь по навмешу");

		// Виджеты врагов: пул по числу живых, полоса скрыта до первого урона.
		var widgets = arena.Widgets;
		Check(widgets.ActiveCount == EnemyBase.Alive.Count, $"виджетов {widgets.ActiveCount} на {EnemyBase.Alive.Count} врагов");
		var w0 = widgets.WidgetOf(enemy);
		await Frames(gs, 2);
		Check(w0 != null && !w0.Visible && !enemy.WasHit, "полоса жизни скрыта до первого урона");
		enemy.GlobalPosition = player.GlobalPosition + new Vector3(3, 0, 0);
		DamageSystem.Apply(DamageSystem.BuildRaw(player.Stats, player, DamageType.Physical, 1f, false), enemy);
		DamageSystem.ApplyChill(enemy, player.Stats);
		DamageSystem.ApplyChill(enemy, player.Stats);
		enemy.Status.Apply(gs.Config.Ignite, player.Stats, 1f);
		await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
		await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(w0.Visible && w0.IconCount == 2 && w0.ChillStackText == "2", $"после урона: виджет виден, иконок {w0.IconCount}, стаков Холода «{w0.ChillStackText}»");
		var chillFx = enemy.Status.Get(gs.Config.Chill.Id);
		Check(chillFx != null && chillFx.Duration > 0 && chillFx.Remaining <= chillFx.Duration, "у статуса есть полная длительность для кругового таймера");
		enemy.Status.Remove(gs.Config.Chill.Id);
		enemy.Status.Remove(gs.Config.Ignite.Id);
		// Предмет в сумке забега — сгорит при смерти.
		var doomed = gs.Generator.Generate(1, Rarity.Rare);
		Check(gs.Bag != null && gs.Bag.TryAdd(doomed), "сумка забега создана при входе в узел");
		int stashBefore = gs.Profile.Stash.Count(s => s != null);

		// Удар мечом по замороженному врагу рядом с игроком.
		DamageSystem.ApplyChill(enemy, player.Stats);
		for (int i = 0; i < 5 && !enemy.Stats.HasTag(Tags.Frozen); i++) DamageSystem.ApplyChill(enemy, player.Stats);
		Check(enemy.Stats.HasTag(Tags.Frozen), "5 стаков Холода → Заморозка");
		enemy.GlobalPosition = player.GlobalPosition + new Vector3(0, 0, -1.2f);
		await Frames(gs, 3);
		var hits = Hitbox.OverlapSphere(player.GetWorld3D(), enemy.GlobalPosition + Vector3.Up, 0.5f, Layers.EnemyHurtbox);
		Check(hits.Contains(enemy), "Hitbox находит Hurtbox врага через физику (Jolt)");
		float before = enemy.Stats.CurrentLife;
		bool cast = player.Skills.TryCast(0, enemy.GlobalPosition);
		Check(cast && enemy.Stats.CurrentLife < before, $"Удар мечом: {before} → {enemy.Stats.CurrentLife}");

		enemy.Status.Apply(gs.Config.Shock, player.Stats);
		Check(enemy.Stats.HasTag(Tags.Shocked) && Near(enemy.Stats.Get(StatType.DamageTaken), 1.2f), "Шок: +20% получаемого урона");
		enemy.Status.Apply(gs.Config.Ignite, player.Stats, 20f);
		float lifeBeforeIgnite = enemy.Stats.CurrentLife;
		await Frames(gs, 40);
		Check(enemy.IsDead || enemy.Stats.CurrentLife < lifeBeforeIgnite, "Поджог наносит урон тиками");

		// Кулдаун в HUD-данных.
		Check(player.Skills.CooldownRemaining(0) >= 0f, "кулдауны читаются");

		// Смерть игрока → экран смерти, узел не засчитан.
		player.Stats.RemoveBySource("dash");
		player.Stats.TakeDamage(99999);
		await Frames(gs, 2);
		Check(gs.GetTree().Paused && !gs.Completed.Contains("cold_1"), "смерть: пауза, узел не засчитан");
		var result = FindOfType<ResultScreen>(arena);
		Check(result != null && result.IsShown && result.BodyText.Contains(doomed.Name), "экран смерти показывает потерянное");

		gs.ReturnToAtlas();
		for (int i = 0; i < 60 && gs.GetTree().CurrentScene is not AtlasScreen; i++)
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(gs.GetTree().CurrentScene is AtlasScreen, "возврат на атлас");
		Check(!gs.GetTree().Paused, "пауза снята");
		Check(gs.Bag == null && gs.Profile.Stash.Count(s => s != null) == stashBefore && !gs.Profile.Stash.Contains(doomed),
			"смерть: сумка сгорела, тайник не изменился");

		// Сохранение переживает перезагрузку.
		gs.CompleteNode("cold_1");
		var loaded = SaveSystem.Instance.Load()["completed"].AsStringArray();
		Check(loaded.Contains("cold_1") && loaded.Contains("fire_1"), "сейв содержит пройденные узлы");
		Check(gs.StateOf("cold_2") == AtlasNodeState.Available && gs.StateOf("cold_3") == AtlasNodeState.Locked, "пройденный узел открывает соседа");

		await TestInventoryUi(gs);
		await TestFullRun(gs);
		await TestKeystone(gs);
		await TestProcs(gs);
	}

	/// <summary>Умения и особые эффекты пассивок на полигоне с манекенами.</summary>
	private static async Task TestProcs(GameState gs)
	{
		GD.Print("[skills & procs]");
		// Без экипировки: проверяем базовые числа умений из ТЗ.
		var savedEquip = gs.Profile.Equipped.ToDictionary(kv => kv.Key, kv => kv.Value);
		gs.Profile.Equipped.Clear();
		gs.GetTree().Paused = false;
		gs.GetTree().ChangeSceneToFile("res://Scenes/Sandbox.tscn");
		ArenaScene arena = null;
		for (int i = 0; i < 60 && arena == null; i++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			arena = gs.GetTree().CurrentScene as ArenaScene;
		}
		if (arena == null || !arena.SandboxMode)
		{
			Check(false, "полигон загружен");
			return;
		}
		await Frames(gs, 5);
		var player = arena.Player;
		var p0 = new Vector3(10, 0, 10); // подальше от стандартных манекенов полигона
		player.GlobalPosition = p0;
		player.Stats.AddModifier(StatType.CritChance, ModifierType.Flat, -1f, "test");
		var skills = player.Skills;
		var dummies = new List<EnemyBase>();

		EnemyBase Dummy(Vector3 offset)
		{
			var d = arena.Spawner.Spawn(gs.Config.DummyScene, p0 + offset);
			dummies.Add(d);
			return d;
		}

		async Task Reset(params string[] passives)
		{
			foreach (var d in dummies) if (GodotObject.IsInstanceValid(d)) d.QueueFree();
			dummies.Clear();
			foreach (var id in new[] { "cold_4", "lightning_3", "lightning_5", "cold_5", "fire_3", "fire_5", "lightning_4" })
				player.Stats.RemoveBySource("passive:" + id);
			player.Stats.RemoveBySource("test_ailment");
			foreach (var id in passives) player.ApplyPassives(new[] { gs.Atlas.Get(id) });
			player.GlobalPosition = p0;
			skills.ResetCooldowns();
			await Frames(gs, 3);
		}

		float Lost(EnemyBase d) => d.Stats.Get(StatType.MaxLife) - d.Stats.CurrentLife;
		int Frames4(float dist) => Mathf.CeilToInt(dist / 15f * 60f) + 10;

		// Огненный взрыв: 25 в круге 3 м, мимо — 0.
		await Reset();
		var a = Dummy(new Vector3(0, 0, 5));
		var b = Dummy(new Vector3(4.6f, 0, 5));
		await Frames(gs, 3);
		skills.TryCast(2, a.GlobalPosition);
		Check(Near(Lost(a), 25f) && Near(Lost(b), 0f), $"Огненный взрыв: в круге −{Lost(a)}, за кругом −{Lost(b)}");

		// Ледяной осколок: 10 холодом.
		await Reset();
		a = Dummy(new Vector3(0, 0, 4));
		await Frames(gs, 3);
		skills.TryCast(1, a.GlobalPosition);
		await Frames(gs, Frames4(4));
		Check(Near(Lost(a), 10f), $"Ледяной осколок: −{Lost(a)}");

		// Хрустальная крипта: пробивает 1 цель.
		await Reset("cold_4");
		a = Dummy(new Vector3(0, 0, 3));
		b = Dummy(new Vector3(0, 0, 6));
		await Frames(gs, 3);
		skills.TryCast(1, b.GlobalPosition);
		await Frames(gs, Frames4(6));
		Check(Lost(a) > 0 && Lost(b) > 0, $"Пробитие: первая −{Lost(a)}, вторая −{Lost(b)}");

		// Разряд: +50% молнией и перескок на 2 цели.
		await Reset("lightning_3");
		a = Dummy(new Vector3(0, 0, 3));
		b = Dummy(new Vector3(3, 0, 3));
		var c = Dummy(new Vector3(-3, 0, 3));
		await Frames(gs, 3);
		skills.TryCast(1, a.GlobalPosition);
		await Frames(gs, Frames4(3));
		Check(Near(Lost(a), 15f) && Lost(b) > 0 && Lost(c) > 0, $"Разряд: цель −{Lost(a)}, перескоки −{Lost(b)} / −{Lost(c)}");

		// Гроза: каждый 4-й каст бьёт молнией ближайших.
		await Reset("lightning_5");
		a = Dummy(new Vector3(0, 0, 3));
		b = Dummy(new Vector3(3, 0, 0));
		await Frames(gs, 3);
		for (int i = 0; i < 4; i++)
		{
			skills.ResetCooldowns();
			skills.TryCast(0, p0 + new Vector3(0, 0, -5));
			await Frames(gs, 1);
		}
		Check(Lost(a) > 0 && Lost(b) > 0, $"Гроза: −{Lost(a)} / −{Lost(b)}");
		Check(Near(player.Stats.Get(StatType.MaxLife), 70f), "Гроза: −30% жизни игрока");

		// Абсолютный ноль: ×2 по замороженной и раскол на соседей.
		await Reset("cold_5");
		a = Dummy(new Vector3(0, 0, 1.5f));
		b = Dummy(new Vector3(0, 0, 3.2f));
		await Frames(gs, 3);
		a.Status.Apply(gs.Config.Freeze, player.Stats);
		skills.TryCast(0, a.GlobalPosition);
		Check(Near(Lost(a), 24f) && Lost(b) > 0 && !a.Stats.HasTag(Tags.Frozen), $"Раскол: цель −{Lost(a)} (×2), сосед −{Lost(b)}, заморозка снята");

		// Пожарище: Поджог переходит при смерти.
		await Reset("fire_3");
		a = Dummy(new Vector3(0, 0, 4));
		b = Dummy(new Vector3(1.2f, 0, 4));
		await Frames(gs, 3);
		a.Status.Apply(gs.Config.Ignite, player.Stats, 20f);
		Check(Near(a.Status.Get("ignite").Remaining, 6f), $"Пожарище: Поджог 6 с → {a.Status.Get("ignite").Remaining}");
		a.Stats.TakeDamage(99999);
		Check(b.Stats.HasTag(Tags.Ignited), "Пожарище: Поджог перешёл на соседа при смерти");

		// Вечное пламя: +50% огнём, Холод не накладывается.
		await Reset("fire_5");
		player.Stats.AddModifier(StatType.AilmentChance, ModifierType.Flat, 1f, "test_ailment");
		a = Dummy(new Vector3(0, 0, 4));
		await Frames(gs, 3);
		skills.TryCast(1, a.GlobalPosition);
		await Frames(gs, Frames4(4) - 5);
		Check(!a.Stats.HasTag(Tags.Chilled) && !a.Stats.HasTag(Tags.Frozen) && a.Stats.HasTag(Tags.Ignited), "Вечное пламя: осколок поджигает, но не холодит");

		// Абсолютный ноль: Поджог невозможен.
		await Reset("cold_5");
		player.Stats.AddModifier(StatType.AilmentChance, ModifierType.Flat, 1f, "test_ailment");
		a = Dummy(new Vector3(0, 0, 4));
		await Frames(gs, 3);
		skills.TryCast(2, a.GlobalPosition);
		Check(Lost(a) > 0 && !a.Stats.HasTag(Tags.Ignited), "Абсолютный ноль: взрыв не поджигает");

		// Рывок: неуязвимость на время рывка, +1 заряд от Громовой башни.
		await Reset("lightning_4");
		Check(player.Dash.MaxCharges == 2 && player.Dash.Charges == 2, $"Громовая башня: зарядов {player.Dash.Charges}/{player.Dash.MaxCharges}");
		var start = player.GlobalPosition;
		player.Dash.TryDash(Vector3.Forward);
		float dealt = DamageSystem.Apply(DamageSystem.BuildRaw(player.Stats, null, DamageType.Physical, 50, false), player);
		Check(dealt == 0f && player.Stats.HasTag(Tags.Invulnerable), "рывок: неуязвимость");
		await Frames(gs, 15);
		Check(!player.Stats.HasTag(Tags.Invulnerable) && Mathf.Abs(player.GlobalPosition.DistanceTo(start) - 5f) < 0.2f,
			$"рывок: 5 м за 0.15 с → {player.GlobalPosition.DistanceTo(start):0.00} м");
		Check(player.Dash.Charges == 1, "рывок тратит заряд");

		// Экипировка в бою: меч 15, шлем с доп. снарядом осколка, видимые части.
		GD.Print("[equipment]");
		await Reset();
		var db = gs.Items;
		var sword = gs.Generator.Generate(1, Rarity.Normal, db.Base("sword"));
		sword.BaseValue = 15;
		var helmet = gs.Generator.Generate(1, Rarity.Normal, db.Base("helmet"));
		helmet.Affixes.Add(new AffixRoll { Affix = db.Affix("shard_projectile"), Tier = 3, Value = 1 });
		float lifeBefore = player.Stats.Get(StatType.MaxLife);
		player.ApplyEquipment(new[] { sword, helmet });
		Check(Near(player.Stats.Get(StatType.WeaponDamage), 15f) && Near(player.Stats.Get(StatType.MaxLife), lifeBefore + helmet.BaseValue),
			$"статы экипировки: меч {player.Stats.Get(StatType.WeaponDamage)}, жизнь {lifeBefore} → {player.Stats.Get(StatType.MaxLife)}");
		Check(player.Visual.IsPartVisible("Sword") && player.Visual.IsPartVisible("SK_Helmet") && !player.Visual.IsPartVisible("SK_Belt"),
			"надетое видно на модели (меч, шлем), не надетое скрыто (пояс)");
		a = Dummy(new Vector3(0, 0, 1.5f));
		await Frames(gs, 3);
		skills.TryCast(0, a.GlobalPosition);
		Check(Near(Lost(a), 15f), $"Удар мечом с оружием 15: −{Lost(a)}");
		skills.ResetCooldowns();
		skills.TryCast(1, p0 + new Vector3(0, 0, 8));
		int shards = arena.GetChildren().Count(c => c is Projectile);
		Check(shards == 2, $"+1 снаряд Ледяного осколка: снарядов {shards}");
		player.Stats.RemoveBySource(sword.SourceId);
		player.Stats.RemoveBySource(helmet.SourceId);

		await Reset();
		player.Stats.RemoveBySource("test");
		gs.Profile.Equipped.Clear();
		foreach (var (slot, item) in savedEquip) gs.Profile.Equipped[slot] = item;
	}

	private static async Task<ArenaScene> Enter(GameState gs, string id)
	{
		gs.EnterNode(id);
		ArenaScene arena = null;
		for (int i = 0; i < 60 && arena == null; i++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			arena = gs.GetTree().CurrentScene as ArenaScene;
		}
		return arena;
	}

	private static async Task Leave(GameState gs)
	{
		gs.ReturnToAtlas();
		for (int i = 0; i < 60 && gs.GetTree().CurrentScene is not AtlasScreen; i++)
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
	}

	/// <summary>Три волны → элита → победа, узел засчитан.</summary>
	private static async Task TestFullRun(GameState gs)
	{
		GD.Print("[full run]");
		// Вход через устройство карт: карта cold_2 без модов расходуется.
		var map = new MapInstance { Node = gs.Atlas.Get("cold_2"), Tier = 2 };
		gs.Profile.Maps.Add(map);
		if (gs.GetTree().CurrentScene is AtlasScreen atlasScreen)
		{
			atlasScreen.Panel.ShowDevice(map);
			Check(atlasScreen.Panel.DeviceMode && atlasScreen.Panel.SelectedMap == map, "устройство карт: карта в слоте");
		}
		Check(!gs.OpenMap(new MapInstance { Node = gs.Atlas.Get("cold_2"), Tier = 2 }), "нельзя открыть карту, которой нет в тайнике");
		Check(gs.OpenMap(map) && !gs.Profile.Maps.Contains(map), "«Открыть»: карта израсходована");
		ArenaScene arena = null;
		for (int i = 0; i < 60 && arena == null; i++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			arena = gs.GetTree().CurrentScene as ArenaScene;
		}
		if (arena == null)
		{
			Check(false, "вход в cold_2");
			return;
		}
		Check(arena.Map == map && arena.Node.Id == "cold_2", "забег по карте из устройства");
		var waves = new HashSet<int>();
		int spawnedTotal = 0;
		void OnSpawn(Node3D _) => spawnedTotal++;
		EventBus.Instance.EnemySpawned += OnSpawn;
		for (int frame = 0; frame < 60 * 60 && !gs.Completed.Contains("cold_2"); frame++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.PhysicsFrame);
			waves.Add(arena.Spawner.Wave);
			if (frame % 20 == 0 && arena.Spawner.Running)
				foreach (var e in EnemyBase.Alive.ToList()) e.Stats.TakeDamage(99999);
		}
		EventBus.Instance.EnemySpawned -= OnSpawn;
		Check(waves.Contains(1) && waves.Contains(2) && waves.Contains(3), $"пройдены волны {string.Join(",", waves.Where(w => w > 0))}");
		// cold_2 без модификаторов: 10 + 15 + 20 + рыцарь.
		Check(spawnedTotal == 46, $"заспавнено {spawnedTotal} (10+15+20+элита)");
		Check(gs.Completed.Contains("cold_2"), "убийство элиты засчитывает узел");
		Check(gs.GetTree().Paused, "экран победы (пауза)");
		// Элита: 2 предмета (первый минимум магический) + 1 сфера сразу в сумку, при победе — в тайник.
		var stash = gs.Profile.Stash.Where(s => s != null).ToList();
		Check(stash.Count >= 2 && stash.Any(i => i.Rarity >= Rarity.Magic) && gs.Profile.CurrencyTotal >= 2,
			$"победа: лут элиты в тайнике ({stash.Count} предм., валюты {gs.Profile.CurrencyTotal})");
		// Элита: 1 карта гарантированно, узел соседний с пройденными, тир 2 или 3.
		Check(gs.Profile.Maps.Count >= 1 && gs.Profile.Maps.All(m => m.Tier is 2 or 3 && gs.StateOf(m.Node.Id) != AtlasNodeState.Locked),
			$"победа: карты в тайнике ({string.Join(", ", gs.Profile.Maps.Select(m => m.Node.Id + " T" + m.Tier))})");
		gs.Profile.Maps.Clear();
		await Leave(gs);
		Check(gs.StateOf("cold_3") == AtlasNodeState.Available, "после победы открыт следующий узел");
	}

	private static async Task TestKeystone(GameState gs)
	{
		GD.Print("[keystone]");
		gs.Completed.UnionWith(new[] { "fire_1", "fire_2", "fire_3", "fire_4" });
		var arena = await Enter(gs, "fire_5");
		if (arena == null)
		{
			Check(false, "вход в fire_5");
			return;
		}
		for (int i = 0; i < 200 && arena.Spawner.Elite == null; i++)
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.PhysicsFrame);
		await Frames(gs, 60);
		var elite = arena.Spawner.Elite;
		// 200 × 3 × (1 + 0.15 × 4): +15% за каждый тир выше T1.
		Check(elite != null && Near(elite.Stats.Get(StatType.MaxLife), 960f), $"keystone: рыцарь с жизнью ×3 и сложностью T5 → {elite?.Stats.Get(StatType.MaxLife)}");
		Check(EnemyBase.Alive.Count == 1 + gs.Config.KeystoneEscort, $"keystone: эскорт {EnemyBase.Alive.Count - 1}");
		Check(arena.Player.Stats.HasTag(Tags.NoChill) == false, "пассивка keystone ещё не активна до победы");
		if (elite == null) return;

		// Полоса элиты с именем видна сразу, без урона.
		elite.GlobalPosition = arena.Player.GlobalPosition + new Vector3(4, 0, 0);
		elite.Status.Apply(gs.Config.Freeze, arena.Player.Stats);
		await Frames(gs, 3);
		var ew = arena.Widgets.WidgetOf(elite);
		Check(ew != null && ew.Visible && !elite.WasHit, "виджет элиты виден сразу, без урона");

		// Лут босса keystone: 3 предмета (минимум один редкий), 5 осколков, 1 глиф/руна, 2 карты — в тайник.
		var stashBefore = gs.Profile.Stash.Where(s => s != null).ToHashSet();
		int currencyBefore = gs.Profile.CurrencyTotal;
		int mapsBefore = gs.Profile.Maps.Count;
		elite.Stats.TakeDamage(999999);
		await Frames(gs, 3);
		var gained = gs.Profile.Stash.Where(s => s != null && !stashBefore.Contains(s)).ToList();
		Check(gained.Count == gs.Config.Loot.KeystoneItems && gained.Any(i => i.Rarity == Rarity.Rare),
			$"keystone: получено {gained.Count} предм., редкость {string.Join(",", gained.Select(i => i.Rarity))}");
		var lc = gs.Config.Loot;
		Check(gs.Profile.CurrencyTotal - currencyBefore == lc.KeystoneShards + lc.KeystoneGlyphRunes,
			$"keystone: {lc.KeystoneShards} осколков + {lc.KeystoneGlyphRunes} глиф/руна → +{gs.Profile.CurrencyTotal - currencyBefore}");
		Check(gs.Profile.Maps.Count - mapsBefore == lc.KeystoneMaps, $"keystone: {lc.KeystoneMaps} карты в тайнике");
		Check(gained.All(i => i.Level == 5), "уровень предметов = шаг узла (5)");
		var result = FindOfType<ResultScreen>(arena);
		Check(result.IsShown && gained.All(i => result.BodyText.Contains(i.Name)), "экран победы перечисляет полученное");
		await Leave(gs);
		await TestBagFull(gs);
	}

	/// <summary>Действия экрана инвентаря: перетаскивание, двойной клик, сферы, сохранение.</summary>
	private static async Task TestInventoryUi(GameState gs)
	{
		GD.Print("[inventory ui]");
		var atlas = gs.GetTree().CurrentScene as AtlasScreen;
		if (atlas == null)
		{
			Check(false, "атлас открыт");
			return;
		}
		var ui = atlas.Inventory;
		var inv = gs.Profile;
		var db = gs.Items;
		ui.Open();
		await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
		Check(ui.Visible && ui.Cells.Count == 60 && ui.Doll.Count == 7, "инвентарь: сетка 10×6 и 7 слотов куклы");

		var savedEquip = inv.Equipped.ToDictionary(kv => kv.Key, kv => kv.Value);
		inv.Equipped.Clear();
		var helm = gs.Generator.Generate(2, Rarity.Normal, db.Base("helmet"));
		var helm2 = gs.Generator.Generate(2, Rarity.Magic, db.Base("helmet"));
		var boots = gs.Generator.Generate(2, Rarity.Normal, db.Base("boots"));
		inv.Stash[0] = helm;
		inv.Stash[1] = helm2;
		inv.Stash[2] = boots;
		ItemCell Slot(ItemSlot s) => ui.Doll.First(c => c.DollSlot == s);
		Godot.Collections.Dictionary FromStash(int i) => new() { ["doll"] = false, ["index"] = i, ["slot"] = 0 };
		Godot.Collections.Dictionary FromDoll(ItemSlot s) => new() { ["doll"] = true, ["index"] = -1, ["slot"] = (int)s };

		Check(!ui.CanDrop(Slot(ItemSlot.Boots), FromStash(0)), "шлем нельзя бросить в слот сапог");
		Check(ui.CanDrop(Slot(ItemSlot.Helmet), FromStash(0)), "шлем можно бросить в слот шлема");
		ui.Drop(Slot(ItemSlot.Helmet), FromStash(0));
		Check(inv.Equipped.GetValueOrDefault(ItemSlot.Helmet) == helm && inv.Stash[0] == null, "перетаскивание на куклу надевает");
		Check(!ui.CanDrop(ui.Cells[2], FromDoll(ItemSlot.Helmet)), "снять шлем в ячейку с сапогами нельзя");
		ui.Drop(ui.Cells[1], FromDoll(ItemSlot.Helmet));
		Check(inv.Equipped[ItemSlot.Helmet] == helm2 && inv.Stash[1] == helm, "снять на ячейку с другим шлемом — обмен");
		ui.Drop(ui.Cells[10], FromDoll(ItemSlot.Helmet));
		Check(!inv.Equipped.ContainsKey(ItemSlot.Helmet) && inv.Stash[10] == helm2, "снять в пустую ячейку");
		ui.Drop(ui.Cells[20], FromStash(2));
		Check(inv.Stash[20] == boots && inv.Stash[2] == null, "перемещение внутри тайника");
		ui.ToggleEquip(ui.Cells[20]);
		Check(inv.Equipped.GetValueOrDefault(ItemSlot.Boots) == boots, "двойной клик надевает");
		ui.ToggleEquip(Slot(ItemSlot.Boots));
		Check(!inv.Equipped.ContainsKey(ItemSlot.Boots) && inv.Stash.Contains(boots), "двойной клик по кукле снимает");

		// Окно крафта: валюта тратится только при успехе, сейв обновлён.
		var fireShard = db.ShardFor(db.Affix("fire_damage"));
		var lifeShard = db.ShardFor(db.Affix("life"));
		var hope = db.ByAction(CurrencyAction.GlyphHope);
		inv.AddCurrency(fireShard.Id, 1);
		inv.AddCurrency(hope.Id, 1);
		inv.AddCurrency(lifeShard.Id, 1);
		ui.OpenCraft(helm);
		var craft = ui.Craft;
		Check(craft.Visible && craft.Target == helm, "окно крафта открыто для предмета");
		craft.SelectShard(fireShard);
		craft.SelectGlyph(hope);
		int pkBefore = helm.Potential;
		var res = craft.Forge();
		Check(res.Ok && helm.Roll(db.Affix("fire_damage"))?.Tier == 1 && inv.CurrencyCount(fireShard) == 0 && inv.CurrencyCount(hope) == 0,
			$"ковка через окно: огонь T1, осколок и глиф потрачены («{craft.LastMessage}»)");
		Check(helm.Potential == pkBefore - res.PotentialSpent && craft.LastMessage.Contains("ПК"), "строка результата с потраченным ПК");
		Check(craft.Shard == null && craft.Glyph == null, "выбор снят, когда валюта кончилась");
		var saved = SaveSystem.Instance.Load()["inventory"].AsGodotDictionary()["stash"].AsGodotDictionary();
		int helmIdx = System.Array.IndexOf(inv.Stash, helm);
		Check(saved.ContainsKey(helmIdx.ToString()) && saved[helmIdx.ToString()].AsGodotDictionary()["affixes"].AsGodotArray().Count == 1, "сейв на диске отражает крафт");
		craft.Open(gs.Generator.Generate(1, Rarity.Normal, db.Base("sword")));
		craft.SelectShard(lifeShard);
		Check(!craft.Forge().Ok && inv.CurrencyCount(lifeShard) == 1, $"неудачная ковка не тратит осколок («{craft.LastMessage}»)");
		inv.AddCurrency(lifeShard.Id, -1);
		craft.Close();

		// Разборка: одиночная и массовая, надетое не разбирается.
		var junk = gs.Generator.Generate(3, Rarity.Rare, db.Base("amulet"));
		var junk2 = gs.Generator.Generate(3, Rarity.Magic, db.Base("ring"));
		var junk3 = gs.Generator.Generate(3, Rarity.Magic, db.Base("belt"));
		inv.Stash[30] = junk;
		inv.Stash[31] = junk2;
		inv.Stash[32] = junk3;
		int currencyBefore = inv.CurrencyTotal;
		var gains = ui.Disassemble(new[] { 30 });
		Check(inv.Stash[30] == null && gains.Values.Sum() >= junk.Affixes.Count - 1 && inv.CurrencyTotal == currencyBefore + gains.Values.Sum(),
			$"разборка: предмет убран, валюта +{gains.Values.Sum()} («{ui.LastMessage}»)");
		ui.EnterMassMode();
		ui.ToggleSelected(31);
		ui.ToggleSelected(32);
		ui.ToggleSelected(40);
		Check(ui.MassMode && ui.SelectedCount == 2 && ui.IsSelected(31), "массовая разборка: выделено 2 предмета, пустая ячейка не выделяется");
		ui.Disassemble(new[] { 31, 32 });
		ui.ExitMassMode();
		Check(inv.Stash[31] == null && inv.Stash[32] == null && !ui.MassMode, "массовая разборка: оба предмета разобраны");
		foreach (var id in inv.Currency.Keys.ToList()) inv.AddCurrency(id, -inv.CurrencyCount(id));

		var crit = new ItemInstance { Base = db.Base("ring") };
		crit.Affixes.Add(new AffixRoll { Affix = db.Affix("crit_multi"), Tier = 3, Value = 0.15f });
		Check(ItemText.Compare(crit, null).Length > 0 && ItemText.Compare(crit, new ItemInstance { Base = db.Base("ring") }).Contains("+15% урон крита"),
			"сравнение: урон крита в процентах");
		var fireRoll = new AffixRoll { Affix = db.Affix("fire_damage"), Tier = 3, Value = 0.16f };
		Check(ItemText.AffixFull(fireRoll) == "T3 +16% к урону огнём (14-19%)", $"тултип аффикса: «{ItemText.AffixFull(fireRoll)}»");
		Check(ItemText.Describe(helm).Contains("Потенциал ковки"), "тултип показывает ПК");

		ui.Close();
		for (int i = 0; i < inv.Stash.Length; i++) inv.Stash[i] = null;
		inv.Equipped.Clear();
		foreach (var (slot, item) in savedEquip) inv.Equipped[slot] = item;
		gs.SaveGame();
	}

	/// <summary>Полная сумка: предметы не подбираются, сферы — всегда.</summary>
	private static async Task TestBagFull(GameState gs)
	{
		GD.Print("[map mods & bag]");
		// Карта cold_3 (T3) с модами: Толпа, Закалённые, Огнеупорные.
		var map = new MapInstance { Node = gs.Atlas.Get("cold_3"), Tier = 3 };
		foreach (var id in new[] { "crowd", "hardened", "fireproof" }) map.Mods.Add(gs.Items.MapMod(id));
		gs.EnterMap(map);
		ArenaScene arena = null;
		for (int i = 0; i < 60 && arena == null; i++)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			arena = gs.GetTree().CurrentScene as ArenaScene;
		}
		if (arena == null)
		{
			Check(false, "вход в cold_3");
			return;
		}
		arena.Player.Stats.AddTag(Tags.Invulnerable, "test");
		Check(Near(arena.Loot.Context.Quantity, 0.43f) && Near(arena.Loot.Context.RarityBonus, 0.18f), $"бонус лута карты: +{arena.Loot.Context.Quantity * 100:0}% / +{arena.Loot.Context.RarityBonus * 100:0}%");
		for (int i = 0; i < 400 && EnemyBase.Alive.Count < 15; i++) await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.PhysicsFrame);
		await Frames(gs, 20);
		Check(EnemyBase.Alive.Count == 15 && arena.Spawner.Wave == 1, $"Толпа: первая волна {EnemyBase.Alive.Count} врагов (10 × 1.5)");
		var modded = EnemyBase.Alive.FirstOrDefault();
		// Жизнь: +30% за T3 и +40% Закалённых — оба Increased, складываются.
		Check(modded != null && Near(modded.Stats.Get(StatType.MaxLife), modded.Data.MaxLife * 1.7f) && Near(modded.Stats.Get(StatType.FireResistance), 0.5f),
			$"моды карты на врагах: жизнь {modded?.Stats.Get(StatType.MaxLife)} (×1.7), сопр. огню {modded?.Stats.Get(StatType.FireResistance)}");
		var bag = arena.Bag;
		for (int i = 0; i < bag.Capacity; i++) bag.TryAdd(gs.Generator.Generate(1));
		Check(bag.IsFull && !bag.TryAdd(gs.Generator.Generate(1)), $"сумка на {bag.Capacity} ячеек заполнена");
		var p = arena.Player.GlobalPosition;
		var item = LootDrop.SpawnItem(arena, p + new Vector3(0.3f, 0, 0), gs.Generator.Generate(3, Rarity.Magic));
		var shard = gs.Items.Shards.First();
		var coin = LootDrop.SpawnCurrency(arena, p + new Vector3(-0.3f, 0, 0), shard);
		var mapDrop = LootDrop.SpawnMap(arena, p + new Vector3(0, 0, 0.3f), gs.Generator.GenerateMap(gs.Atlas.Get("cold_3")));
		arena.Drops.Add(item);
		arena.Drops.Add(coin);
		arena.Drops.Add(mapDrop);
		await Frames(gs, 3);
		Check(GodotObject.IsInstanceValid(item) && arena.Drops.Contains(item) && arena.Drops.Contains(mapDrop), "полная сумка: предмет и карта остались на земле");
		Check(!arena.Drops.Contains(coin) && bag.Currency.GetValueOrDefault(shard.Id) == 1, "полная сумка: осколок подобран");
		var hud = FindOfType<Hud>(arena);
		Check(hud.BagCounterText.StartsWith($"Сумка {bag.Capacity}/{bag.Capacity}"), $"HUD: «{hud.BagCounterText}»");

		// Подбор касанием в пустой сумке: карта тоже занимает ячейку.
		bag.Items.Clear();
		await Frames(gs, 3);
		Check(!arena.Drops.Contains(item) && bag.Items.Contains(item.Item) && bag.Maps.Contains(mapDrop.Map) && bag.Used == 2, "подбор касанием: предмет и карта в сумке");

		// Подбор кликом: рядом с лутом и в пределах досягаемости.
		var far = LootDrop.SpawnItem(arena, p + new Vector3(3f, 0, 0), gs.Generator.Generate(1));
		var tooFar = LootDrop.SpawnItem(arena, p + new Vector3(9f, 0, 0), gs.Generator.Generate(1));
		arena.Drops.Add(far);
		arena.Drops.Add(tooFar);
		Check(!arena.TryClickPickupAt(p + new Vector3(6f, 0, 6f)), "клик мимо лута не перехватывается (меч бьёт)");
		Check(arena.TryClickPickupAt(p + new Vector3(3.4f, 0, 0.3f)) && bag.Items.Contains(far.Item), "клик по луту в 3 м: подобран");
		Check(arena.TryClickPickupAt(p + new Vector3(9f, 0, 0)) && !bag.Items.Contains(tooFar.Item) && arena.Drops.Contains(tooFar), "клик по луту в 9 м: далеко, не подобран");

		// Победа при полном тайнике: не поместившееся перечислено как потерянное.
		for (int i = 0; gs.Profile.FreeCells > 0 && i < 100; i++) gs.Profile.AddToStash(gs.Generator.Generate(1));
		arena.Spawner.ForceElite();
		for (int i = 0; i < 120 && arena.Spawner.Elite == null; i++) await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.PhysicsFrame);
		var bagItems = bag.Items.ToList();
		arena.Spawner.Elite?.Stats.TakeDamage(999999);
		await Frames(gs, 3);
		var res = FindOfType<ResultScreen>(arena);
		Check(res.IsShown && res.BodyText.Contains("Не поместилось") && bagItems.All(i => !gs.Profile.Stash.Contains(i) && res.BodyText.Contains(i.Name)),
			"полный тайник: предметы сумки не потеряны молча, а перечислены как не поместившиеся");
		for (int i = 0; i < gs.Profile.Stash.Length; i++) gs.Profile.Stash[i] = null;
		gs.Profile.Maps.Clear();
		await Leave(gs);
	}

	private static T FindOfType<T>(Node root) where T : Node
	{
		if (root is T t) return t;
		foreach (var child in root.GetChildren())
			if (FindOfType<T>(child) is { } found) return found;
		return null;
	}

	/// <summary>Скриншоты атласа, инвентаря и боя + замер FPS с виджетами: godot --path . -- --shots</summary>
	public static async Task<int> Shots(GameState gs, string dir)
	{
		var rawSave = SaveSystem.Instance.ReadRaw();
		SaveSystem.Instance.WriteRaw(null);
		gs.LoadGame();

		async Task Wait(int n)
		{
			for (int i = 0; i < n; i++) await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
		}
		void Shot(string name) => gs.GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");

		// Демо-имущество.
		gs.Completed.UnionWith(new[] { "fire_1", "fire_2", "lightning_1" });
		for (int i = 0; i < 17; i++) gs.Profile.AddToStash(gs.Generator.Generate(1 + i % 5));
		foreach (var c in gs.Items.Currency) gs.Profile.AddCurrency(c.Id, 1 + (c.SortOrder * 7) % 5);
		foreach (var id in new[] { "fire_2", "fire_3", "lightning_2", "cold_1", "fire_3" })
			gs.Profile.Maps.Add(gs.Generator.GenerateMap(gs.Atlas.Get(id), Rarity.Rare));
		gs.Profile.Equip(0);
		gs.SaveGame();

		await Leave(gs);
		await Wait(30);
		Shot("atlas");
		var atlas = gs.GetTree().CurrentScene as AtlasScreen;
		if (FindOfType<NodePanel>(atlas) is { } panel)
		{
			panel.ShowNode(gs.Atlas.Get("fire_1"));
			await Wait(10);
			Shot("node_panel");
			panel.ShowDevice(gs.Profile.Maps.FirstOrDefault());
			await Wait(10);
			Shot("map_device");
			panel.Hide();
		}
		if (atlas != null)
		{
			atlas.Inventory.Open();
			await Wait(10);
			Shot("inventory");
			var rare = atlas.Inventory.Cells.FirstOrDefault(c => c.Item?.Rarity == Rarity.Rare)?.Item ?? gs.Profile.Stash.First(i => i != null);
			atlas.Inventory.OpenCraft(rare);
			var firstRoll = rare.Affixes.FirstOrDefault();
			if (firstRoll != null) atlas.Inventory.Craft.SelectShard(gs.Items.ShardFor(firstRoll.Affix));
			await Wait(5);
			Shot("craft");
			atlas.Inventory.Craft.Forge();
			await Wait(5);
			Shot("craft_result");
			atlas.Inventory.Craft.Close();
			atlas.Inventory.ShowTab(InventoryScreen.TabResources);
			await Wait(5);
			Shot("inventory_resources");
			atlas.Inventory.ShowTab(InventoryScreen.TabMaps);
			await Wait(5);
			Shot("inventory_maps");
			atlas.Inventory.EnterMassMode();
			atlas.Inventory.ToggleSelected(System.Array.FindIndex(gs.Profile.Stash, i => i != null));
			await Wait(5);
			Shot("inventory_mass");
			atlas.Inventory.ExitMassMode();
			atlas.Inventory.ShowTab(InventoryScreen.TabItems);
			await Wait(5);
			// Тултип со сравнением: наводим мышь на редкий предмет.
			var cell = atlas.Inventory.Cells.FirstOrDefault(c => c.Item?.Rarity == Rarity.Rare) ?? atlas.Inventory.Cells.First(c => c.Item != null);
			var target = cell.GetGlobalRect().GetCenter();
			Input.WarpMouse(target + new Vector2(-3, 0));
			await Wait(3);
			Input.WarpMouse(target);
			gs.GetViewport().PushInput(new InputEventMouseMotion { Position = target, GlobalPosition = target });
			await Wait(70);
			// Автоматический прогон не всегда вызывает всплывашку — рисуем тот же контрол тултипа рядом с ячейкой.
			if (cell._MakeCustomTooltip(" ") is Control tipContent)
			{
				var tip = new PanelContainer { ThemeTypeVariation = "TooltipPanel", Position = target + new Vector2(20, 10) };
				tip.AddChild(tipContent);
				atlas.Inventory.AddChild(tip);
				await Wait(5);
				GD.Print($"TOOLTIP size={tip.Size} text_height={((RichTextLabel)tipContent).GetContentHeight()}");
				Shot("tooltip");
				tip.QueueFree();
			}
			atlas.Inventory.Close();
		}

		var arena = await Enter(gs, "fire_3");
		if (arena == null) return 1;
		arena.Player.Stats.AddTag(Tags.Invulnerable, "stress");
		await Wait(60 * 3);
		// Статусы на ближних врагах + лут на земле рядом.
		var p = arena.Player.GlobalPosition;
		int k = 0;
		foreach (var e in EnemyBase.Alive.ToList())
		{
			e.GlobalPosition = p + new Vector3(Mathf.Cos(k) * (3 + k % 3), 0, Mathf.Sin(k) * (3 + k % 3));
			DamageSystem.Apply(DamageSystem.BuildRaw(arena.Player.Stats, arena.Player, DamageType.Physical, 3, false), e);
			if (k % 4 == 0) e.Status.Apply(gs.Config.Ignite, arena.Player.Stats, 2f);
			if (k % 4 == 1) { DamageSystem.ApplyChill(e, arena.Player.Stats); DamageSystem.ApplyChill(e, arena.Player.Stats); }
			if (k % 4 == 2) e.Status.Apply(gs.Config.Shock, arena.Player.Stats);
			if (k % 4 == 3) e.Status.Apply(gs.Config.Freeze, arena.Player.Stats);
			k++;
		}
		arena.Drops.Add(LootDrop.SpawnItem(arena, p + new Vector3(-4, 0, 3), gs.Generator.Generate(3, Rarity.Rare)));
		arena.Drops.Add(LootDrop.SpawnItem(arena, p + new Vector3(-5, 0, 1), gs.Generator.Generate(3, Rarity.Magic)));
		arena.Drops.Add(LootDrop.SpawnCurrency(arena, p + new Vector3(-3, 0, 5), gs.Items.GlyphsAndRunes.First()));
		arena.Drops.Add(LootDrop.SpawnMap(arena, p + new Vector3(-2, 0, 6), gs.Generator.GenerateMap(gs.Atlas.Get("fire_4"), Rarity.Magic)));
		await Wait(20);
		Shot("arena");

		// Стресс: +60 врагов, у всех виджеты и статусы, замер FPS за 5 секунд.
		arena.Spawner.StressSpawn(gs.Config.StressSpawnCount);
		await Wait(60);
		void Afflict()
		{
			int j = 0;
			foreach (var e in EnemyBase.Alive)
			{
				if (!e.WasHit) DamageSystem.Apply(DamageSystem.BuildRaw(arena.Player.Stats, arena.Player, DamageType.Physical, 1, false), e);
				e.Status.Apply(j % 2 == 0 ? gs.Config.Ignite : gs.Config.Shock, arena.Player.Stats, 1f);
				if (j % 3 == 0) DamageSystem.ApplyChill(e, arena.Player.Stats);
				j++;
			}
		}
		Afflict();
		ulong start = Time.GetTicksUsec();
		int frames = 0;
		double worst = 0;
		ulong last = start;
		while (Time.GetTicksUsec() - start < 5_000_000)
		{
			await gs.ToSignal(gs.GetTree(), SceneTree.SignalName.ProcessFrame);
			ulong now = Time.GetTicksUsec();
			double ms = (now - last) / 1000.0;
			worst = System.Math.Max(worst, ms);
			if (ms > 30) GD.Print($"STRESS spike frame={frames} ms={ms:0.0}");
			last = now;
			frames++;
			if (frames % 60 == 0) Afflict();
			if (frames % 120 == 0)
				GD.Print($"STRESS perf: process={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0}ms physics={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0}ms " +
					$"draws={Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame)} objects={Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)} " +
					$"prims={Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame)} nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}");
		}
		double avg = frames / ((Time.GetTicksUsec() - start) / 1_000_000.0);
		int visible = arena.Widgets.ActiveCount;
		GD.Print($"STRESS: enemies={EnemyBase.Alive.Count} widgets={visible} avgFps={avg:0.0} worstFrameMs={worst:0.0}");
		Shot("stress");

		SaveSystem.Instance.WriteRaw(rawSave);
		gs.LoadGame();
		return 0;
	}
}
