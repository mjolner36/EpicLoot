// Оверлей статуса на модели. Один материал на статус для всех врагов (не ломает батчинг),
// мерцание и вспышки считаются здесь по TIME, фаза — от позиции объекта.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, shadows_disabled;

uniform vec4 color : source_color = vec4(1.0, 1.0, 1.0, 0.5);
// 0 — ровный, 1 — мерцание (Поджог), 2 — вспышки (Шок)
uniform int mode = 0;

void fragment() {
	float phase = dot(NODE_POSITION_WORLD, vec3(1.37, 0.0, 2.11));
	float a = color.a;
	if (mode == 1) {
		a = 0.38 + 0.2 * sin(TIME * 18.0 + phase);
	} else if (mode == 2) {
		a = mod(TIME + phase, 0.5) < 0.1 ? 0.8 : 0.2;
	}
	ALBEDO = color.rgb;
	ALPHA = a;
}
