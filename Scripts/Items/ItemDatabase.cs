using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;

namespace EpicLoot.Items;

/// <summary>Реестр баз, аффиксов, валюты крафта и модов карт, загруженных из папок Data.</summary>
public class ItemDatabase
{
	public readonly LootConfig Config;
	public readonly List<ItemBaseData> Bases;
	public readonly List<AffixData> Affixes;
	public readonly List<CraftCurrencyData> Currency;
	public readonly List<MapModData> MapMods;

	public ItemDatabase(LootConfig config)
	{
		Config = config;
		Bases = LoadDir<ItemBaseData>(config.BasesDir).OrderBy(b => b.Slot).ToList();
		Affixes = LoadDir<AffixData>(config.AffixesDir).OrderBy(a => !a.IsPrefix).ThenBy(a => a.Id).ToList();
		Currency = LoadDir<CraftCurrencyData>(config.CurrencyDir).OrderBy(c => c.Kind).ThenBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
		MapMods = LoadDir<MapModData>(config.MapModsDir).OrderBy(m => m.Id).ToList();
	}

	public ItemBaseData Base(string id) => Bases.FirstOrDefault(b => b.Id == id);
	public AffixData Affix(string id) => Affixes.FirstOrDefault(a => a.Id == id);
	public CraftCurrencyData Currencies(string id) => Currency.FirstOrDefault(c => c.Id == id);
	public MapModData MapMod(string id) => MapMods.FirstOrDefault(m => m.Id == id);

	public CraftCurrencyData ShardFor(AffixData affix) =>
		Currency.FirstOrDefault(c => c.Kind == CurrencyKind.Shard && c.Affix == affix);

	public CraftCurrencyData ByAction(CurrencyAction action) =>
		Currency.FirstOrDefault(c => c.Action == action && c.Kind != CurrencyKind.Shard);

	public IEnumerable<CraftCurrencyData> GlyphsAndRunes => Currency.Where(c => c.Kind != CurrencyKind.Shard);
	public IEnumerable<CraftCurrencyData> Shards => Currency.Where(c => c.Kind == CurrencyKind.Shard);

	public static List<T> LoadDir<T>(string dir) where T : Resource
	{
		var list = new List<T>();
		foreach (var file in DirAccess.GetFilesAt(dir))
		{
			var name = file.EndsWith(".remap") ? file[..^6] : file;
			if (!name.EndsWith(".tres") && !name.EndsWith(".res")) continue;
			var res = ResourceLoader.Load<T>($"{dir}/{name}");
			if (res != null) list.Add(res);
			else GD.PushError($"Не удалось загрузить {dir}/{name}");
		}
		return list;
	}
}
