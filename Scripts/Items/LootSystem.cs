using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Enemies;

namespace EpicLoot.Items;

/// <summary>Контекст выпадения: тир карты, бонусы количества и редкости, узлы, чьи карты могут выпасть.</summary>
public class LootContext
{
	public int MapTier = 1;
	public float Quantity;
	public float RarityBonus;
	public List<AtlasNodeData> MapNodes = new();
}

/// <summary>Гарантированный лут элиты/босса.</summary>
public class LootBundle
{
	public readonly List<ItemInstance> Items = new();
	public readonly List<MapInstance> Maps = new();
	public readonly Dictionary<string, int> Currency = new();
}

/// <summary>Выпадение лута с врагов по шансам из EnemyData, с учётом модов карты.</summary>
public class LootSystem
{
	private readonly ItemDatabase _db;
	private readonly ItemGenerator _gen;
	private readonly LootContext _ctx;

	public LootSystem(ItemDatabase db, ItemGenerator gen, LootContext ctx)
	{
		_db = db;
		_gen = gen;
		_ctx = ctx;
	}

	public LootContext Context => _ctx;

	private bool Roll(float chance) => chance > 0f && _gen.Randf() < chance * (1f + _ctx.Quantity);

	/// <summary>
	/// Узлы, чьи карты могут выпасть: пройденные (старт считается пройденным) и соседние с ними, тир N или N+1.
	/// Если таких нет — карта не падает.
	/// </summary>
	public static List<AtlasNodeData> EligibleMapNodes(AtlasGraph atlas, ISet<string> completed, int mapTier)
	{
		var list = new List<AtlasNodeData>();
		foreach (var node in atlas.Nodes.Values)
		{
			if (node.Type == AtlasNodeType.Start) continue;
			if (node.Tier != mapTier && node.Tier != mapTier + 1) continue;
			if (atlas.StateOf(node.Id, completed) != AtlasNodeState.Locked) list.Add(node);
		}
		return list.OrderBy(n => n.Id).ToList();
	}

	public MapInstance RollMap()
	{
		if (_ctx.MapNodes.Count == 0) return null;
		var node = _ctx.MapNodes[_gen.RandiRange(0, _ctx.MapNodes.Count - 1)];
		return _gen.GenerateMap(node, null, _ctx.RarityBonus);
	}

	public ItemInstance RollItem(Rarity? rarity = null) => _gen.Generate(_ctx.MapTier, rarity, null, _ctx.RarityBonus);

	/// <summary>Обычный враг: предмет, осколок, глиф/руна, карта — на землю.</summary>
	public List<LootDrop> RollGround(EnemyData data, Node parent, Vector3 at)
	{
		var drops = new List<LootDrop>();
		if (Roll(data.ItemDropChance))
			drops.Add(LootDrop.SpawnItem(parent, at + Jitter(), RollItem()));
		if (Roll(data.ShardDropChance) && Crafting.RandomShard(_gen, _db) is { } shard)
			drops.Add(LootDrop.SpawnCurrency(parent, at + Jitter(), shard));
		if (Roll(data.GlyphRuneDropChance) && Crafting.RollGlyphOrRune(_gen, _db) is { } gr)
			drops.Add(LootDrop.SpawnCurrency(parent, at + Jitter(), gr));
		if (Roll(data.MapDropChance) && RollMap() is { } map)
			drops.Add(LootDrop.SpawnMap(parent, at + Jitter(), map));
		return drops;
	}

	/// <summary>Гарантированный лут: первый предмет не ниже minRarity; глиф/руна — целые штуки плюс шанс дробной части.</summary>
	public LootBundle RollGuaranteed(int items, Rarity minRarity, int shards, float glyphRunes, int maps)
	{
		var b = new LootBundle();
		for (int i = 0; i < items; i++)
			b.Items.Add(RollItem(i == 0 ? _gen.RollRarity(minRarity, _ctx.RarityBonus) : null));
		for (int i = 0; i < shards; i++)
			if (Crafting.RandomShard(_gen, _db) is { } s) b.Currency[s.Id] = b.Currency.GetValueOrDefault(s.Id) + 1;
		int whole = Mathf.FloorToInt(glyphRunes);
		int count = whole + (_gen.Randf() < glyphRunes - whole ? 1 : 0);
		for (int i = 0; i < count; i++)
			if (Crafting.RollGlyphOrRune(_gen, _db) is { } c) b.Currency[c.Id] = b.Currency.GetValueOrDefault(c.Id) + 1;
		for (int i = 0; i < maps; i++)
			if (RollMap() is { } m) b.Maps.Add(m);
		return b;
	}

	private Vector3 Jitter() => new(_gen.Rng.RandfRange(-0.6f, 0.6f), 0, _gen.Rng.RandfRange(-0.6f, 0.6f));
}
