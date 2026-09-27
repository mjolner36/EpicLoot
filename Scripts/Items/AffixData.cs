using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Items;

/// <summary>
/// Аффикс: стат, тип модификатора, диапазоны значений по тирам (индекс 0 = T1, самый слабый; 4 = T5, самый сильный),
/// префикс или суффикс, разрешённые слоты.
/// </summary>
[GlobalClass]
public partial class AffixData : Resource
{
	[Export] public string Id { get; set; } = "";
	/// <summary>Короткое имя для списков и строки ковки ("Урон огнём").</summary>
	[Export] public string DisplayName { get; set; } = "";
	/// <summary>Шаблон текста, {0} — значение (проценты уже умножены на 100).</summary>
	[Export] public string Text { get; set; } = "";
	[Export] public StatType Stat { get; set; }
	[Export] public ModifierType Type { get; set; }
	[Export] public bool IsPrefix { get; set; }
	[Export] public float[] TierMin { get; set; } = System.Array.Empty<float>();
	[Export] public float[] TierMax { get; set; } = System.Array.Empty<float>();
	/// <summary>Слоты как числа ItemSlot.</summary>
	[Export] public int[] AllowedSlots { get; set; } = System.Array.Empty<int>();
	[Export] public bool IsPercent { get; set; }
	[Export] public bool Integer { get; set; }

	public int TierCount => Mathf.Min(TierMin.Length, TierMax.Length);

	public bool Allows(ItemSlot slot) => System.Array.IndexOf(AllowedSlots, (int)slot) >= 0;
}
