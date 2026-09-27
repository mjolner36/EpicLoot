using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using Dictionary = Godot.Collections.Dictionary;
using GArray = Godot.Collections.Array;

namespace EpicLoot.Items;

/// <summary>Постоянное имущество игрока: тайник (сетка), надетая экипировка, валюта крафта, карты.</summary>
public class Inventory
{
	public event Action Changed;

	public readonly ItemInstance[] Stash;
	public readonly System.Collections.Generic.Dictionary<ItemSlot, ItemInstance> Equipped = new();
	public readonly System.Collections.Generic.Dictionary<string, int> Currency = new();
	public readonly List<MapInstance> Maps = new();

	public Inventory(int stashSize)
	{
		Stash = new ItemInstance[stashSize];
	}

	public void NotifyChanged() => Changed?.Invoke();

	public int FreeCells => Stash.Count(s => s == null);

	public int FirstFree() => Array.IndexOf(Stash, null);

	public bool AddToStash(ItemInstance item)
	{
		int i = FirstFree();
		if (i < 0) return false;
		Stash[i] = item;
		NotifyChanged();
		return true;
	}

	public int CurrencyCount(string id) => Currency.GetValueOrDefault(id, 0);
	public int CurrencyCount(CraftCurrencyData c) => c == null ? 0 : CurrencyCount(c.Id);
	public int CurrencyTotal => Currency.Values.Sum();

	public void AddCurrency(string id, int count)
	{
		if (count == 0) return;
		int n = Math.Max(0, CurrencyCount(id) + count);
		if (n == 0) Currency.Remove(id);
		else Currency[id] = n;
		NotifyChanged();
	}

	public void AddCurrency(IEnumerable<KeyValuePair<string, int>> gains)
	{
		foreach (var (id, n) in gains) AddCurrency(id, n);
	}

	public int MapCount(AtlasNodeData node) => Maps.Count(m => m.Node == node);

	/// <summary>Перемещение/обмен между ячейками тайника.</summary>
	public void Move(int from, int to)
	{
		if (from == to || !Valid(from) || !Valid(to)) return;
		(Stash[from], Stash[to]) = (Stash[to], Stash[from]);
		NotifyChanged();
	}

	/// <summary>Надевает предмет из ячейки; надетый ранее встаёт на его место.</summary>
	public bool Equip(int stashIndex)
	{
		if (!Valid(stashIndex) || Stash[stashIndex] == null) return false;
		var item = Stash[stashIndex];
		Equipped.TryGetValue(item.Slot, out var old);
		Equipped[item.Slot] = item;
		Stash[stashIndex] = old;
		NotifyChanged();
		return true;
	}

	/// <summary>Снимает предмет в указанную ячейку (или первую свободную). Если ячейка занята предметом того же слота — обмен.</summary>
	public bool Unequip(ItemSlot slot, int targetIndex = -1)
	{
		if (!Equipped.TryGetValue(slot, out var item)) return false;
		if (targetIndex < 0) targetIndex = FirstFree();
		if (!Valid(targetIndex)) return false;
		var there = Stash[targetIndex];
		if (there != null)
		{
			if (there.Slot != slot) return false;
			Equipped[slot] = there;
		}
		else
		{
			Equipped.Remove(slot);
		}
		Stash[targetIndex] = item;
		NotifyChanged();
		return true;
	}

	/// <summary>Убирает предмет из ячейки (после разборки).</summary>
	public ItemInstance Take(int index)
	{
		if (!Valid(index)) return null;
		var item = Stash[index];
		Stash[index] = null;
		NotifyChanged();
		return item;
	}

	public void Sort()
	{
		var items = Stash.Where(s => s != null)
			.OrderBy(s => s.Slot).ThenByDescending(s => s.Rarity).ThenByDescending(s => s.Level).ToList();
		Array.Clear(Stash);
		for (int i = 0; i < items.Count; i++) Stash[i] = items[i];
		NotifyChanged();
	}

	public IEnumerable<ItemInstance> EquippedItems => Equipped.Values;

	private bool Valid(int i) => i >= 0 && i < Stash.Length;

	public Dictionary ToDict()
	{
		var stash = new Dictionary();
		for (int i = 0; i < Stash.Length; i++)
			if (Stash[i] != null) stash[i.ToString()] = Stash[i].ToDict();
		var equipped = new Dictionary();
		foreach (var (slot, item) in Equipped) equipped[((int)slot).ToString()] = item.ToDict();
		var currency = new Dictionary();
		foreach (var (id, n) in Currency) if (n > 0) currency[id] = n;
		var maps = new GArray();
		foreach (var m in Maps) maps.Add(m.ToDict());
		return new Dictionary { ["stash"] = stash, ["equipped"] = equipped, ["currency"] = currency, ["maps"] = maps };
	}

	public void LoadFrom(Dictionary d, ItemDatabase db, AtlasGraph atlas)
	{
		Array.Clear(Stash);
		Equipped.Clear();
		Currency.Clear();
		Maps.Clear();
		if (d.TryGetValue("stash", out var stash))
			foreach (var (k, v) in stash.AsGodotDictionary())
			{
				int i = k.AsString().ToInt();
				var item = ItemInstance.FromDict(v.AsGodotDictionary(), db);
				if (item != null && Valid(i)) Stash[i] = item;
			}
		if (d.TryGetValue("equipped", out var eq))
			foreach (var (_, v) in eq.AsGodotDictionary())
			{
				var item = ItemInstance.FromDict(v.AsGodotDictionary(), db);
				if (item != null) Equipped[item.Slot] = item;
			}
		if (d.TryGetValue("currency", out var currency))
			foreach (var (k, v) in currency.AsGodotDictionary())
			{
				if (db.Currencies(k.AsString()) == null)
				{
					GD.PushWarning($"Сейв: неизвестная валюта {k}, пропущена");
					continue;
				}
				Currency[k.AsString()] = v.AsInt32();
			}
		if (d.TryGetValue("maps", out var maps))
			foreach (var v in maps.AsGodotArray())
			{
				var map = MapInstance.FromDict(v.AsGodotDictionary(), atlas, db);
				if (map != null) Maps.Add(map);
			}
		NotifyChanged();
	}
}

/// <summary>Сумка забега: предметы и карты занимают ячейки, валюта — нет. Переходит в тайник только при победе.</summary>
public class RunBag
{
	public readonly int Capacity;
	public readonly List<ItemInstance> Items = new();
	public readonly List<MapInstance> Maps = new();
	public readonly System.Collections.Generic.Dictionary<string, int> Currency = new();
	public event Action Changed;

	public RunBag(int capacity) => Capacity = capacity;

	public int Used => Items.Count + Maps.Count;
	public bool IsFull => Used >= Capacity;
	public int CurrencyTotal => Currency.Values.Sum();

	public bool TryAdd(ItemInstance item)
	{
		if (IsFull) return false;
		Items.Add(item);
		Changed?.Invoke();
		return true;
	}

	public bool TryAdd(MapInstance map)
	{
		if (IsFull) return false;
		Maps.Add(map);
		Changed?.Invoke();
		return true;
	}

	public void AddCurrency(string id, int count = 1)
	{
		Currency[id] = Currency.GetValueOrDefault(id, 0) + count;
		Changed?.Invoke();
	}
}
