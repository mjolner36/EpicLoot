using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Core;
using EpicLoot.Items;
using EpicLoot.Stats;

namespace EpicLoot.UI;

/// <summary>
/// Инвентарь (только на атласе): кукла с 7 слотами, вкладки "Предметы" (сетка 10×6) и "Ресурсы" (сферы).
/// Надеть/снять, перетаскивание, сортировка, удаление, крафт сферами.
/// </summary>
public partial class InventoryScreen : Control
{
	public OrbData ArmedOrb { get; private set; }
	public string LastMessage => _message.Text;
	public IReadOnlyList<ItemCell> Cells => _cells;
	public IReadOnlyList<ItemCell> Doll => _doll;

	public void ShowTab(int index) => _tabs.CurrentTab = index;

	private readonly List<ItemCell> _cells = new();
	private readonly List<ItemCell> _doll = new();
	private TabContainer _tabs;
	private Label _message;
	private Label _stats;
	private VBoxContainer _orbList;
	private ConfirmationDialog _confirm;
	private int _pendingDelete = -1;

	private GameState Gs => GameState.Instance;
	private Inventory Inv => Gs.Profile;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.7f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);

		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		var panel = new PanelContainer();
		center.AddChild(panel);
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 10);
		panel.AddChild(outer);

		var header = new HBoxContainer();
		outer.AddChild(header);
		header.AddChild(new Label { Text = "Инвентарь", ThemeTypeVariation = "HeaderLarge", SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var close = new Button { Text = "Закрыть (I)" };
		close.Pressed += Close;
		header.AddChild(close);

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 24);
		outer.AddChild(body);

		// Кукла.
		var left = new VBoxContainer { CustomMinimumSize = new Vector2(280, 0) };
		left.AddThemeConstantOverride("separation", 8);
		body.AddChild(left);
		left.AddChild(new Label { Text = "Экипировка", ThemeTypeVariation = "HeaderSmall" });
		var doll = new GridContainer { Columns = 3 };
		doll.AddThemeConstantOverride("h_separation", 8);
		doll.AddThemeConstantOverride("v_separation", 8);
		left.AddChild(doll);
		ItemSlot?[] layout =
		{
			null, ItemSlot.Helmet, ItemSlot.Amulet,
			ItemSlot.Weapon, ItemSlot.Chest, ItemSlot.Ring,
			null, ItemSlot.Belt, null,
			null, ItemSlot.Boots, null,
		};
		foreach (var slot in layout)
		{
			if (slot == null)
			{
				doll.AddChild(new Control { CustomMinimumSize = new Vector2(76, 76) });
				continue;
			}
			var cell = new ItemCell { Screen = this, IsDoll = true, DollSlot = slot.Value };
			doll.AddChild(cell);
			_doll.Add(cell);
		}
		_stats = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(260, 0) };
		_stats.AddThemeFontSizeOverride("font_size", 15);
		left.AddChild(_stats);

		// Вкладки.
		var right = new VBoxContainer();
		body.AddChild(right);
		_tabs = new TabContainer { CustomMinimumSize = new Vector2(ItemCell.CellSize * 10 + 60, 400) };
		right.AddChild(_tabs);

		var itemsTab = new VBoxContainer { Name = "Предметы" };
		itemsTab.AddThemeConstantOverride("separation", 8);
		_tabs.AddChild(itemsTab);
		var toolbar = new HBoxContainer();
		itemsTab.AddChild(toolbar);
		var sort = new Button { Text = "Сортировать" };
		sort.Pressed += () =>
		{
			Inv.Sort();
			Changed();
		};
		toolbar.AddChild(sort);
		toolbar.AddChild(new Label
		{
			Text = "Двойной клик — надеть/снять · перетаскивание · ПКМ — удалить",
			Modulate = new Color(1, 1, 1, 0.55f),
			VerticalAlignment = VerticalAlignment.Center,
		});
		var grid = new GridContainer { Columns = Gs.Config.Loot.StashColumns };
		grid.AddThemeConstantOverride("h_separation", 4);
		grid.AddThemeConstantOverride("v_separation", 4);
		itemsTab.AddChild(grid);
		for (int i = 0; i < Inv.Stash.Length; i++)
		{
			var cell = new ItemCell { Screen = this, StashIndex = i };
			grid.AddChild(cell);
			_cells.Add(cell);
		}

		var resTab = new ScrollContainer { Name = "Ресурсы", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_tabs.AddChild(resTab);
		_orbList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_orbList.AddThemeConstantOverride("separation", 6);
		resTab.AddChild(_orbList);

		_message = new Label { CustomMinimumSize = new Vector2(0, 26) };
		right.AddChild(_message);

		_confirm = new ConfirmationDialog { Title = "Удаление", OkButtonText = "Удалить", CancelButtonText = "Отмена" };
		_confirm.Confirmed += () =>
		{
			if (_pendingDelete >= 0) Inv.Delete(_pendingDelete);
			_pendingDelete = -1;
			Changed();
		};
		AddChild(_confirm);
	}

	public void Open()
	{
		Visible = true;
		Disarm();
		_message.Text = "";
		Refresh();
	}

	public void Close()
	{
		Disarm();
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible) return;
		if (e.IsActionPressed(InputSetup.Abandon))
		{
			if (ArmedOrb != null) Disarm();
			else Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Changed()
	{
		Gs.SaveGame();
		Refresh();
	}

	public void Refresh()
	{
		foreach (var c in _cells) c.Refresh();
		foreach (var c in _doll) c.Refresh();
		RefreshOrbs();
		RefreshStats();
	}

	private void RefreshOrbs()
	{
		foreach (var child in _orbList.GetChildren()) child.QueueFree();
		foreach (var orb in Gs.Items.Orbs)
		{
			int count = Inv.OrbCount(orb.Id);
			var row = new Button
			{
				CustomMinimumSize = new Vector2(0, 52),
				Disabled = count == 0,
				ToggleMode = true,
				ButtonPressed = ArmedOrb == orb,
				TooltipText = orb.Description,
			};
			var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
			h.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			h.AddThemeConstantOverride("separation", 10);
			row.AddChild(h);
			h.AddChild(new TextureRect
			{
				Texture = orb.Icon,
				CustomMinimumSize = new Vector2(40, 40),
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				SizeFlagsVertical = SizeFlags.ShrinkCenter,
				MouseFilter = MouseFilterEnum.Ignore,
			});
			var texts = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill };
			h.AddChild(texts);
			texts.AddChild(new Label { Text = orb.DisplayName, MouseFilter = MouseFilterEnum.Ignore });
			var desc = new Label { Text = orb.Description, Modulate = new Color(1, 1, 1, 0.6f), MouseFilter = MouseFilterEnum.Ignore };
			desc.AddThemeFontSizeOverride("font_size", 13);
			texts.AddChild(desc);
			h.AddChild(new Label { Text = $"×{count}", CustomMinimumSize = new Vector2(60, 0), VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore });
			var captured = orb;
			row.Pressed += () => Arm(captured);
			_orbList.AddChild(row);
		}
	}

	private void RefreshStats()
	{
		// Итоговые статы с экипировкой и пассивками — "заметно в бою" видно ещё до входа.
		var s = new StatBlock();
		Gs.Config.Player.ApplyTo(s);
		foreach (var n in Gs.ActivePassives) s.AddModifiers(n.PassiveModifiers, "passive:" + n.Id);
		foreach (var item in Inv.EquippedItems) s.AddModifiers(item.Modifiers(), item.SourceId);
		var sword = Gs.Config.Player.Skills.FirstOrDefault();
		float swordDmg = s.Get(StatType.WeaponDamage) > 0 ? s.Get(StatType.WeaponDamage) : sword?.PhysicalDamage ?? 0;
		_stats.Text =
			$"Жизнь: {s.Get(StatType.MaxLife):0}\n" +
			$"Броня: {s.Get(StatType.Armour):0}\n" +
			$"Удар мечом: {swordDmg * s.Get(StatType.Damage):0.#} физ.\n" +
			$"Урон: ×{s.Get(StatType.Damage):0.##} · огонь ×{s.Get(StatType.FireDamage):0.##} · холод ×{s.Get(StatType.ColdDamage):0.##} · молния ×{s.Get(StatType.LightningDamage):0.##}\n" +
			$"Скорость умений: ×{s.Get(StatType.AttackSpeed):0.##}\n" +
			$"Крит: {s.Get(StatType.CritChance) * 100:0.#}% × {s.Get(StatType.CritMultiplier) * 100:0}%\n" +
			$"Скорость: {s.Get(StatType.MoveSpeed):0.##} м/с · Рывок: {s.Get(StatType.DashCharges):0}\n" +
			$"Снаряды осколка: {1 + s.Get(StatType.ShardProjectiles):0}";
		s.Free();
	}

	// ---------- Действия ----------

	public void Arm(OrbData orb)
	{
		if (Inv.OrbCount(orb.Id) <= 0) return;
		ArmedOrb = orb;
		_tabs.CurrentTab = 0;
		_message.Text = $"{orb.DisplayName}: кликните по предмету (ПКМ или Esc — отмена)";
		Refresh();
	}

	public void Disarm()
	{
		if (ArmedOrb == null) return;
		ArmedOrb = null;
		_message.Text = "";
		Refresh();
	}

	/// <summary>Применяет взведённую сферу к предмету ячейки. Сфера тратится только при успехе.</summary>
	public void ApplyArmedOrb(ItemCell cell) => ApplyOrb(ArmedOrb, cell.Item);

	public bool ApplyOrb(OrbData orb, ItemInstance item)
	{
		if (orb == null || item == null) return false;
		if (Inv.OrbCount(orb.Id) <= 0)
		{
			_message.Text = "Сферы закончились";
			return false;
		}
		var (ok, msg) = Crafting.Apply(orb, item, Gs.Generator, Gs.Config.Loot);
		_message.Text = msg;
		_message.Modulate = ok ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f);
		if (!ok) return false;
		Inv.AddOrbs(orb.Id, -1);
		if (Inv.OrbCount(orb.Id) <= 0) ArmedOrb = null;
		Changed();
		return true;
	}

	public void ToggleEquip(ItemCell cell)
	{
		if (cell.Item == null) return;
		bool ok = cell.IsDoll ? Inv.Unequip(cell.DollSlot) : Inv.Equip(cell.StashIndex);
		if (!ok && cell.IsDoll) _message.Text = "Тайник полон";
		Changed();
	}

	public void AskDelete(int index)
	{
		var item = Inv.Stash[index];
		if (item == null) return;
		_pendingDelete = index;
		_confirm.DialogText = $"Удалить «{item.Name}» ({ItemInstance.RarityName(item.Rarity)}, ур. {item.Level})?";
		_confirm.PopupCentered();
	}

	public bool CanDrop(ItemCell target, Godot.Collections.Dictionary data)
	{
		bool fromDoll = data["doll"].AsBool();
		var dragged = fromDoll ? Inv.Equipped.GetValueOrDefault((ItemSlot)data["slot"].AsInt32()) : Inv.Stash[data["index"].AsInt32()];
		if (dragged == null) return false;
		if (target.IsDoll) return !fromDoll && dragged.Slot == target.DollSlot;
		// Снять в ячейку: пустую или с предметом того же слота (обмен).
		if (fromDoll) return target.Item == null || target.Item.Slot == dragged.Slot;
		return true;
	}

	public void Drop(ItemCell target, Godot.Collections.Dictionary data)
	{
		if (!CanDrop(target, data)) return;
		bool fromDoll = data["doll"].AsBool();
		if (target.IsDoll) Inv.Equip(data["index"].AsInt32());
		else if (fromDoll) Inv.Unequip((ItemSlot)data["slot"].AsInt32(), target.StashIndex);
		else Inv.Move(data["index"].AsInt32(), target.StashIndex);
		Changed();
	}
}
