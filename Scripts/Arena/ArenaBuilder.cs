using System.Collections.Generic;
using Godot;
using EpicLoot.Combat;

namespace EpicLoot.Arena;

/// <summary>
/// Собирает арену: пол, стены, 6-10 препятствий из пула по сетке с проверкой проходимости,
/// затем запекает навмеш в рантайме.
/// </summary>
public static class ArenaBuilder
{
	private enum ObstacleKind
	{
		Column,
		Rock,
		Gravestone,
	}

	private const float Cell = 4f;

	public static NavigationRegion3D Build(Node3D parent, float size, int minObstacles, int maxObstacles, Vector3[] keepClear, RandomNumberGenerator rng)
	{
		var region = new NavigationRegion3D
		{
			Name = "NavigationRegion3D",
			NavigationMesh = new NavigationMesh
			{
				GeometryParsedGeometryType = NavigationMesh.ParsedGeometryType.StaticColliders,
				GeometryCollisionMask = Layers.World,
				AgentRadius = 0.5f,
				AgentHeight = 2.0f,
				AgentMaxClimb = 0.25f,
			},
		};
		parent.AddChild(region);

		float half = size * 0.5f;
		AddBox(region, new Vector3(0, -0.5f, 0), new Vector3(size, 1f, size), new Color(0.32f, 0.31f, 0.3f), "Floor");
		var wallColor = new Color(0.22f, 0.21f, 0.2f);
		AddBox(region, new Vector3(0, 1f, -half - 0.5f), new Vector3(size + 2f, 2f, 1f), wallColor, "WallN");
		AddBox(region, new Vector3(0, 1f, half + 0.5f), new Vector3(size + 2f, 2f, 1f), wallColor, "WallS");
		AddBox(region, new Vector3(-half - 0.5f, 1f, 0), new Vector3(1f, 2f, size), wallColor, "WallW");
		AddBox(region, new Vector3(half + 0.5f, 1f, 0), new Vector3(1f, 2f, size), wallColor, "WallE");

		PlaceObstacles(region, size, minObstacles, maxObstacles, keepClear, rng);
		region.BakeNavigationMesh(false);
		return region;
	}

	private static void PlaceObstacles(Node3D parent, float size, int min, int max, Vector3[] keepClear, RandomNumberGenerator rng)
	{
		int cells = Mathf.FloorToInt(size / Cell);
		float half = size * 0.5f;
		var candidates = new List<Vector2I>();
		for (int x = 1; x < cells - 1; x++)
		for (int z = 1; z < cells - 1; z++)
		{
			var c = CellCenter(x, z, half);
			bool blocked = false;
			foreach (var p in keepClear)
				if (new Vector2(p.X, p.Z).DistanceTo(new Vector2(c.X, c.Z)) < Cell * 1.2f) blocked = true;
			if (!blocked) candidates.Add(new Vector2I(x, z));
		}

		int count = rng.RandiRange(min, max);
		for (int attempt = 0; attempt < 30; attempt++)
		{
			Shuffle(candidates, rng);
			var chosen = candidates.GetRange(0, Mathf.Min(count, candidates.Count));
			var placed = new List<(Vector3 pos, Vector3 size, ObstacleKind kind, float rot)>();
			foreach (var cell in chosen)
			{
				var kind = (ObstacleKind)rng.RandiRange(0, 2);
				var c = CellCenter(cell.X, cell.Y, half) + new Vector3(rng.RandfRange(-0.8f, 0.8f), 0, rng.RandfRange(-0.8f, 0.8f));
				placed.Add((c, SizeOf(kind), kind, rng.RandfRange(0f, Mathf.Tau)));
			}
			if (!IsPassable(placed, size, keepClear)) continue;
			foreach (var o in placed) AddObstacle(parent, o.pos, o.size, o.kind, o.rot);
			return;
		}
		GD.PushWarning("ArenaBuilder: не удалось расставить препятствия с проходами, арена пустая");
	}

	private static Vector3 CellCenter(int x, int z, float half) => new(-half + (x + 0.5f) * Cell, 0, -half + (z + 0.5f) * Cell);

	private static Vector3 SizeOf(ObstacleKind kind) => kind switch
	{
		ObstacleKind.Column => new Vector3(1.2f, 3.5f, 1.2f),
		ObstacleKind.Rock => new Vector3(2.2f, 1.4f, 1.8f),
		_ => new Vector3(1.0f, 1.5f, 0.35f),
	};

	/// <summary>Проверка проходимости: BFS по сетке 1 м от первой точки до всех остальных и до всех свободных клеток.</summary>
	private static bool IsPassable(List<(Vector3 pos, Vector3 size, ObstacleKind kind, float rot)> placed, float size, Vector3[] keepClear)
	{
		int n = Mathf.FloorToInt(size);
		float half = size * 0.5f;
		var blocked = new bool[n, n];
		const float agent = 0.6f;
		for (int x = 0; x < n; x++)
		for (int z = 0; z < n; z++)
		{
			var p = new Vector2(-half + x + 0.5f, -half + z + 0.5f);
			foreach (var o in placed)
			{
				float r = Mathf.Max(o.size.X, o.size.Z) * 0.5f + agent;
				if (p.DistanceTo(new Vector2(o.pos.X, o.pos.Z)) < r) blocked[x, z] = true;
			}
		}

		Vector2I ToCell(Vector3 v) => new(Mathf.Clamp(Mathf.FloorToInt(v.X + half), 0, n - 1), Mathf.Clamp(Mathf.FloorToInt(v.Z + half), 0, n - 1));
		var start = ToCell(keepClear[0]);
		if (blocked[start.X, start.Y]) return false;
		var seen = new bool[n, n];
		var queue = new Queue<Vector2I>();
		queue.Enqueue(start);
		seen[start.X, start.Y] = true;
		int reached = 1;
		var dirs = new[] { Vector2I.Left, Vector2I.Right, Vector2I.Up, Vector2I.Down };
		while (queue.Count > 0)
		{
			var c = queue.Dequeue();
			foreach (var d in dirs)
			{
				var nb = c + d;
				if (nb.X < 0 || nb.Y < 0 || nb.X >= n || nb.Y >= n || seen[nb.X, nb.Y] || blocked[nb.X, nb.Y]) continue;
				seen[nb.X, nb.Y] = true;
				reached++;
				queue.Enqueue(nb);
			}
		}

		int free = 0;
		foreach (var b in blocked) if (!b) free++;
		foreach (var p in keepClear)
		{
			var c = ToCell(p);
			if (!seen[c.X, c.Y]) return false;
		}
		return reached == free;
	}

	private static void AddObstacle(Node3D parent, Vector3 pos, Vector3 size, ObstacleKind kind, float rot)
	{
		var body = new StaticBody3D { Name = kind.ToString(), CollisionLayer = Layers.World, CollisionMask = 0 };
		parent.AddChild(body);
		body.Position = new Vector3(pos.X, 0, pos.Z);
		body.Rotation = new Vector3(0, kind == ObstacleKind.Column ? 0 : rot, 0);

		Mesh mesh;
		Shape3D shape;
		Color color;
		switch (kind)
		{
			case ObstacleKind.Column:
				mesh = new CylinderMesh { TopRadius = size.X * 0.5f, BottomRadius = size.X * 0.55f, Height = size.Y, RadialSegments = 10 };
				shape = new CylinderShape3D { Radius = size.X * 0.55f, Height = size.Y };
				color = new Color(0.55f, 0.53f, 0.5f);
				break;
			case ObstacleKind.Rock:
				mesh = new BoxMesh { Size = size };
				shape = new BoxShape3D { Size = size };
				color = new Color(0.42f, 0.4f, 0.38f);
				break;
			default:
				mesh = new BoxMesh { Size = size };
				shape = new BoxShape3D { Size = size };
				color = new Color(0.5f, 0.5f, 0.55f);
				break;
		}
		body.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f }, Position = new Vector3(0, size.Y * 0.5f, 0) });
		body.AddChild(new CollisionShape3D { Shape = shape, Position = new Vector3(0, size.Y * 0.5f, 0) });
	}

	private static void AddBox(Node3D parent, Vector3 pos, Vector3 size, Color color, string name)
	{
		var body = new StaticBody3D { Name = name, CollisionLayer = Layers.World, CollisionMask = 0, Position = pos };
		parent.AddChild(body);
		body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Roughness = 1f } });
		body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
	}

	private static void Shuffle<T>(List<T> list, RandomNumberGenerator rng)
	{
		for (int i = list.Count - 1; i > 0; i--)
		{
			int j = rng.RandiRange(0, i);
			(list[i], list[j]) = (list[j], list[i]);
		}
	}
}
