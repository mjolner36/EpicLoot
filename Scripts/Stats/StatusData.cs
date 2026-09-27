using Godot;
using EpicLoot.Combat;

namespace EpicLoot.Stats;

[GlobalClass]
public partial class StatusData : Resource
{
	[Export] public string Id { get; set; } = "";
	[Export] public string DisplayName { get; set; } = "";
	[Export] public string Tag { get; set; } = "";
	[Export] public float Duration { get; set; } = 1f;
	/// <summary>Стат атакующего, дополнительно умножающий длительность (например IgniteDuration).</summary>
	[Export] public StatType DurationStat { get; set; } = StatType.None;
	[Export] public float TickInterval { get; set; } = 0.5f;
	/// <summary>Доля урона удара, наносимая в секунду (Поджог).</summary>
	[Export] public float DamagePerSecondFraction { get; set; }
	[Export] public DamageType DamageType { get; set; } = DamageType.Physical;
	/// <summary>Стаки. 0 или 1 = без стаков.</summary>
	[Export] public int MaxStacks { get; set; } = 1;
	[Export] public Modifier[] Modifiers { get; set; } = System.Array.Empty<Modifier>();
	[Export] public Color OverlayColor { get; set; } = Colors.White;
	[Export] public string IconText { get; set; } = "";
	/// <summary>Иконка 20×20 из общего атласа иконок.</summary>
	[Export] public Texture2D Icon { get; set; }
	/// <summary>Всплывающий текст при наложении (пусто — без текста).</summary>
	[Export] public string ApplyText { get; set; } = "";
	/// <summary>Эффект оверлея: 0 — ровный, 1 — мерцание, 2 — вспышки.</summary>
	[Export] public int OverlayEffect { get; set; }
	[Export] public int OverlayPriority { get; set; }
}
