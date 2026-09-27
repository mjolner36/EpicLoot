using Godot;

namespace EpicLoot.Items;

public enum CurrencyKind
{
	Shard,
	Glyph,
	Rune,
}

public enum CurrencyAction
{
	/// <summary>Осколок: добавляет свой аффикс на T1 или повышает его тир.</summary>
	Shard,
	/// <summary>Глиф надежды: с осколком, шанс не потратить ПК.</summary>
	GlyphHope,
	/// <summary>Глиф хаоса: с осколком повышает тир, но меняет аффикс; на карте — с Руной перековки меняет мод.</summary>
	GlyphChaos,
	/// <summary>Руна удаления: убирает случайный аффикс (возвращает осколок) или мод карты.</summary>
	RuneRemoval,
	/// <summary>Руна перековки: перебрасывает значения аффиксов в пределах тиров.</summary>
	RuneRefinement,
	/// <summary>Руна открытия: 2-4 аффикса на пустой предмет или 2-4 мода на пустую карту.</summary>
	RuneDiscovery,
}

/// <summary>Валюта крафта: осколок аффикса, глиф или руна.</summary>
[GlobalClass]
public partial class CraftCurrencyData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
	[Export] public CurrencyKind Kind { get; set; }
	[Export] public CurrencyAction Action { get; set; }
	/// <summary>Аффикс осколка (только для Kind = Shard).</summary>
	[Export] public AffixData Affix { get; set; }
	[Export] public Texture2D Icon { get; set; }
	/// <summary>Вес в пуле глифов и рун при выпадении.</summary>
	[Export] public float DropWeight { get; set; } = 1f;
	[Export] public int SortOrder { get; set; }
}
