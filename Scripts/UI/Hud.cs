using Godot;
using EpicLoot.Core;
using EpicLoot.Enemies;
using EpicLoot.Items;
using EpicLoot.Player;
using EpicLoot.Skills;
using EpicLoot.Stats;

namespace EpicLoot.UI;

/// <summary>HUD: жизнь, умения с кулдаунами, заряды dash, волна и счётчик врагов, полоса элиты, FPS.</summary>
public partial class Hud : CanvasLayer
{
	private PlayerController _player;
	private WaveSpawner _spawner;
	private ProgressBar _life;
	private Label _lifeText;
	private Label _wave;
	private Label _fps;
	private Label _dash;
	private readonly SkillSlotView[] _slots = new SkillSlotView[4];
	private VBoxContainer _eliteBox;
	private ProgressBar _eliteBar;
	private EnemyBase _elite;
	private string _waveText = "";
	private RunBag _bag;
	private Label _bagLabel;
	private PanelContainer _bagPanel;
	private RichTextLabel _bagText;

	public string BagCounterText => _bagLabel?.Text ?? "";
	public bool BagPanelVisible => _bagPanel?.Visible ?? false;

	public void Init(PlayerController player, WaveSpawner spawner, RunBag bag, string title)
	{
		_player = player;
		_spawner = spawner;
		_bag = bag;

		var root = new Control();
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		root.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(root);

		// Жизнь — слева сверху.
		var lifeBox = new VBoxContainer { Position = new Vector2(20, 16) };
		root.AddChild(lifeBox);
		lifeBox.AddChild(new Label { Text = title, ThemeTypeVariation = "HeaderSmall" });
		_life = new ProgressBar { CustomMinimumSize = new Vector2(320, 26), ShowPercentage = false };
		_life.AddThemeStyleboxOverride("fill", Box(new Color(0.75f, 0.15f, 0.15f)));
		lifeBox.AddChild(_life);
		_lifeText = new Label();
		_life.AddChild(_lifeText);
		_lifeText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_lifeText.HorizontalAlignment = HorizontalAlignment.Center;
		_lifeText.VerticalAlignment = VerticalAlignment.Center;

		// Волна — по центру сверху, под ней полоса элиты.
		var top = new VBoxContainer();
		top.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
		top.Position = new Vector2(-200, 12);
		top.CustomMinimumSize = new Vector2(400, 0);
		root.AddChild(top);
		_wave = new Label { HorizontalAlignment = HorizontalAlignment.Center, ThemeTypeVariation = "HeaderSmall" };
		top.AddChild(_wave);
		_eliteBox = new VBoxContainer { Visible = false };
		top.AddChild(_eliteBox);
		_eliteBox.AddChild(new Label { Text = "Элитный рыцарь", HorizontalAlignment = HorizontalAlignment.Center });
		_eliteBar = new ProgressBar { CustomMinimumSize = new Vector2(400, 18), ShowPercentage = false };
		_eliteBar.AddThemeStyleboxOverride("fill", Box(new Color(0.55f, 0.1f, 0.1f)));
		_eliteBox.AddChild(_eliteBar);

		_fps = new Label();
		_fps.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		_fps.Position = new Vector2(-140, 12);
		root.AddChild(_fps);

		// Умения — снизу по центру.
		var bar = new HBoxContainer();
		bar.AddThemeConstantOverride("separation", 10);
		bar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
		bar.Position = new Vector2(-183, -134);
		root.AddChild(bar);
		for (int i = 0; i < 3; i++)
		{
			var skill = i < player.Skills.Slots.Length ? player.Skills.Slots[i] : null;
			_slots[i] = new SkillSlotView(skill);
			bar.AddChild(_slots[i]);
		}
		_slots[3] = new SkillSlotView(player.Dash.Data);
		bar.AddChild(_slots[3]);
		_dash = new Label { HorizontalAlignment = HorizontalAlignment.Center };
		_slots[3].AddChild(_dash);
		_dash.Position = new Vector2(0, -26);
		_dash.Size = new Vector2(84, 24);

		// Сумка забега: счётчик под жизнью, TAB — содержимое.
		_bagLabel = new Label { Visible = bag != null };
		lifeBox.AddChild(_bagLabel);
		_bagPanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(360, 0), Position = new Vector2(20, 130) };
		root.AddChild(_bagPanel);
		_bagText = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(330, 0) };
		_bagPanel.AddChild(_bagText);
		if (bag != null)
		{
			bag.Changed += RefreshBag;
			RefreshBag();
		}

		var help = new Label { Text = "WASD — движение · ЛКМ/ПКМ/Q — умения · Пробел — рывок · ЛКМ/касание — подобрать · TAB — сумка · Esc — сдаться · F9 — +60 врагов", Modulate = new Color(1, 1, 1, 0.5f) };
		help.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		help.Position = new Vector2(20, -30);
		help.AddThemeFontSizeOverride("font_size", 14);
		root.AddChild(help);

		EventBus.Instance.WaveStarted += OnWaveStarted;
		EventBus.Instance.EliteSpawned += OnEliteSpawned;

		// Жизнь обновляется по сигналам StatBlock, а не опросом.
		player.Stats.LifeChanged += OnLifeChanged;
		player.Stats.StatChanged += (stat, _) =>
		{
			if (stat == StatType.MaxLife) OnLifeChanged(_player.Stats.CurrentLife, _player.Stats.Get(StatType.MaxLife));
		};
		OnLifeChanged(player.Stats.CurrentLife, player.Stats.Get(StatType.MaxLife));
	}

	private void OnLifeChanged(float current, float max)
	{
		_life.MaxValue = max;
		_life.Value = current;
		_lifeText.Text = $"{Mathf.CeilToInt(current)} / {Mathf.RoundToInt(max)}";
	}

	private void RefreshBag()
	{
		if (_bag == null) return;
		_bagLabel.Text = $"Сумка {_bag.Items.Count}/{_bag.Capacity}" + (_bag.OrbTotal > 0 ? $" · Сферы {_bag.OrbTotal}" : "") + (_bag.IsFull ? " · полна" : "");
		var text = $"[b]Сумка забега {_bag.Items.Count}/{_bag.Capacity}[/b]\n";
		if (_bag.Items.Count == 0 && _bag.OrbTotal == 0) text += "[color=#888888]пусто[/color]\n";
		foreach (var item in _bag.Items)
			text += $"[color=#{ItemText.Hex(item.Color)}]{item.Name}[/color] [color=#888888]ур. {item.Level}[/color]\n";
		foreach (var (id, n) in _bag.Orbs)
			text += $"{GameState.Instance.Items.Orb(id)?.DisplayName ?? id} ×{n}\n";
		text += "\n[color=#888888]Лут переходит в тайник только при победе.[/color]";
		_bagText.Text = text;
	}

	public override void _ExitTree()
	{
		if (_bag != null) _bag.Changed -= RefreshBag;
		EventBus.Instance.WaveStarted -= OnWaveStarted;
		EventBus.Instance.EliteSpawned -= OnEliteSpawned;
	}

	private void OnWaveStarted(int wave, int total)
	{
		_waveText = wave > total ? "Элита" : $"Волна {wave} / {total}";
	}

	private void OnEliteSpawned(Node3D enemy)
	{
		_elite = enemy as EnemyBase;
		_eliteBox.Visible = true;
	}

	public override void _Process(double delta)
	{
		if (_player == null || !IsInstanceValid(_player)) return;
		_wave.Text = (_waveText.Length > 0 ? _waveText + " · " : "") + $"Врагов: {EnemyBase.Alive.Count}";
		_fps.Text = $"FPS {Engine.GetFramesPerSecond()}";
		if (_bag != null) _bagPanel.Visible = Input.IsActionPressed(InputSetup.ShowBag);

		for (int i = 0; i < 3; i++)
			_slots[i].SetCooldown(_player.Skills.CooldownRemaining(i), _player.Skills.CooldownFraction(i));
		var dash = _player.Dash;
		_slots[3].SetCooldown(dash.Charges > 0 ? 0f : dash.RechargeFraction * (dash.Data?.Cooldown ?? 0f), dash.Charges > 0 ? 0f : dash.RechargeFraction);
		_dash.Text = $"{dash.Charges}/{dash.MaxCharges}";

		if (_elite != null)
		{
			if (!IsInstanceValid(_elite) || _elite.IsDead)
			{
				_eliteBox.Visible = false;
				_elite = null;
			}
			else
			{
				_eliteBar.MaxValue = _elite.Stats.Get(StatType.MaxLife);
				_eliteBar.Value = _elite.Stats.CurrentLife;
			}
		}
	}

	public static StyleBoxFlat Box(Color c) => new() { BgColor = c, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3 };
}
