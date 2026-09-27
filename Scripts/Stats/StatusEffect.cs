namespace EpicLoot.Stats;

/// <summary>Активный экземпляр статуса на сущности.</summary>
public class StatusEffect
{
	public StatusData Data;
	public float Remaining;
	/// <summary>Полная длительность текущего наложения (для кругового таймера).</summary>
	public float Duration;
	public int Stacks;
	public float TickTimer;
	public float DamagePerSecond;
	/// <summary>Накопленный урон тиков для одного числа над целью (раз в секунду).</summary>
	public float PendingNumber;
	public float NumberTimer;
	public StatBlock SourceStats;

	public string SourceId => "status:" + Data.Id;
}
