using Godot;
using EpicLoot.Core;
using EpicLoot.UI;

namespace EpicLoot.Atlas;

/// <summary>Корень экрана атласа: граф, панель узла, список активных пассивок, сброс.</summary>
public partial class AtlasScreen : Control
{
	private AtlasView _view;
	private NodePanel _panel;
	private VBoxContainer _passives;
	public InventoryScreen Inventory { get; private set; }

	public override void _Ready()
	{
		SetAnchorsPreset(LayoutPreset.FullRect);
		var bg = new ColorRect { Color = new Color(0.07f, 0.07f, 0.09f) };
		bg.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(bg);

		var split = new HBoxContainer();
		split.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(split);

		// Слева — список активных пассивок (текущий билд) и сброс.
		var side = new PanelContainer { CustomMinimumSize = new Vector2(320, 0) };
		split.AddChild(side);
		var sideBox = new VBoxContainer();
		sideBox.AddThemeConstantOverride("separation", 8);
		side.AddChild(sideBox);
		sideBox.AddChild(new Label { Text = "Атлас", ThemeTypeVariation = "HeaderLarge" });
		sideBox.AddChild(new Label { Text = "Активные пассивки", ThemeTypeVariation = "HeaderSmall" });
		var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		sideBox.AddChild(scroll);
		_passives = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_passives.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_passives);
		var device = new Button { Text = "Устройство карт", CustomMinimumSize = new Vector2(0, 44) };
		device.Pressed += () => _panel.ShowDevice();
		sideBox.AddChild(device);
		var inventory = new Button { Text = "Инвентарь (I)", CustomMinimumSize = new Vector2(0, 44) };
		inventory.Pressed += () => Inventory.Open();
		sideBox.AddChild(inventory);
		var reset = new Button { Text = "Сбросить атлас", CustomMinimumSize = new Vector2(0, 40) };
		reset.Pressed += () =>
		{
			GameState.Instance.ResetAtlas();
			_panel.Hide();
		};
		sideBox.AddChild(reset);

		_view = new AtlasView { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
		split.AddChild(_view);
		_view.NodeClicked += node => _panel.ShowNode(node);

		var hint = new Label { Text = "Центр атласа — устройство карт. Карты T1 открываются бесплатно кликом по узлу.", Modulate = new Color(1, 1, 1, 0.55f) };
		hint.SetAnchorsPreset(LayoutPreset.CenterBottom);
		hint.Position = new Vector2(-280, -40);
		AddChild(hint);

		var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(center);
		_panel = new NodePanel();
		center.AddChild(_panel);

		Inventory = new InventoryScreen { Name = "Inventory" };
		AddChild(Inventory);
		Inventory.MapToDevice += map =>
		{
			Inventory.Close();
			_panel.ShowDevice(map);
		};
		_panel.CraftMapRequested += map => Inventory.OpenCraft(map);

		EventBus.Instance.AtlasChanged += Refresh;
		GameState.Instance.Profile.Changed += OnProfileChanged;
		Refresh();
	}

	public NodePanel Panel => _panel;

	public override void _ExitTree()
	{
		EventBus.Instance.AtlasChanged -= Refresh;
		GameState.Instance.Profile.Changed -= OnProfileChanged;
	}

	private void OnProfileChanged()
	{
		_view.Refresh();
		_panel.Refresh();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed(InputSetup.Inventory))
		{
			if (Inventory.Visible) Inventory.Close();
			else Inventory.Open();
		}
		else if (e.IsActionPressed(InputSetup.Abandon)) _panel.Hide();
	}

	private void Refresh()
	{
		_view.Refresh();
		foreach (var child in _passives.GetChildren()) child.QueueFree();
		int count = 0;
		foreach (var node in GameState.Instance.ActivePassives)
		{
			var label = new Label
			{
				Text = $"{node.DisplayName}: {node.PassiveDescription}",
				AutowrapMode = TextServer.AutowrapMode.WordSmart,
				CustomMinimumSize = new Vector2(290, 0),
				Modulate = AtlasView.BranchColor(node.Branch),
			};
			label.AddThemeFontSizeOverride("font_size", 15);
			_passives.AddChild(label);
			count++;
		}
		if (count == 0)
			_passives.AddChild(new Label { Text = "Пока нет. Пройдите узел рядом со стартом.", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(290, 0), Modulate = new Color(1, 1, 1, 0.5f) });
	}
}
