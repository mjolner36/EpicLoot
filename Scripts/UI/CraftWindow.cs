using System.Collections.Generic;
using System.Linq;
using Godot;
using EpicLoot.Core;
using EpicLoot.Items;

namespace EpicLoot.UI;

/// <summary>
/// Окно крафта в стиле Last Epoch: слева предмет (или карта), справа его аффиксы с кнопкой "+" (если есть осколок),
/// ниже слоты глифа и руны и кнопка "Ковать". Валюта списывается только при успешной ковке.
/// </summary>
public partial class CraftWindow : Control
{
	public object Target { get; private set; }
	public CraftCurrencyData Shard { get; private set; }
	public CraftCurrencyData Glyph { get; private set; }
	public CraftCurrencyData Rune { get; private set; }
	public string LastMessage => _result.Text;

	private PanelContainer _panel;
	private RichTextLabel _desc;
	private VBoxContainer _rows;
	private Label _potential;
	private OptionButton _glyphPick;
	private OptionButton _runePick;
	private Label _preview;
	private Button _forge;
	private Label _result;
	private readonly List<CraftCurrencyData> _glyphOptions = new();
	private readonly List<CraftCurrencyData> _runeOptions = new();

	private GameState Gs => GameState.Instance;
	private Inventory Inv => Gs.Profile;
	private ItemInstance TargetItem => Target as ItemInstance;
	private MapInstance TargetMap => Target as MapInstance;

	public override void _Ready()
	{
		SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		Visible = false;

		var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Ignore };
		dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(dim);
		var center = new CenterContainer();
		center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		AddChild(center);
		_panel = new PanelContainer();
		center.AddChild(_panel);
		var outer = new VBoxContainer();
		outer.AddThemeConstantOverride("separation", 10);
		_panel.AddChild(outer);

		var header = new HBoxContainer();
		outer.AddChild(header);
		header.AddChild(new Label { Text = "Крафт", ThemeTypeVariation = "HeaderLarge", SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var close = new Button { Text = "Закрыть" };
		close.Pressed += Close;
		header.AddChild(close);

		var body = new HBoxContainer();
		body.AddThemeConstantOverride("separation", 20);
		outer.AddChild(body);

		// Слева — предмет.
		var left = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
		body.AddChild(left);
		_desc = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(360, 0) };
		left.AddChild(_desc);

		// Справа — аффиксы, глиф, руна, ковка.
		var right = new VBoxContainer { CustomMinimumSize = new Vector2(430, 0) };
		right.AddThemeConstantOverride("separation", 8);
		body.AddChild(right);
		_potential = new Label();
		right.AddChild(_potential);
		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 250), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		right.AddChild(scroll);
		_rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_rows.AddThemeConstantOverride("separation", 4);
		scroll.AddChild(_rows);

		var slots = new GridContainer { Columns = 2 };
		slots.AddThemeConstantOverride("h_separation", 10);
		right.AddChild(slots);
		slots.AddChild(new Label { Text = "Глиф" });
		_glyphPick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_glyphPick.ItemSelected += i => SelectGlyph(i <= 0 ? null : _glyphOptions[(int)i - 1]);
		slots.AddChild(_glyphPick);
		slots.AddChild(new Label { Text = "Руна" });
		_runePick = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
		_runePick.ItemSelected += i => SelectRune(i <= 0 ? null : _runeOptions[(int)i - 1]);
		slots.AddChild(_runePick);
		foreach (var pick in new[] { _glyphPick, _runePick })
		{
			pick.AddThemeConstantOverride("icon_max_width", 20);
			pick.GetPopup().AddThemeConstantOverride("icon_max_width", 20);
		}

		_preview = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0), Modulate = new Color(1, 1, 1, 0.75f) };
		right.AddChild(_preview);
		_forge = new Button { Text = "Ковать", CustomMinimumSize = new Vector2(0, 44) };
		_forge.Pressed += () => Forge();
		right.AddChild(_forge);
		_result = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 26) };
		right.AddChild(_result);
	}

	public void Open(object target)
	{
		if (target is not (ItemInstance or MapInstance)) return;
		Target = target;
		Shard = Glyph = Rune = null;
		_result.Text = "";
		Visible = true;
		Refresh();
	}

	public void Close()
	{
		Visible = false;
		Target = null;
		GetParentOrNull<InventoryScreen>()?.Refresh();
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (Visible && e.IsActionPressed(InputSetup.Abandon))
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	// ---------- Выбор валюты ----------

	/// <summary>Осколок и руна взаимоисключающие: выбор одного снимает другое.</summary>
	public void SelectShard(CraftCurrencyData shard)
	{
		Shard = shard == Shard ? null : shard;
		if (Shard != null) Rune = null;
		Refresh();
	}

	public void SelectGlyph(CraftCurrencyData glyph)
	{
		Glyph = glyph;
		// На предметах глиф идёт только с осколком; на картах — Глиф хаоса с Руной перековки.
		if (Glyph != null && TargetItem != null) Rune = null;
		Refresh();
	}

	public void SelectRune(CraftCurrencyData rune)
	{
		Rune = rune;
		if (Rune != null)
		{
			Shard = null;
			if (TargetItem != null) Glyph = null;
		}
		Refresh();
	}

	// ---------- Ковка ----------

	public ForgeResult Forge()
	{
		if (Target == null) return ForgeResult.Fail("Нет цели");
		foreach (var c in new[] { Shard, Glyph, Rune })
			if (c != null && Inv.CurrencyCount(c) <= 0)
			{
				Show(ForgeResult.Fail($"Нет: {c.DisplayName}"));
				return ForgeResult.Fail($"Нет: {c.DisplayName}");
			}
		var res = Crafting.Forge(Target, Shard, Glyph, Rune, Gs.Generator, Gs.Items);
		if (res.Ok)
		{
			foreach (var c in res.Consumed) Inv.AddCurrency(c.Id, -1);
			Inv.AddCurrency(res.Returned);
			if (Shard != null && Inv.CurrencyCount(Shard) <= 0) Shard = null;
			if (Glyph != null && Inv.CurrencyCount(Glyph) <= 0) Glyph = null;
			if (Rune != null && Inv.CurrencyCount(Rune) <= 0) Rune = null;
			Inv.NotifyChanged();
			Gs.SaveGame();
			Flash();
		}
		Show(res);
		Refresh();
		return res;
	}

	private void Show(ForgeResult res)
	{
		_result.Text = res.Message;
		_result.Modulate = res.Ok ? new Color(0.7f, 1f, 0.7f) : new Color(1f, 0.6f, 0.5f);
	}

	/// <summary>Короткая анимация успешной ковки: вспышка панели и "подпрыгивание" строки результата.</summary>
	private void Flash()
	{
		_panel.Modulate = new Color(1.6f, 1.5f, 1.2f);
		var t = CreateTween();
		t.TweenProperty(_panel, "modulate", Colors.White, 0.35f);
		_result.PivotOffset = new Vector2(0, 13);
		_result.Scale = new Vector2(1.15f, 1.15f);
		CreateTween().TweenProperty(_result, "scale", Vector2.One, 0.25f);
	}

	// ---------- Отрисовка ----------

	public void Refresh()
	{
		if (Target == null) return;
		foreach (var child in _rows.GetChildren()) child.QueueFree();
		var cfg = Gs.Config.Loot;
		var (min, max) = Crafting.CostRange(cfg);

		if (TargetItem is { } item)
		{
			_desc.Text = ItemText.Describe(item);
			_potential.Text = $"Потенциал ковки: {item.Potential} · осколок тратит {min}-{max} ПК";
			_potential.Modulate = item.Potential > 0 ? Colors.White : new Color(1f, 0.55f, 0.5f);

			Header($"Префиксы {item.Prefixes}/{cfg.MaxPrefixes} · суффиксы {item.Suffixes}/{cfg.MaxSuffixes}");
			foreach (var roll in item.Affixes.OrderBy(a => !a.Affix.IsPrefix))
			{
				var shard = Gs.Items.ShardFor(roll.Affix);
				int n = Inv.CurrencyCount(shard);
				AffixRow($"{(roll.Affix.IsPrefix ? "П" : "С")} {ItemText.AffixFull(roll)}", ItemText.TierColor(roll.Tier), shard, n,
					roll.Tier < cfg.MaxTier && item.Potential > 0, roll.Tier >= cfg.MaxTier ? "максимальный тир" : null);
			}
			var addable = Gs.Items.Shards.Where(s => s.Affix.Allows(item.Slot) && !item.HasAffix(s.Affix) && Inv.CurrencyCount(s) > 0).ToList();
			Header(addable.Count > 0 ? "Добавить аффикс (осколок)" : "Нет осколков для новых аффиксов этого слота");
			foreach (var s in addable)
			{
				bool free = Gs.Generator.HasFreeSlot(item, s.Affix);
				AffixRow($"{(s.Affix.IsPrefix ? "П" : "С")} {s.Affix.DisplayName} → T1 ({ItemText.TierRange(s.Affix, 1)})", "cccccc", s, Inv.CurrencyCount(s),
					free && item.Potential > 0, free ? null : "нет свободного слота");
			}
		}
		else if (TargetMap is { } map)
		{
			_desc.Text = ItemText.DescribeMap(map);
			_potential.Text = "Карты меняются только рунами и Глифом хаоса";
			_potential.Modulate = Colors.White;
			Header("Руна открытия — моды на пустую карту · Руна удаления — убрать мод · Глиф хаоса + Руна перековки — заменить мод");
		}

		FillPick(_glyphPick, _glyphOptions, Gs.Items.GlyphsAndRunes.Where(c => c.Kind == CurrencyKind.Glyph && (TargetItem != null || c.Action == CurrencyAction.GlyphChaos)), Glyph);
		FillPick(_runePick, _runeOptions, Gs.Items.GlyphsAndRunes.Where(c => c.Kind == CurrencyKind.Rune), Rune);
		_preview.Text = PreviewText();
		_forge.Disabled = Shard == null && Rune == null;
	}

	private string PreviewText()
	{
		var parts = new List<string>();
		if (Shard != null) parts.Add(Shard.DisplayName);
		if (Glyph != null) parts.Add(Glyph.DisplayName);
		if (Rune != null) parts.Add(Rune.DisplayName);
		if (parts.Count == 0) return TargetItem != null ? "Нажмите «+» у аффикса или выберите руну." : "Выберите руну.";
		var text = "Будет использовано: " + string.Join(" + ", parts);
		if (Shard != null && TargetItem != null)
		{
			var (min, max) = Crafting.CostRange(Gs.Config.Loot);
			text += Glyph?.Action == CurrencyAction.GlyphHope
				? $". Трата {min}-{max} ПК, {Gs.Config.Loot.HopeChance * 100:0}% шанс не потратить"
				: $". Трата {min}-{max} ПК";
		}
		return text;
	}

	private void Header(string text)
	{
		var l = new Label { Text = text, Modulate = new Color(1, 1, 1, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(400, 0) };
		l.AddThemeFontSizeOverride("font_size", 14);
		_rows.AddChild(l);
	}

	private void AffixRow(string text, string colorHex, CraftCurrencyData shard, int count, bool canUse, string reason)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		_rows.AddChild(row);
		var label = new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill, Modulate = Color.FromHtml(colorHex), ClipText = true };
		row.AddChild(label);
		row.AddChild(new Label { Text = $"×{count}", Modulate = new Color(1, 1, 1, count > 0 ? 0.8f : 0.35f), CustomMinimumSize = new Vector2(40, 0) });
		var plus = new Button
		{
			Text = "+",
			ToggleMode = true,
			ButtonPressed = shard != null && shard == Shard,
			CustomMinimumSize = new Vector2(36, 30),
			Disabled = shard == null || count <= 0 || !canUse,
			TooltipText = reason ?? (count > 0 ? shard?.Description ?? "" : "Нет осколка этого аффикса"),
		};
		var captured = shard;
		plus.Pressed += () => SelectShard(captured);
		row.AddChild(plus);
	}

	private void FillPick(OptionButton pick, List<CraftCurrencyData> options, IEnumerable<CraftCurrencyData> pool, CraftCurrencyData selected)
	{
		pick.Clear();
		options.Clear();
		pick.AddItem("— нет —");
		foreach (var c in pool)
		{
			options.Add(c);
			int n = Inv.CurrencyCount(c);
			pick.AddIconItem(c.Icon, $"{c.DisplayName} ×{n}");
			pick.SetItemDisabled(options.Count, n <= 0);
			pick.SetItemTooltip(options.Count, c.Description);
		}
		pick.Selected = selected == null ? 0 : options.IndexOf(selected) + 1;
	}
}
