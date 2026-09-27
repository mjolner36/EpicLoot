using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;
using EpicLoot.Items;

namespace EpicLoot.UI;

/// <summary>Экран победы / смерти поверх боя: итог, пассивка, полученный или потерянный лут. Работает при паузе.</summary>
public partial class ResultScreen : CanvasLayer
{
	private Control _root;
	private Label _title;
	private RichTextLabel _body;

	public string BodyText => _body?.Text ?? "";
	public bool IsShown => _root?.Visible ?? false;

	public override void _Ready()
	{
		Layer = 10;
		ProcessMode = ProcessModeEnum.Always;

		_root = new ColorRect { Color = new Color(0, 0, 0, 0.6f), Visible = false };
		_root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(_root);

		var center = new CenterContainer();
		center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_root.AddChild(center);

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(560, 0) };
		center.AddChild(panel);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 16);
		panel.AddChild(box);

		_title = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = "HeaderLarge" };
		box.AddChild(_title);
		_body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(520, 0) };
		box.AddChild(_body);
		var button = new Button { Text = "На атлас", CustomMinimumSize = new Vector2(200, 44), SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
		button.Pressed += () => GameState.Instance.ReturnToAtlas();
		box.AddChild(button);
	}

	private static string ItemList(IEnumerable<ItemInstance> items) =>
		string.Join("\n", items.Select(i => $"• [color=#{ItemText.Hex(i.Color)}]{i.Name}[/color] [color=#888888]{ItemInstance.RarityName(i.Rarity)}, ур. {i.Level}[/color]"));

	private static string MapList(IEnumerable<MapInstance> maps) =>
		string.Join("\n", maps.Select(m => $"• [color=#{ItemText.Hex(m.Color)}]{ItemText.MapTitle(m)}[/color] [color=#888888]{ItemInstance.RarityName(m.Rarity)}[/color]"));

	private static string CurrencyList(Dictionary<string, int> currency) =>
		string.Join("\n", ItemText.CurrencyLines(currency, GameState.Instance.Items).Select(l => "• " + l));

	/// <summary>Предметы, карты и валюта одним списком; "ничего", если пусто.</summary>
	private static string LootList(IEnumerable<ItemInstance> items, IEnumerable<MapInstance> maps, Dictionary<string, int> currency)
	{
		var parts = new[] { ItemList(items), MapList(maps), currency == null ? "" : CurrencyList(currency) }.Where(p => p.Length > 0).ToArray();
		return parts.Length == 0 ? "ничего" : string.Join("\n", parts);
	}

	public void ShowVictory(AtlasNodeData node, bool firstTime, List<ItemInstance> stored, List<ItemInstance> lost,
		List<MapInstance> maps, List<MapInstance> lostMaps, Dictionary<string, int> currency)
	{
		_title.Text = "Узел пройден";
		_title.Modulate = new Color(0.6f, 1f, 0.6f);
		var text = "";
		if (node != null)
		{
			text += $"[center][b]{node.DisplayName}[/b][/center]\n";
			text += firstTime
				? $"Открыта пассивка:\n[color=#ffd070]{node.PassiveDescription}[/color]\n"
				: "[color=#aaaaaa]Узел уже был пройден — пассивка не выдаётся.[/color]\n";
		}
		text += "\n[b]Получено в тайник[/b]\n" + LootList(stored, maps, currency) + "\n";
		if (lost.Count > 0 || lostMaps.Count > 0)
			text += $"\n[color=#e07060][b]Не поместилось (сумка или тайник полны)[/b][/color]\n{LootList(lost, lostMaps, null)}";
		_body.Text = text.TrimEnd('\n');
		_root.Visible = true;
	}

	public void ShowDeath(AtlasNodeData node, RunBag bag, string title)
	{
		_title.Text = title;
		_title.Modulate = new Color(1f, 0.45f, 0.45f);
		var text = node != null ? $"[center]{node.DisplayName} не пройден. Прогресс атласа сохранён.[/center]\n" : "";
		if (bag != null)
		{
			text += "\n[b]Потеряно из сумки[/b]\n" + LootList(bag.Items, bag.Maps, bag.Currency);
			text += "\n\n[color=#aaaaaa]Надетая экипировка не теряется.[/color]";
		}
		_body.Text = text;
		_root.Visible = true;
	}
}
