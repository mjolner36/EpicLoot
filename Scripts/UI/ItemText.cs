using System.Collections.Generic;
using System.Linq;
using System.Text;
using EpicLoot.Items;
using EpicLoot.Stats;

namespace EpicLoot.UI;

/// <summary>Тексты предметов для тултипов, экранов результата и сравнения с надетым.</summary>
public static class ItemText
{
	public static readonly Dictionary<StatType, string> StatNames = new()
	{
		[StatType.MaxLife] = "жизнь",
		[StatType.MoveSpeed] = "скорость движения",
		[StatType.Damage] = "весь урон",
		[StatType.FireDamage] = "урон огнём",
		[StatType.ColdDamage] = "урон холодом",
		[StatType.LightningDamage] = "урон молнией",
		[StatType.AttackSpeed] = "скорость умений",
		[StatType.CritChance] = "шанс крита",
		[StatType.CritMultiplier] = "урон крита",
		[StatType.AilmentChance] = "шанс статусов",
		[StatType.AilmentDuration] = "длительность статусов",
		[StatType.DashCharges] = "заряды рывка",
		[StatType.Armour] = "броня",
		[StatType.FireBlastCooldown] = "кулдаун Огненного взрыва",
		[StatType.WeaponDamage] = "урон Удара мечом",
		[StatType.ShardProjectiles] = "снаряды Ледяного осколка",
	};

	/// <summary>Статы, чьи Flat-значения — доли (0.05 = 5%).</summary>
	public static bool IsFraction(StatType s) => s is StatType.CritChance or StatType.CritMultiplier or StatType.AilmentChance;

	/// <summary>Статы, где меньше — лучше.</summary>
	public static bool LowerIsBetter(StatType s) => s == StatType.FireBlastCooldown;

	public static string Hex(Godot.Color c) => c.ToHtml(false);

	private static string Num(float v, bool percent, bool integer)
	{
		if (percent) return (v * 100f).ToString("0.#");
		return integer ? Godot.Mathf.RoundToInt(v).ToString() : v.ToString("0.##");
	}

	public static string BaseLine(ItemInstance item)
	{
		var b = item.Base;
		if (b.BaseStat == StatType.None || string.IsNullOrEmpty(b.BaseText)) return "";
		return string.Format(b.BaseText, Num(item.BaseValue, b.BaseIsPercent, !b.BaseIsPercent));
	}

	public static string AffixLine(AffixRoll a) =>
		string.Format(a.Affix.Text, Num(a.Value, a.Affix.IsPercent, a.Affix.Integer));

	/// <summary>Полное описание предмета в BBCode.</summary>
	public static string Describe(ItemInstance item)
	{
		var sb = new StringBuilder();
		sb.Append($"[b][color=#{Hex(item.Color)}]{item.Name}[/color][/b]\n");
		sb.Append($"[color=#aaaaaa]{ItemInstance.RarityName(item.Rarity)} · уровень {item.Level}[/color]\n");
		var baseLine = BaseLine(item);
		if (baseLine.Length > 0) sb.Append($"{baseLine}\n");
		if (item.Affixes.Count > 0) sb.Append("[color=#666666]──────────[/color]\n");
		foreach (var a in item.Affixes)
			sb.Append($"[color=#8fb8ff]{AffixLine(a)}[/color] [color=#777777]T{a.Tier}[/color]\n");
		if (item.Corrupted) sb.Append("[color=#e05050]Осквернён[/color]\n");
		return sb.ToString().TrimEnd('\n');
	}

	private static Dictionary<(StatType, ModifierType), float> Sum(ItemInstance item)
	{
		var d = new Dictionary<(StatType, ModifierType), float>();
		if (item == null) return d;
		foreach (var m in item.Modifiers())
			d[(m.Stat, m.Type)] = d.GetValueOrDefault((m.Stat, m.Type)) + m.Value;
		return d;
	}

	/// <summary>Сравнение с надетым в том же слоте: зелёный лучше, красный хуже.</summary>
	public static string Compare(ItemInstance item, ItemInstance equipped)
	{
		if (equipped == null) return "[color=#88dd88]Слот пуст — предмет только добавит свойства[/color]";
		if (equipped == item) return "[color=#aaaaaa]Надето[/color]";
		var a = Sum(item);
		var b = Sum(equipped);
		var lines = new List<string>();
		foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k.Item1))
		{
			float diff = a.GetValueOrDefault(key) - b.GetValueOrDefault(key);
			if (System.Math.Abs(diff) < 0.0001f) continue;
			var (stat, type) = key;
			bool better = LowerIsBetter(stat) ? diff < 0 : diff > 0;
			string value = type == ModifierType.Flat && !IsFraction(stat)
				? diff.ToString("+0.##;-0.##")
				: (diff * 100f).ToString("+0.#;-0.#") + "%";
			string name = StatNames.GetValueOrDefault(stat, stat.ToString());
			lines.Add($"[color=#{(better ? "66dd66" : "e05555")}]{value} {name}[/color]");
		}
		if (lines.Count == 0) return "[color=#aaaaaa]Равноценно надетому[/color]";
		return "[color=#aaaaaa]По сравнению с надетым:[/color]\n" + string.Join("\n", lines);
	}
}
