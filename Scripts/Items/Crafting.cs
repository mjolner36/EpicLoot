using System.Collections.Generic;
using System.Linq;

namespace EpicLoot.Items;

/// <summary>Результат ковки: успех, строка для игрока, потраченная валюта и ПК, возвращённые осколки.</summary>
public class ForgeResult
{
	public bool Ok;
	public string Message = "";
	public int PotentialSpent;
	public bool HopeSaved;
	public readonly List<CraftCurrencyData> Consumed = new();
	public readonly Dictionary<string, int> Returned = new();

	public static ForgeResult Fail(string msg) => new() { Ok = false, Message = msg };
}

/// <summary>
/// Крафт в стиле Last Epoch. Один вход Forge(цель, осколок, глиф, руна) для предметов и карт.
/// Инвентарь не трогает: вызывающий проверяет наличие валюты и списывает Consumed только при Ok.
/// </summary>
public static class Crafting
{
	public static ForgeResult Forge(object target, CraftCurrencyData shard, CraftCurrencyData glyph, CraftCurrencyData rune, ItemGenerator gen, ItemDatabase db) =>
		target switch
		{
			ItemInstance item => ForgeItem(item, shard, glyph, rune, gen, db),
			MapInstance map => ForgeMap(map, shard, glyph, rune, gen),
			_ => ForgeResult.Fail("Нет цели"),
		};

	/// <summary>Возможный диапазон траты ПК для текущего выбора (для окна крафта).</summary>
	public static (int min, int max) CostRange(LootConfig cfg) => (cfg.ShardCostMin, cfg.ShardCostMax);

	// ---------- Предметы ----------

	private static ForgeResult ForgeItem(ItemInstance item, CraftCurrencyData shard, CraftCurrencyData glyph, CraftCurrencyData rune, ItemGenerator gen, ItemDatabase db)
	{
		var cfg = gen.Cfg;
		if (rune != null)
		{
			if (shard != null || glyph != null) return ForgeResult.Fail("Руна применяется отдельно от осколка и глифа");
			return ApplyRune(item, rune, gen, db);
		}
		if (shard == null) return ForgeResult.Fail(glyph != null ? "Глиф применяется вместе с осколком" : "Выберите осколок или руну");
		if (shard.Kind != CurrencyKind.Shard || shard.Affix == null) return ForgeResult.Fail("Это не осколок");
		if (glyph != null && glyph.Kind != CurrencyKind.Glyph) return ForgeResult.Fail("В слот глифа — только глиф");
		if (item.Potential <= 0) return ForgeResult.Fail("Потенциал ковки исчерпан");

		var affix = shard.Affix;
		if (!affix.Allows(item.Slot)) return ForgeResult.Fail($"«{affix.DisplayName}» не бывает на слоте «{item.Name}»");
		var roll = item.Roll(affix);
		var res = new ForgeResult { Ok = true };

		if (glyph?.Action == CurrencyAction.GlyphChaos)
		{
			// Повышает тир, но меняет аффикс на случайный другой того же типа и нового тира.
			if (roll == null) return ForgeResult.Fail("Глиф хаоса работает только при усилении имеющегося аффикса");
			if (roll.Tier >= cfg.MaxTier) return ForgeResult.Fail($"«{affix.DisplayName}» уже T{cfg.MaxTier}");
			var others = db.Affixes.Where(a => a != affix && a.IsPrefix == affix.IsPrefix && a.Allows(item.Slot) && !item.HasAffix(a)).ToList();
			if (others.Count == 0) return ForgeResult.Fail("Нет аффикса для замены");
			var replacement = others[gen.RandiRange(0, others.Count - 1)];
			int newTier = roll.Tier + 1;
			int oldTier = roll.Tier;
			roll.Affix = replacement;
			roll.Tier = newTier;
			roll.Value = gen.RollTier(replacement, newTier);
			res.Message = $"{affix.DisplayName} T{oldTier} → {replacement.DisplayName} T{newTier}";
		}
		else if (roll != null)
		{
			if (roll.Tier >= cfg.MaxTier) return ForgeResult.Fail($"«{affix.DisplayName}» уже T{cfg.MaxTier}");
			roll.Tier++;
			roll.Value = gen.RollTier(affix, roll.Tier);
			res.Message = $"{affix.DisplayName} T{roll.Tier - 1} → T{roll.Tier}";
		}
		else
		{
			if (!gen.HasFreeSlot(item, affix))
				return ForgeResult.Fail(affix.IsPrefix ? "Нет свободного слота префикса" : "Нет свободного слота суффикса");
			gen.AddAffix(item, affix, 1);
			res.Message = $"{affix.DisplayName}: добавлен T1";
		}

		res.Consumed.Add(shard);
		if (glyph != null) res.Consumed.Add(glyph);
		if (glyph?.Action == CurrencyAction.GlyphHope && gen.Randf() < cfg.HopeChance)
		{
			res.HopeSaved = true;
			res.Message += ", ПК не потрачен (Глиф надежды)";
		}
		else
		{
			int cost = gen.RandiRange(cfg.ShardCostMin, cfg.ShardCostMax);
			res.PotentialSpent = System.Math.Min(cost, item.Potential);
			item.Potential -= res.PotentialSpent;
			res.Message += $", потрачено {res.PotentialSpent} ПК";
		}
		return res;
	}

	private static ForgeResult ApplyRune(ItemInstance item, CraftCurrencyData rune, ItemGenerator gen, ItemDatabase db)
	{
		var cfg = gen.Cfg;
		var res = new ForgeResult { Ok = true };
		switch (rune.Action)
		{
			case CurrencyAction.RuneRemoval:
			{
				if (item.Affixes.Count == 0) return ForgeResult.Fail("У предмета нет аффиксов");
				var roll = item.Affixes[gen.RandiRange(0, item.Affixes.Count - 1)];
				item.Affixes.Remove(roll);
				var back = db.ShardFor(roll.Affix);
				if (back != null) res.Returned[back.Id] = 1;
				res.Message = $"{roll.Affix.DisplayName} T{roll.Tier} удалён, возвращён 1 осколок";
				break;
			}
			case CurrencyAction.RuneRefinement:
				if (item.Affixes.Count == 0) return ForgeResult.Fail("У предмета нет аффиксов");
				foreach (var roll in item.Affixes) roll.Value = gen.RollTier(roll.Affix, roll.Tier);
				res.Message = "Значения аффиксов переброшены";
				break;
			case CurrencyAction.RuneDiscovery:
			{
				if (item.Affixes.Count > 0) return ForgeResult.Fail("Руна открытия — только для предмета без аффиксов");
				int n = gen.RandiRange(cfg.DiscoveryMin, cfg.DiscoveryMax);
				for (int i = 0; i < n; i++)
				{
					var a = gen.RandomCandidate(item, null);
					if (a == null) break;
					gen.AddAffix(item, a, gen.RandiRange(1, cfg.DiscoveryMaxTier));
				}
				res.Message = $"Открыто аффиксов: {item.Affixes.Count}";
				break;
			}
			default:
				return ForgeResult.Fail("В слот руны — только руна");
		}
		res.Consumed.Add(rune);
		return res;
	}

	// ---------- Карты ----------

	private static ForgeResult ForgeMap(MapInstance map, CraftCurrencyData shard, CraftCurrencyData glyph, CraftCurrencyData rune, ItemGenerator gen)
	{
		var cfg = gen.Cfg;
		if (shard != null) return ForgeResult.Fail("Осколки на карты не действуют");
		if (glyph != null && glyph.Action != CurrencyAction.GlyphChaos) return ForgeResult.Fail("На картах работает только Глиф хаоса (с Руной перековки)");
		if (rune == null) return ForgeResult.Fail(glyph != null ? "Глиф хаоса на карте нужен вместе с Руной перековки" : "Выберите руну");
		var res = new ForgeResult { Ok = true };
		switch (rune.Action)
		{
			case CurrencyAction.RuneDiscovery:
				if (glyph != null) return ForgeResult.Fail("Глиф хаоса сочетается только с Руной перековки");
				if (map.Mods.Count > 0) return ForgeResult.Fail("Руна открытия — только для карты без модов");
				gen.AddMapMods(map, gen.RandiRange(cfg.DiscoveryMin, cfg.DiscoveryMax));
				res.Message = $"На карту добавлено модов: {map.Mods.Count}";
				break;
			case CurrencyAction.RuneRemoval:
			{
				if (glyph != null) return ForgeResult.Fail("Глиф хаоса сочетается только с Руной перековки");
				if (map.Mods.Count == 0) return ForgeResult.Fail("У карты нет модов");
				var mod = map.Mods[gen.RandiRange(0, map.Mods.Count - 1)];
				map.Mods.Remove(mod);
				res.Message = $"Мод «{mod.DisplayName}» удалён";
				break;
			}
			case CurrencyAction.RuneRefinement:
			{
				if (glyph == null) return ForgeResult.Fail("На карте Руна перековки работает только с Глифом хаоса");
				if (map.Mods.Count == 0) return ForgeResult.Fail("У карты нет модов");
				int i = gen.RandiRange(0, map.Mods.Count - 1);
				var old = map.Mods[i];
				var next = gen.RandomMapMod(map, old);
				if (next == null) return ForgeResult.Fail("Нет мода для замены");
				map.Mods[i] = next;
				res.Message = $"«{old.DisplayName}» → «{next.DisplayName}»";
				res.Consumed.Add(glyph);
				break;
			}
			default:
				return ForgeResult.Fail("В слот руны — только руна");
		}
		res.Consumed.Add(rune);
		return res;
	}

	// ---------- Разборка ----------

	/// <summary>
	/// Разборка: за каждый аффикс его осколки (по тиру), с шансом один осколок предмета теряется;
	/// шанс глифа или руны (у редких и возвышенных — несколько бросков).
	/// </summary>
	public static Dictionary<string, int> Disassemble(ItemInstance item, ItemGenerator gen, ItemDatabase db)
	{
		var cfg = gen.Cfg;
		var gains = new Dictionary<string, int>();
		foreach (var roll in item.Affixes)
		{
			var shard = db.ShardFor(roll.Affix);
			if (shard == null) continue;
			var byTier = cfg.DisassembleShardsByTier;
			int n = byTier[System.Math.Clamp(roll.Tier, 1, byTier.Length) - 1];
			gains[shard.Id] = gains.GetValueOrDefault(shard.Id) + n;
		}
		if (gains.Count > 0 && gen.Randf() < cfg.DisassembleLossChance)
		{
			var keys = gains.Keys.ToList();
			var lost = keys[gen.RandiRange(0, keys.Count - 1)];
			if (--gains[lost] <= 0) gains.Remove(lost);
		}
		int rolls = item.Rarity is Rarity.Rare or Rarity.Exalted ? cfg.DisassembleRareRolls : 1;
		for (int i = 0; i < rolls; i++)
		{
			if (gen.Randf() >= cfg.DisassembleGlyphRuneChance) continue;
			var c = RollGlyphOrRune(gen, db);
			if (c != null) gains[c.Id] = gains.GetValueOrDefault(c.Id) + 1;
		}
		return gains;
	}

	public static CraftCurrencyData RollGlyphOrRune(ItemGenerator gen, ItemDatabase db)
	{
		var pool = db.GlyphsAndRunes.ToList();
		if (pool.Count == 0) return null;
		float r = gen.Randf() * pool.Sum(c => c.DropWeight);
		foreach (var c in pool)
		{
			r -= c.DropWeight;
			if (r <= 0f) return c;
		}
		return pool[^1];
	}

	public static CraftCurrencyData RandomShard(ItemGenerator gen, ItemDatabase db)
	{
		var shards = db.Shards.ToList();
		return shards.Count == 0 ? null : shards[gen.RandiRange(0, shards.Count - 1)];
	}
}
