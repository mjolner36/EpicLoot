using System;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;
using EpicLoot.Items;

namespace EpicLoot.UI;

/// <summary>
/// Панель атласа в двух режимах. Узел: пассивка, тир, карты в тайнике, "Открыть T1 бесплатно".
/// Устройство карт (узел "Старт"): слот под карту, её моды и бонус к луту, "Открыть" расходует карту.
/// </summary>
public partial class NodePanel : PanelContainer
{
	/// <summary>Просьба открыть окно крафта для карты (инвентарь на атласе).</summary>
	public event Action<MapInstance> CraftMapRequested;

	public MapInstance SelectedMap { get; private set; }
	public bool DeviceMode { get; private set; }
	public AtlasNodeData ShownNode => _node;

	private Label _title;
	private Label _type;
	private RichTextLabel _body;
	private VBoxContainer _mapList;
	private ScrollContainer _mapScroll;
	private Button _primary;
	private Button _secondary;
	private AtlasNodeData _node;

	private GameState Gs => GameState.Instance;

	public override void _Ready()
	{
		CustomMinimumSize = new Vector2(520, 0);
		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 10);
		AddChild(box);

		_title = new Label { ThemeTypeVariation = "HeaderLarge" };
		box.AddChild(_title);
		_type = new Label { Modulate = new Color(1, 1, 1, 0.7f) };
		box.AddChild(_type);

		_mapScroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 170), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		box.AddChild(_mapScroll);
		_mapList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_mapList.AddThemeConstantOverride("separation", 4);
		_mapScroll.AddChild(_mapList);

		_body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(500, 0), ScrollActive = false };
		box.AddChild(_body);

		var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
		buttons.AddThemeConstantOverride("separation", 10);
		box.AddChild(buttons);
		var close = new Button { Text = "Закрыть", CustomMinimumSize = new Vector2(120, 40) };
		close.Pressed += Hide;
		buttons.AddChild(close);
		_secondary = new Button { CustomMinimumSize = new Vector2(150, 40) };
		_secondary.Pressed += OnSecondary;
		buttons.AddChild(_secondary);
		_primary = new Button { CustomMinimumSize = new Vector2(190, 40) };
		_primary.Pressed += OnPrimary;
		buttons.AddChild(_primary);
		Hide();
	}

	// ---------- Узел ----------

	public void ShowNode(AtlasNodeData node)
	{
		if (node.Type == AtlasNodeType.Start)
		{
			ShowDevice();
			return;
		}
		DeviceMode = false;
		_node = node;
		SelectedMap = null;
		var state = Gs.StateOf(node.Id);
		var color = AtlasView.BranchColor(node.Branch).ToHtml(false);

		_title.Text = node.DisplayName;
		_title.Modulate = AtlasView.BranchColor(node.Branch);
		_type.Text = $"{node.TypeName} · T{node.Tier}";
		_mapScroll.Visible = false;

		var text = $"[b]Пассивка за первое прохождение[/b]\n[color=#{color}]{node.PassiveDescription}[/color]\n\n";
		text += $"[b]Сложность[/b]\n{DifficultyText(node.Tier)}\n";
		if (node.Type == AtlasNodeType.Keystone)
			text += $"Вместо волн — элитный рыцарь (жизнь ×{Gs.Config.KeystoneLifeMultiplier:0.#}) с эскортом\n";
		int maps = Gs.Profile.MapCount(node);
		text += $"\n[b]Карт этой локации в тайнике:[/b] {maps}\n";
		text += state switch
		{
			AtlasNodeState.Completed => "\n[color=#aaaaaa]Узел пройден: повторные забеги дают только лут.[/color]",
			AtlasNodeState.Available => "\n[color=#aaaaaa]Карты этой локации могут выпасть в забегах.[/color]",
			_ => "\n[color=#aaaaaa]Закрыт: карты начнут выпадать, когда пройден соседний узел.[/color]",
		};
		_body.Text = text;

		_secondary.Text = "В устройство";
		_secondary.Visible = true;
		_secondary.Disabled = maps == 0;
		_primary.Text = "Открыть T1 бесплатно";
		_primary.Visible = Gs.CanOpenFree(node);
		_primary.Disabled = false;
		Show();
	}

	private string DifficultyText(int tier)
	{
		int steps = Mathf.Max(0, tier - 1);
		return $"T{tier}: +{Mathf.RoundToInt(Gs.Config.DifficultyPerStep * steps * 100)}% жизни и урона врагов · аффиксы лута до T{Gs.Generator.MaxDropTier(tier)}";
	}

	// ---------- Устройство карт ----------

	/// <summary>Открывает устройство карт; preselect — карта, сразу положенная в слот.</summary>
	public void ShowDevice(MapInstance preselect = null)
	{
		DeviceMode = true;
		_node = Gs.Atlas.Get(Gs.Atlas.StartId);
		_title.Text = "Устройство карт";
		_title.Modulate = Colors.White;
		_type.Text = "Положите карту из тайника и откройте локацию. Карта расходуется.";
		_mapScroll.Visible = true;
		SelectedMap = preselect != null && Gs.Profile.Maps.Contains(preselect) ? preselect : null;
		RefreshDevice();
		Show();
	}

	public void SelectMap(MapInstance map)
	{
		SelectedMap = map != null && Gs.Profile.Maps.Contains(map) ? map : null;
		RefreshDevice();
	}

	private void RefreshDevice()
	{
		foreach (var child in _mapList.GetChildren()) child.QueueFree();
		var maps = Gs.Profile.Maps.OrderBy(m => m.Tier).ThenBy(m => m.Node.Branch).ThenBy(m => m.Name).ThenByDescending(m => m.Mods.Count).ToList();
		if (maps.Count == 0)
			_mapList.AddChild(new Label { Text = "В тайнике нет карт. Карты T1 открываются бесплатно кликом по узлу.", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(480, 0), Modulate = new Color(1, 1, 1, 0.6f) });
		foreach (var map in maps)
		{
			var b = new Button
			{
				Text = $"{ItemText.MapTitle(map)} · {ItemInstance.RarityName(map.Rarity)}" + (map.Mods.Count > 0 ? $" · модов {map.Mods.Count}" : ""),
				ToggleMode = true,
				ButtonPressed = map == SelectedMap,
				Alignment = HorizontalAlignment.Left,
				TooltipText = string.Join("\n", map.Mods.Select(m => $"{m.DisplayName}: {m.Description}")),
			};
			b.AddThemeColorOverride("font_color", map.Color);
			b.AddThemeColorOverride("font_pressed_color", map.Color.Lightened(0.3f));
			var captured = map;
			b.Pressed += () => SelectMap(captured);
			_mapList.AddChild(b);
		}

		if (SelectedMap == null)
		{
			_body.Text = "[color=#888888]Слот пуст — выберите карту из списка.[/color]";
		}
		else
		{
			var m = SelectedMap;
			var text = $"[b]В слоте[/b]\n{ItemText.DescribeMap(m)}\n\n";
			text += $"[b]Локация[/b]: {m.Node.DisplayName} ({m.Node.TypeName})\n{DifficultyText(m.Tier)}\n";
			text += $"Пассивка: [color=#{AtlasView.BranchColor(m.Node.Branch).ToHtml(false)}]{m.Node.PassiveDescription}[/color]";
			if (Gs.StateOf(m.Node.Id) == AtlasNodeState.Completed) text += " [color=#888888](уже активна)[/color]";
			_body.Text = text;
		}
		_secondary.Text = "Крафт карты";
		_secondary.Visible = true;
		_secondary.Disabled = SelectedMap == null;
		_primary.Text = "Открыть";
		_primary.Visible = true;
		_primary.Disabled = SelectedMap == null;
	}

	/// <summary>Обновить после изменений тайника (крафт карты, сброс).</summary>
	public void Refresh()
	{
		if (!Visible) return;
		if (DeviceMode) SelectMap(SelectedMap);
		else if (_node != null) ShowNode(_node);
	}

	private void OnPrimary()
	{
		if (DeviceMode)
		{
			if (SelectedMap != null) Gs.OpenMap(SelectedMap);
		}
		else if (_node != null)
		{
			Gs.OpenFree(_node);
		}
	}

	private void OnSecondary()
	{
		if (DeviceMode)
		{
			if (SelectedMap != null) CraftMapRequested?.Invoke(SelectedMap);
		}
		else if (_node != null)
		{
			ShowDevice(Gs.Profile.Maps.Where(m => m.Node == _node).OrderByDescending(m => m.Mods.Count).FirstOrDefault());
		}
	}
}
