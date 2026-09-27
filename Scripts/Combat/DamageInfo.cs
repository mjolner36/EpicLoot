using Godot;
using EpicLoot.Stats;

namespace EpicLoot.Combat;

public enum DamageType
{
	Physical,
	Fire,
	Cold,
	Lightning,
}

/// <summary>Урон одного удара после множителей атакующего и крита, до защиты цели.</summary>
public class DamageInfo
{
	public float[] Amounts = new float[4];
	public bool IsCrit;
	public StatBlock SourceStats;
	public Node3D SourceNode;
	public string[] SkillTags = System.Array.Empty<string>();
	public float IgniteChance;
	public float ChillChance;
	public float ShockChance;
	/// <summary>Разрешены ли вторичные эффекты (раскол, цепи) — false для самих вторичных ударов.</summary>
	public bool AllowSecondary = true;

	public float this[DamageType t]
	{
		get => Amounts[(int)t];
		set => Amounts[(int)t] = value;
	}

	public float Total => Amounts[0] + Amounts[1] + Amounts[2] + Amounts[3];

	public DamageInfo Clone()
	{
		var c = (DamageInfo)MemberwiseClone();
		c.Amounts = (float[])Amounts.Clone();
		return c;
	}

	public DamageInfo Scaled(float factor)
	{
		var c = Clone();
		for (int i = 0; i < 4; i++) c.Amounts[i] *= factor;
		return c;
	}

	public DamageType MainType
	{
		get
		{
			int best = 0;
			for (int i = 1; i < 4; i++) if (Amounts[i] > Amounts[best]) best = i;
			return (DamageType)best;
		}
	}

	public static Color ColorOf(DamageType t) => t switch
	{
		DamageType.Fire => new Color(1f, 0.55f, 0.15f),
		DamageType.Cold => new Color(0.5f, 0.8f, 1f),
		DamageType.Lightning => new Color(1f, 0.92f, 0.3f),
		_ => Colors.White,
	};
}
