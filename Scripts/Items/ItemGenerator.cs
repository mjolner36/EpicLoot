using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;

namespace EpicLoot.Items;

/// <summary>Генерация предметов, аффиксов и карт. Уровень предмета = тир карты.</summary>
public class ItemGenerator
{
	private readonly ItemDatabase _db;
	private readonly RandomNumberGenerator _rng;

	public ItemGenerator(ItemDatabase db, RandomNumberGenerator rng = null)
	{
		_db = db;
		_rng = rng ?? new RandomNumberGenerator();
		if (rng == null) _rng.Randomize();
	}

	public LootConfig Cfg => _db.Config;
	public RandomNumberGenerator Rng => _rng;

	/// <summary>Бросок редкости выпадения (Normal/Magic/Rare). Бонус редкости умножает веса Magic и Rare.</summary>
	public Rarity RollRarity(Rarity min = Rarity.Normal, float rarityBonus = 0f)
	{
		var w = Cfg.RarityWeights;
		var weights = new float[w.Length];
		for (int i = 0; i < w.Length; i++)
			weights[i] = i < (int)min ? 0f : w[i] * (i == 0 ? 1f : 1f + rarityBonus);
		float r = _rng.Randf() * weights.Sum();
		for (int i = 0; i < weights.Length; i++)
		{
			r -= weights[i];
			if (r <= 0f && weights[i] > 0f) return (Rarity)i;
		}
		return (Rarity)(weights.Length - 1);
	}

	public (int min, int max) AffixRange(Rarity r) => r switch
	{
		Rarity.Magic => (Cfg.MagicAffixMin, Cfg.MagicAffixMax),
		Rarity.Rare or Rarity.Exalted => (Cfg.RareAffixMin, Cfg.RareAffixMax),
		_ => (0, 0),
	};

	public int MaxAffixes => Cfg.MaxPrefixes + Cfg.MaxSuffixes;

	/// <summary>Максимальный тир выпавшего аффикса для тира карты.</summary>
	public int MaxDropTier(int mapTier)
	{
		var t = Cfg.MaxAffixTierByMapTier;
		return t[Mathf.Clamp(mapTier, 1, t.Length) - 1];
	}

	public int RollPotential(Rarity dropRarity)
	{
		var r = dropRarity switch
		{
			Rarity.Magic => Cfg.MagicPotential,
			Rarity.Rare or Rarity.Exalted => Cfg.RarePotential,
			_ => Cfg.NormalPotential,
		};
		return _rng.RandiRange(r.X, r.Y);
	}

	/// <summary>Предмет с карты тира mapTier. rarity — редкость выпадения (задаёт число аффиксов и ПК).</summary>
	public ItemInstance Generate(int mapTier, Rarity? rarity = null, ItemBaseData baseData = null, float rarityBonus = 0f)
	{
		baseData ??= _db.Bases[_rng.RandiRange(0, _db.Bases.Count - 1)];
		var dropRarity = rarity ?? RollRarity(Rarity.Normal, rarityBonus);
		var item = new ItemInstance { Base = baseData, Level = Mathf.Max(1, mapTier) };
		item.BaseValue = RollValue(baseData.BaseMin, baseData.BaseMax, !baseData.BaseIsPercent);
		item.Potential = RollPotential(dropRarity);
		var (min, max) = AffixRange(dropRarity);
		int count = _rng.RandiRange(min, max);
		int maxTier = MaxDropTier(item.Level);
		for (int i = 0; i < count; i++)
		{
			var a = RandomCandidate(item, null);
			if (a == null) break;
			AddAffix(item, a, _rng.RandiRange(1, maxTier));
		}
		return item;
	}

	/// <summary>Есть ли свободный слот под префикс/суффикс этого аффикса.</summary>
	public bool HasFreeSlot(ItemInstance item, AffixData a) =>
		a.IsPrefix ? item.Prefixes < Cfg.MaxPrefixes : item.Suffixes < Cfg.MaxSuffixes;

	/// <summary>Аффиксы, которые можно добавить: разрешены для слота, ещё не на предмете, есть место. prefix = null — любые.</summary>
	public List<AffixData> Candidates(ItemInstance item, bool? prefix) =>
		_db.Affixes.Where(a => a.Allows(item.Slot) && !item.HasAffix(a) && HasFreeSlot(item, a) && (prefix == null || a.IsPrefix == prefix)).ToList();

	public AffixData RandomCandidate(ItemInstance item, bool? prefix)
	{
		var c = Candidates(item, prefix);
		return c.Count == 0 ? null : c[_rng.RandiRange(0, c.Count - 1)];
	}

	public AffixRoll AddAffix(ItemInstance item, AffixData affix, int tier)
	{
		var roll = new AffixRoll { Affix = affix, Tier = tier, Value = RollTier(affix, tier) };
		item.Affixes.Add(roll);
		return roll;
	}

	public float RollTier(AffixData a, int tier) =>
		RollValue(a.TierMin[tier - 1], a.TierMax[tier - 1], a.Integer);

	private float RollValue(float min, float max, bool integer)
	{
		if (min > max) (min, max) = (max, min);
		if (integer) return _rng.RandiRange(Mathf.RoundToInt(min), Mathf.RoundToInt(max));
		return Mathf.Snapped(_rng.RandfRange(min, max), 0.01f);
	}

	// ---------- Карты ----------

	public (int min, int max) MapModRange(Rarity r) => r switch
	{
		Rarity.Magic => (Cfg.MapMagicModsMin, Cfg.MapMagicModsMax),
		Rarity.Rare => (Cfg.MapRareModsMin, Cfg.MapRareModsMax),
		_ => (0, 0),
	};

	public MapInstance GenerateMap(AtlasNodeData node, Rarity? rarity = null, float rarityBonus = 0f)
	{
		var map = new MapInstance { Node = node, Tier = node.Tier };
		var r = rarity ?? RollRarity(Rarity.Normal, rarityBonus);
		var (min, max) = MapModRange(r);
		AddMapMods(map, _rng.RandiRange(min, max));
		return map;
	}

	/// <summary>Добавляет n случайных модов (без повторов, по весам).</summary>
	public int AddMapMods(MapInstance map, int n)
	{
		int added = 0;
		for (int i = 0; i < n; i++)
		{
			var mod = RandomMapMod(map, null);
			if (mod == null) break;
			map.Mods.Add(mod);
			added++;
		}
		return added;
	}

	public MapModData RandomMapMod(MapInstance map, MapModData exclude)
	{
		var pool = _db.MapMods.Where(m => !map.Mods.Contains(m) && m != exclude).ToList();
		if (pool.Count == 0) return null;
		float r = _rng.Randf() * pool.Sum(m => m.DropWeight);
		foreach (var m in pool)
		{
			r -= m.DropWeight;
			if (r <= 0f) return m;
		}
		return pool[^1];
	}

	public int RandiRange(int a, int b) => _rng.RandiRange(a, b);
	public float Randf() => _rng.Randf();
}
