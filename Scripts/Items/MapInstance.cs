using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using EpicLoot.Atlas;

namespace EpicLoot.Items;

/// <summary>Карта локации: узел атласа, тир, моды. Редкость считается по числу модов.</summary>
public class MapInstance
{
	public string Uid = ItemInstance.NewUid();
	public AtlasNodeData Node;
	public int Tier = 1;
	public readonly List<MapModData> Mods = new();

	public string Name => Node.DisplayName;
	public Rarity Rarity => ItemInstance.RarityOf(Mods.Count, false);
	public Color Color => ItemInstance.RarityColor(Rarity);
	public float Quantity => Mods.Sum(m => m.QuantityBonus);
	public float RarityBonus => Mods.Sum(m => m.RarityBonus);

	public Dictionary ToDict()
	{
		var mods = new Array();
		foreach (var m in Mods) mods.Add(m.Id);
		return new Dictionary { ["uid"] = Uid, ["node"] = Node.Id, ["tier"] = Tier, ["mods"] = mods };
	}

	public static MapInstance FromDict(Dictionary d, AtlasGraph atlas, ItemDatabase db)
	{
		var node = atlas.Get(d.GetValueOrDefault("node", "").AsString());
		if (node == null)
		{
			GD.PushWarning($"Сейв: неизвестный узел карты {d.GetValueOrDefault("node", "")}, карта пропущена");
			return null;
		}
		var map = new MapInstance
		{
			Uid = d.GetValueOrDefault("uid", ItemInstance.NewUid()).AsString(),
			Node = node,
			Tier = d.GetValueOrDefault("tier", node.Tier).AsInt32(),
		};
		if (d.TryGetValue("mods", out var mods))
			foreach (var id in mods.AsGodotArray())
			{
				var mod = db.MapMod(id.AsString());
				if (mod != null) map.Mods.Add(mod);
				else GD.PushWarning($"Сейв: неизвестный мод карты {id}, пропущен");
			}
		return map;
	}
}
