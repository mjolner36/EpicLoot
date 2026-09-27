using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using EpicLoot.Stats;

namespace EpicLoot.Items;

public class AffixRoll
{
	public AffixData Affix;
	/// <summary>1 = T1 (слабый) … 5 = T5 (сильный).</summary>
	public int Tier;
	public float Value;
}

/// <summary>Предмет в рантайме. Редкость считается по аффиксам. Сериализуется в JSON-сейв.</summary>
public class ItemInstance
{
	public string Uid = NewUid();
	public ItemBaseData Base;
	/// <summary>Уровень = тир карты, на которой предмет выпал.</summary>
	public int Level = 1;
	public float BaseValue;
	public readonly List<AffixRoll> Affixes = new();
	/// <summary>Потенциал ковки.</summary>
	public int Potential;

	public static string NewUid() => System.Guid.NewGuid().ToString("N")[..12];

	public string SourceId => "item:" + Uid;
	public ItemSlot Slot => Base.Slot;
	public string Name => Base.DisplayName;

	public int Prefixes => Affixes.Count(a => a.Affix.IsPrefix);
	public int Suffixes => Affixes.Count(a => !a.Affix.IsPrefix);

	/// <summary>0 — обычный, 1-2 — магический, 3-4 — редкий, любой T5 — возвышенный.</summary>
	public Rarity Rarity => RarityOf(Affixes.Count, Affixes.Any(a => a.Tier >= 5));

	public static Rarity RarityOf(int affixes, bool hasT5) =>
		hasT5 ? Rarity.Exalted : affixes == 0 ? Rarity.Normal : affixes <= 2 ? Rarity.Magic : Rarity.Rare;

	public static Color RarityColor(Rarity r) => r switch
	{
		Rarity.Magic => new Color(0.45f, 0.6f, 1f),
		Rarity.Rare => new Color(1f, 0.88f, 0.3f),
		Rarity.Exalted => new Color(0.78f, 0.45f, 1f),
		_ => new Color(0.92f, 0.92f, 0.92f),
	};

	public Color Color => RarityColor(Rarity);

	public static string RarityName(Rarity r) => r switch
	{
		Rarity.Magic => "Магический",
		Rarity.Rare => "Редкий",
		Rarity.Exalted => "Возвышенный",
		_ => "Обычный",
	};

	public AffixRoll Roll(AffixData a) => Affixes.FirstOrDefault(x => x.Affix == a);
	public bool HasAffix(AffixData a) => Roll(a) != null;

	/// <summary>Все модификаторы предмета: база + аффиксы.</summary>
	public IEnumerable<Modifier> Modifiers()
	{
		if (Base.BaseStat != StatType.None)
			yield return new Modifier(Base.BaseStat, Base.BaseType, BaseValue, SourceId);
		foreach (var a in Affixes)
			yield return new Modifier(a.Affix.Stat, a.Affix.Type, a.Value, SourceId);
	}

	public Dictionary ToDict()
	{
		var affixes = new Array();
		foreach (var a in Affixes)
			affixes.Add(new Dictionary { ["id"] = a.Affix.Id, ["tier"] = a.Tier, ["value"] = a.Value });
		return new Dictionary
		{
			["uid"] = Uid,
			["base"] = Base.Id,
			["level"] = Level,
			["base_value"] = BaseValue,
			["affixes"] = affixes,
			["potential"] = Potential,
		};
	}

	public static ItemInstance FromDict(Dictionary d, ItemDatabase db)
	{
		var baseData = db.Base(d.GetValueOrDefault("base", "").AsString());
		if (baseData == null)
		{
			GD.PushWarning($"Сейв: неизвестная база предмета {d.GetValueOrDefault("base", "")}, предмет пропущен");
			return null;
		}
		var item = new ItemInstance
		{
			Uid = d.GetValueOrDefault("uid", NewUid()).AsString(),
			Base = baseData,
			Level = d.GetValueOrDefault("level", 1).AsInt32(),
			BaseValue = (float)d.GetValueOrDefault("base_value", 0).AsDouble(),
			Potential = d.GetValueOrDefault("potential", 0).AsInt32(),
		};
		if (d.TryGetValue("affixes", out var arr))
		{
			foreach (var v in arr.AsGodotArray())
			{
				var ad = v.AsGodotDictionary();
				var affix = db.Affix(ad.GetValueOrDefault("id", "").AsString());
				if (affix == null)
				{
					GD.PushWarning($"Сейв: неизвестный аффикс {ad.GetValueOrDefault("id", "")}, пропущен");
					continue;
				}
				item.Affixes.Add(new AffixRoll
				{
					Affix = affix,
					Tier = Mathf.Clamp(ad.GetValueOrDefault("tier", 1).AsInt32(), 1, affix.TierCount),
					Value = (float)ad.GetValueOrDefault("value", 0).AsDouble(),
				});
			}
		}
		return item;
	}
}
