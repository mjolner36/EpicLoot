using System.Collections.Generic;
using Godot;

namespace EpicLoot.Stats;

/// <summary>
/// Статы, модификаторы, теги и жизнь одной сущности (игрок или враг).
/// Final = (Base + ΣFlat) × (1 + ΣIncreased) × Π(1 + More)
/// </summary>
public partial class StatBlock : Node
{
	[Signal] public delegate void StatChangedEventHandler(StatType stat, float value);
	[Signal] public delegate void LifeChangedEventHandler(float current, float max);
	[Signal] public delegate void DiedEventHandler();
	[Signal] public delegate void TagsChangedEventHandler();

	private struct Entry
	{
		public StatType Stat;
		public ModifierType Type;
		public float Value;
		public string Source;
	}

	private readonly float[] _base = new float[StatDefaults.Count];
	private readonly float[] _cache = new float[StatDefaults.Count];
	private readonly bool[] _dirty = new bool[StatDefaults.Count];
	private readonly List<Entry> _mods = new();
	private readonly Dictionary<string, HashSet<string>> _tags = new();

	public float CurrentLife { get; private set; }
	public bool IsDead { get; private set; }

	public StatBlock()
	{
		for (int i = 0; i < _base.Length; i++)
		{
			_base[i] = StatDefaults.Get((StatType)i);
			_dirty[i] = true;
		}
	}

	public float Get(StatType stat)
	{
		int i = (int)stat;
		if (_dirty[i])
		{
			_cache[i] = Compute(stat);
			_dirty[i] = false;
		}
		return _cache[i];
	}

	public float GetBase(StatType stat) => _base[(int)stat];

	public void SetBase(StatType stat, float value)
	{
		_base[(int)stat] = value;
		MarkChanged(stat);
	}

	public void AddModifier(Modifier mod, string sourceOverride = null)
	{
		AddModifier(mod.Stat, mod.Type, mod.Value, sourceOverride ?? mod.SourceId);
	}

	public void AddModifier(StatType stat, ModifierType type, float value, string source)
	{
		_mods.Add(new Entry { Stat = stat, Type = type, Value = value, Source = source ?? "" });
		MarkChanged(stat);
	}

	public void AddModifiers(IEnumerable<Modifier> mods, string source)
	{
		if (mods == null) return;
		foreach (var m in mods)
			if (m != null) AddModifier(m, source);
	}

	/// <summary>Снимает все модификаторы и теги одного источника.</summary>
	public void RemoveBySource(string source)
	{
		for (int i = _mods.Count - 1; i >= 0; i--)
		{
			if (_mods[i].Source != source) continue;
			var stat = _mods[i].Stat;
			_mods.RemoveAt(i);
			MarkChanged(stat);
		}

		bool tagsChanged = false;
		var empty = new List<string>();
		foreach (var (tag, sources) in _tags)
		{
			if (sources.Remove(source)) tagsChanged = true;
			if (sources.Count == 0) empty.Add(tag);
		}
		foreach (var t in empty) _tags.Remove(t);
		if (tagsChanged) EmitSignal(SignalName.TagsChanged);
	}

	public void AddTag(string tag, string source)
	{
		if (!_tags.TryGetValue(tag, out var sources))
		{
			sources = new HashSet<string>();
			_tags[tag] = sources;
		}
		if (sources.Add(source)) EmitSignal(SignalName.TagsChanged);
	}

	public void RemoveTag(string tag, string source)
	{
		if (!_tags.TryGetValue(tag, out var sources) || !sources.Remove(source)) return;
		if (sources.Count == 0) _tags.Remove(tag);
		EmitSignal(SignalName.TagsChanged);
	}

	public bool HasTag(string tag) => _tags.ContainsKey(tag);

	public IEnumerable<string> AllTags => _tags.Keys;

	public void ResetLife()
	{
		IsDead = false;
		CurrentLife = Get(StatType.MaxLife);
		EmitSignal(SignalName.LifeChanged, CurrentLife, CurrentLife);
	}

	public void TakeDamage(float amount)
	{
		if (IsDead || amount <= 0f) return;
		CurrentLife = Mathf.Max(0f, CurrentLife - amount);
		EmitSignal(SignalName.LifeChanged, CurrentLife, Get(StatType.MaxLife));
		if (CurrentLife <= 0f)
		{
			IsDead = true;
			EmitSignal(SignalName.Died);
		}
	}

	private void MarkChanged(StatType stat)
	{
		int i = (int)stat;
		float old = _dirty[i] ? float.NaN : _cache[i];
		_dirty[i] = true;
		float now = Get(stat);
		if (stat == StatType.MaxLife && !IsDead)
			CurrentLife = Mathf.Min(CurrentLife, now);
		if (!Mathf.IsEqualApprox(old, now))
			EmitSignal(SignalName.StatChanged, (int)stat, now);
	}

	private float Compute(StatType stat)
	{
		float flat = 0f, inc = 0f, more = 1f;
		foreach (var m in _mods)
		{
			if (m.Stat != stat) continue;
			switch (m.Type)
			{
				case ModifierType.Flat: flat += m.Value; break;
				case ModifierType.Increased: inc += m.Value; break;
				case ModifierType.More: more *= 1f + m.Value; break;
			}
		}
		return Calculate(_base[(int)stat], flat, inc, more);
	}

	public static float Calculate(float baseValue, float flat, float increased, float moreProduct)
	{
		return (baseValue + flat) * (1f + increased) * moreProduct;
	}
}
