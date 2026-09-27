using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Items;

// Порядок значений важен: .tres и сейв хранят enum числом.
public enum ItemSlot
{
	Weapon,
	Helmet,
	Chest,
	Belt,
	Boots,
	Ring,
	Amulet,
}

public enum Rarity
{
	Normal,
	Magic,
	Rare,
	/// <summary>Любой аффикс T5 (только крафтом).</summary>
	Exalted,
}

/// <summary>База предмета: слот, базовое свойство и его диапазон, часть модели.</summary>
[GlobalClass]
public partial class ItemBaseData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string ShortName { get; set; } = "";
	[Export] public ItemSlot Slot { get; set; }
	/// <summary>None — без базового свойства (кольцо).</summary>
	[Export] public StatType BaseStat { get; set; } = StatType.None;
	[Export] public ModifierType BaseType { get; set; }
	[Export] public float BaseMin { get; set; }
	[Export] public float BaseMax { get; set; }
	/// <summary>Шаблон текста, {0} — значение (проценты уже умножены на 100).</summary>
	[Export] public string BaseText { get; set; } = "";
	[Export] public bool BaseIsPercent { get; set; }
	/// <summary>Имя части модели (SK_Helmet, Sword…), пусто — не видно.</summary>
	[Export] public string ModelPart { get; set; } = "";
}
