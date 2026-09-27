using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Atlas;
using EpicLoot.Core;
using EpicLoot.Items;
using EpicLoot.Stats;

namespace EpicLoot.UI;

/// <summary>
/// Инвентарь (только на атласе): кукла с 7 слотами, вкладки "Предметы" (сетка 10×6), "Ресурсы" (осколки, глифы, руны)
/// и "Карты" (по тиру, фильтр по ветке). Надеть/снять, перетаскивание, сортировка, крафт, разборка (одиночная и массовая).
/// </summary>
public partial class InventoryScreen : Control
{
	public const int TabItems = 0, TabResources = 1, TabMaps = 2;

	/// <summary>Кнопка "В устройство" у карты.</summary>
	public event Action<MapInstance> MapToDevice;

	public string LastMessage => _message.Text;
	public IReadOnlyList<ItemCell> Cells => _cells;
	public IReadOnlyList<ItemCell> Doll => _doll;
	public CraftWindow Craft => _craft;
	public bool MassMode { get; private set; }
	public int SelectedCount => _selected.Count;

	public void ShowTab(int index) => _tabs.CurrentTab = index;

	private readonly List<ItemCell> _cells = new();
	private readonly List<ItemCell> _doll = new();
	private readonly HashSet<int> _selected = new();
	private TabContainer _tabs;
	private Label _message;
	private Label _stats;
	private VBoxContainer _resList;
	private VBoxContainer _mapList;
	private OptionButton _branchFilter;
	private Button _massToggle;
	private Button _massConfirm;
	private ConfirmationDialog _confirm;
	private PopupMenu _menu;
	private CraftWindow _craft;
	private Action _pendingConfirm;
	private ItemCell _menuCell;

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
		_tabs = new TabContainer { CustomMinimumSize = new Vector2(ItemCell.CellSize * 10 + 60, 430) };
		right.AddChild(_tabs);

		// Предметы.
		var itemsTab = new VBoxContainer { Name = "Предметы" };
		itemsTab.AddThemeConstantOverride("separation", 8);
		_tabs.AddChild(itemsTab);
		var toolbar = new HBoxContainer();
		toolbar.AddThemeConstantOverride("separation", 8);
		itemsTab.AddChild(toolbar);
		var sort = new Button { Text = "Сортировать" };
		sort.Pressed += () =>
		{
			ExitMassMode();
			Inv.Sort();
			Changed();
		};
		toolbar.AddChild(sort);
		_massToggle = new Button { Text = "Массовая разборка", ToggleMode = true };
		_massToggle.Toggled += on =>
		{
			if (on) EnterMassMode();
			else ExitMassMode();
		};
		toolbar.AddChild(_massToggle);
		_massConfirm = new Button { Visible = false };
		_massConfirm.Pressed += AskDisassembleSelected;
		toolbar.AddChild(_massConfirm);
		var hint = new Label
		{
			Text = "Двойной клик — надеть/снять · перетаскивание · ПКМ — меню",
			Modulate = new Color(1, 1, 1, 0.55f),
			VerticalAlignment = VerticalAlignment.Center,
		};
		hint.AddThemeFontSizeOverride("font_size", 13);
		toolbar.AddChild(hint);
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

		// Ресурсы.
		var resTab = new ScrollContainer { Name = "Ресурсы", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		_tabs.AddChild(resTab);
		_resList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_resList.AddThemeConstantOverride("separation", 4);
		resTab.AddChild(_resList);

		// Карты.
		var mapsTab = new VBoxContainer { Name = "Карты" };
		mapsTab.AddThemeConstantOverride("separation", 8);
		_tabs.AddChild(mapsTab);
		var mapBar = new HBoxContainer();
		mapsTab.AddChild(mapBar);
		mapBar.AddChild(new Label { Text = "Ветка атласа:", VerticalAlignment = VerticalAlignment.Center });
		_branchFilter = new OptionButton();
		_branchFilter.AddItem("Все");
		_branchFilter.AddItem("Огонь");
		_branchFilter.AddItem("Холод");
		_branchFilter.AddItem("Молния");
		_branchFilter.ItemSelected += _ => RefreshMaps();
		mapBar.AddChild(_branchFilter);
		var mapScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		mapsTab.AddChild(mapScroll);
		_mapList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_mapList.AddThemeConstantOverride("separation", 4);
		mapScroll.AddChild(_mapList);

		_message = new Label { CustomMinimumSize = new Vector2(0, 26), AutowrapMode = TextServer.AutowrapMode.WordSmart };
		right.AddChild(_message);

		_confirm = new ConfirmationDialog { Title = "Разборка", OkButtonText = "Разобрать", CancelButtonText = "Отмена", DialogAutowrap = true };
		_confirm.Confirmed += () =>
		{
			var action = _pendingConfirm;
			_pendingConfirm = null;
			action?.Invoke();
		};
		AddChild(_confirm);

		_menu = new PopupMenu();
		_menu.IdPressed += OnMenu;
		AddChild(_menu);

		_craft = new CraftWindow { Name = "Craft" };
		AddChild(_craft);
	}

	public void Open()
	{
		Visible = true;
		ExitMassMode();
		_message.Text = "";
		Refresh();
	}

	public void Close()
	{
		ExitMassMode();
		if (_craft.Visible) _craft.Close();
		Visible = false;
	}

	/// <summary>Открыть окно крафта для предмета или карты.</summary>
	public void OpenCraft(object target)
	{
		if (!Visible) Open();
		ExitMassMode();
		_craft.Open(target);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (!Visible || _craft.Visible) return;
		if (e.IsActionPressed(InputSetup.Abandon))
		{
			if (MassMode) ExitMassMode();
			else Close();
			GetViewport().SetInputAsHandled();
		}
	}

	private void Changed()
	{
		Gs.SaveGame();
		Refresh();
	}

	private void Say(string text, bool ok = true)
	{
		_message.Text = text;
		_message.Modulate = ok ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f);
	}

	public void Refresh()
	{
		_selected.RemoveWhere(i => Inv.Stash[i] == null);
		foreach (var c in _cells) c.Refresh();
		foreach (var c in _doll) c.Refresh();
		_massConfirm.Visible = MassMode;
		_massConfirm.Text = $"Разобрать выбранные ({_selected.Count})";
		_massConfirm.Disabled = _selected.Count == 0;
		RefreshResources();
		RefreshMaps();
		RefreshStats();
		if (_craft.Visible) _craft.Refresh();
	}

	private void RefreshResources()
	{
		foreach (var child in _resList.GetChildren()) child.QueueFree();
		foreach (var kind in new[] { CurrencyKind.Shard, CurrencyKind.Glyph, CurrencyKind.Rune })
		{
			var group = Gs.Items.Currency.Where(c => c.Kind == kind).ToList();
			int total = group.Sum(c => Inv.CurrencyCount(c));
			_resList.AddChild(new Label
			{
				Text = (kind switch { CurrencyKind.Shard => "Осколки аффиксов", CurrencyKind.Glyph => "Глифы", _ => "Руны" }) + $" · {total}",
				ThemeTypeVariation = "HeaderSmall",
			});
			var grid = new GridContainer { Columns = kind == CurrencyKind.Shard ? 2 : 1 };
			grid.AddThemeConstantOverride("h_separation", 12);
			grid.AddThemeConstantOverride("v_separation", 2);
			_resList.AddChild(grid);
			foreach (var c in group)
			{
				int n = Inv.CurrencyCount(c);
				var row = new HBoxContainer { TooltipText = c.Description, MouseFilter = MouseFilterEnum.Pass, CustomMinimumSize = new Vector2(kind == CurrencyKind.Shard ? 280 : 560, 26) };
				row.AddThemeConstantOverride("separation", 8);
				row.Modulate = new Color(1, 1, 1, n > 0 ? 1f : 0.4f);
				row.AddChild(new TextureRect
				{
					Texture = c.Icon,
					CustomMinimumSize = new Vector2(20, 20),
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					SizeFlagsVertical = SizeFlags.ShrinkCenter,
					MouseFilter = MouseFilterEnum.Ignore,
				});
				var name = kind == CurrencyKind.Shard ? c.Affix?.DisplayName ?? c.DisplayName : $"{c.DisplayName} — {c.Description}";
				var label = new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, MouseFilter = MouseFilterEnum.Ignore };
				label.AddThemeFontSizeOverride("font_size", 14);
				row.AddChild(label);
				row.AddChild(new Label { Text = $"×{n}", MouseFilter = MouseFilterEnum.Ignore });
				grid.AddChild(row);
			}
		}
	}

	private void RefreshMaps()
	{
		foreach (var child in _mapList.GetChildren()) child.QueueFree();
		var branch = (AtlasBranch)_branchFilter.Selected;
		var maps = Inv.Maps.Where(m => branch == AtlasBranch.None || m.Node.Branch == branch).ToList();
		if (maps.Count == 0)
		{
			_mapList.AddChild(new Label { Text = Inv.Maps.Count == 0 ? "Карт нет. Они падают в забегах и сохраняются только при победе." : "Нет карт этой ветки.", Modulate = new Color(1, 1, 1, 0.55f) });
			return;
		}
		foreach (var tierGroup in maps.GroupBy(m => m.Tier).OrderBy(g => g.Key))
		{
			_mapList.AddChild(new Label { Text = $"T{tierGroup.Key} · {tierGroup.Count()}", ThemeTypeVariation = "HeaderSmall" });
			foreach (var map in tierGroup.OrderBy(m => m.Node.Branch).ThenBy(m => m.Name).ThenByDescending(m => m.Mods.Count))
			{
				var row = new HBoxContainer();
				row.AddThemeConstantOverride("separation", 8);
				_mapList.AddChild(row);
				var text = new RichTextLabel
				{
					BbcodeEnabled = true,
					FitContent = true,
					ScrollActive = false,
					SizeFlagsHorizontal = SizeFlags.ExpandFill,
					Text = $"[color=#{ItemText.Hex(map.Color)}]{map.Name}[/color] [color=#888888]{ItemInstance.RarityName(map.Rarity)}[/color]"
						+ (map.Mods.Count > 0 ? $"\n[color=#aaaaaa]{string.Join(", ", map.Mods.Select(m => m.DisplayName))} · +{map.Quantity * 100:0}% кол. / +{map.RarityBonus * 100:0}% редк.[/color]" : ""),
				};
				row.AddChild(text);
				var captured = map;
				var craft = new Button { Text = "Крафт", CustomMinimumSize = new Vector2(80, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter };
				craft.Pressed += () => OpenCraft(captured);
				row.AddChild(craft);
				var device = new Button { Text = "В устройство", CustomMinimumSize = new Vector2(120, 32), SizeFlagsVertical = SizeFlags.ShrinkCenter };
				device.Pressed += () => MapToDevice?.Invoke(captured);
				row.AddChild(device);
			}
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
			$"Статусы: +{s.Get(StatType.AilmentChance) * 100:0.#}% шанс · ×{s.Get(StatType.AilmentDuration):0.##} длительность\n" +
			$"Скорость: {s.Get(StatType.MoveSpeed):0.##} м/с · Рывок: {s.Get(StatType.DashCharges):0}\n" +
			$"Снаряды осколка: {1 + s.Get(StatType.ShardProjectiles):0} · Кулдаун взрыва ×{s.Get(StatType.FireBlastCooldown):0.##}";
		s.Free();
	}

	// ---------- Надеть / снять / перетащить ----------

	public void ToggleEquip(ItemCell cell)
	{
		if (cell.Item == null) return;
		bool ok = cell.IsDoll ? Inv.Unequip(cell.DollSlot) : Inv.Equip(cell.StashIndex);
		if (!ok && cell.IsDoll) Say("Тайник полон", false);
		Changed();
	}

	public bool CanDrop(ItemCell target, Godot.Collections.Dictionary data)
	{
		if (MassMode) return false;
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

	// ---------- Контекстное меню ----------

	private const int MenuEquip = 0, MenuCraft = 1, MenuDisassemble = 2;

	public void ShowContextMenu(ItemCell cell, Vector2 at)
	{
		_menuCell = cell;
		_menu.Clear();
		_menu.AddItem(cell.IsDoll ? "Снять" : "Надеть", MenuEquip);
		_menu.AddItem("Крафт", MenuCraft);
		// Надетое разобрать нельзя.
		if (!cell.IsDoll) _menu.AddItem("Разобрать", MenuDisassemble);
		_menu.Position = (Vector2I)at;
		_menu.ResetSize();
		_menu.Popup();
	}

	private void OnMenu(long id)
	{
		var cell = _menuCell;
		if (cell?.Item == null) return;
		switch ((int)id)
		{
			case MenuEquip:
				ToggleEquip(cell);
				break;
			case MenuCraft:
				OpenCraft(cell.Item);
				break;
			case MenuDisassemble:
				AskDisassemble(cell.StashIndex);
				break;
		}
	}

	// ---------- Разборка ----------

	private string DisassembleHint =>
		$"За каждый аффикс — его осколки (T1-T2 по 1, T3-T4 по 2, T5 по 3), с шансом {Gs.Config.Loot.DisassembleLossChance * 100:0}% один осколок теряется. " +
		$"{Gs.Config.Loot.DisassembleGlyphRuneChance * 100:0}% шанс глифа или руны (у редких — дважды).";

	public void AskDisassemble(int index)
	{
		var item = Inv.Stash[index];
		if (item == null) return;
		_confirm.DialogText = $"Разобрать «{item.Name}» ({ItemInstance.RarityName(item.Rarity)}, аффиксов {item.Affixes.Count})?\n\n{DisassembleHint}";
		_pendingConfirm = () => Disassemble(new[] { index });
		_confirm.PopupCentered(new Vector2I(520, 0));
	}

	private void AskDisassembleSelected()
	{
		if (_selected.Count == 0) return;
		var indices = _selected.ToArray();
		_confirm.DialogText = $"Разобрать выбранные предметы ({indices.Length})?\n\n{DisassembleHint}";
		_pendingConfirm = () =>
		{
			Disassemble(indices);
			ExitMassMode();
		};
		_confirm.PopupCentered(new Vector2I(520, 0));
	}

	/// <summary>Разбирает предметы тайника по индексам; возвращает полученную валюту.</summary>
	public Dictionary<string, int> Disassemble(IEnumerable<int> indices)
	{
		var total = new Dictionary<string, int>();
		int count = 0;
		foreach (int i in indices.Distinct().ToList())
		{
			var item = Inv.Take(i);
			if (item == null) continue;
			count++;
			foreach (var (id, n) in Crafting.Disassemble(item, Gs.Generator, Gs.Items))
				total[id] = total.GetValueOrDefault(id) + n;
		}
		Inv.AddCurrency(total);
		_selected.Clear();
		var lines = ItemText.CurrencyLines(total, Gs.Items).ToList();
		Say(count == 0 ? "Нечего разбирать" : $"Разобрано: {count}. Получено: " + (lines.Count == 0 ? "ничего" : string.Join(", ", lines)), count > 0);
		Changed();
		return total;
	}

	public bool IsSelected(int index) => _selected.Contains(index);

	public void ToggleSelected(int index)
	{
		if (!MassMode || Inv.Stash[index] == null) return;
		if (!_selected.Remove(index)) _selected.Add(index);
		Refresh();
	}

	public void EnterMassMode()
	{
		MassMode = true;
		_selected.Clear();
		_massToggle.SetPressedNoSignal(true);
		ShowTab(TabItems);
		Say("Массовая разборка: кликните по предметам, затем «Разобрать выбранные». Esc — отмена.");
		Refresh();
	}

	public void ExitMassMode()
	{
		if (!MassMode && _selected.Count == 0) return;
		MassMode = false;
		_selected.Clear();
		_massToggle?.SetPressedNoSignal(false);
		Refresh();
	}
}
