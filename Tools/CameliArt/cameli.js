// Cameli's drawing (decided 2026-10-17, artifact "Cameli Garden 아트 시안", candidate 2 "반반"), shared by cameli.html (the
// layer PNGs CameliFigure composites) and icon.html (the app icon).
//
// Units: a 100 x 100 square, y down, facing right. The drawing is made at a larger size and then scaled by FIT into the
// square (the big tail needs the room); outlines are widened by 1 / FIT so they stay OUTLINE wide in the square.
const CameliArt = (() => {
  const INK = "#1E2B25";      // Theme.Ink
  const OUTLINE = 4.5;        // outline width in the square
  const FIT = { x: 10, y: 0.5, s: 0.88 };

  // Body, casque and the two half-moon feet share one silhouette (one outline around all of them).
  const BODY = "M92 60 C 92 50, 86 43, 76 41 C 66 37, 50 36, 38 42 C 30 46, 25 54, 25 62 C 25 70, 32 75, 42 75 L 76 74 C 86 73, 92 68, 92 60 Z";
  const CASQUE = "M59 40 C 60 33, 63 28, 67 27 C 73 28, 79 34, 83 44 Z";
  const FEET = [[42, 75], [72, 74]], FOOT_R = 7.5;
  // A thin line above the eye, the mouth, and the eye.
  const RIDGE = "M59 45 Q 68 41 77 42.5";
  const MOUTH = "M93 62 Q 88 67 79 65";
  const EYE = { x: 81, y: 53, r: 7.5 };
  // The tail: a thick stem out of the rump, then one fat curl down and under, toward the belly.
  const TAIL = { P0: [36, 62], C: [15, 72], R: 17, a0: -75, dir: -1, turns: 0.85, shrink: 0.58, w0: 20, wc: 14, w1: 7 };

  function tailPath(t) {
    const a0 = t.a0 * Math.PI / 180, dir = t.dir;
    const S0 = [t.C[0] + t.R * Math.cos(a0), t.C[1] + t.R * Math.sin(a0)];
    const tan = [-dir * Math.sin(a0), dir * Math.cos(a0)];
    const ctrl = [S0[0] - tan[0] * 12, S0[1] - tan[1] * 12];
    const pts = [];
    for (let i = 0; i <= 12; i++) {
      const u = i / 12, v = 1 - u;
      pts.push([v * v * t.P0[0] + 2 * v * u * ctrl[0] + u * u * S0[0], v * v * t.P0[1] + 2 * v * u * ctrl[1] + u * u * S0[1]]);
    }
    const N = 110;
    for (let i = 1; i <= N; i++) {
      const u = i / N, a = a0 + dir * u * t.turns * 2 * Math.PI, r = t.R * (1 - t.shrink * u);
      pts.push([t.C[0] + r * Math.cos(a), t.C[1] + r * Math.sin(a)]);
    }
    const left = [], right = [], n = pts.length;
    for (let i = 0; i < n; i++) {
      const p = pts[i], q = pts[Math.min(i + 1, n - 1)], o = pts[Math.max(i - 1, 0)];
      let dx = q[0] - o[0], dy = q[1] - o[1];
      const len = Math.hypot(dx, dy) || 1; dx /= len; dy /= len;
      // Thick stem narrowing to wc where the curl starts, then thinning to w1.
      const w = (i <= 12 ? t.w0 + (t.wc - t.w0) * (i / 12) : t.w1 + (t.wc - t.w1) * (1 - (i - 12) / N)) / 2;
      left.push([p[0] - dy * w, p[1] + dx * w]);
      right.push([p[0] + dy * w, p[1] - dx * w]);
    }
    const f = (p) => p[0].toFixed(3) + " " + p[1].toFixed(3);
    const back = right.reverse();
    const tip = (t.w1 / 2).toFixed(3);
    return "M" + left.map(f).join(" L") + ` A ${tip} ${tip} 0 0 1 ` + f(back[0]) + " L" + back.slice(1).map(f).join(" L") + " Z";
  }

  const parts = FEET.map(([x, y]) => `<circle cx="${x}" cy="${y}" r="${FOOT_R}"/>`).join("") +
    `<path d="${BODY}"/><path d="${CASQUE}"/>`;
  const tail = tailPath(TAIL);
  const line = OUTLINE / FIT.s;

  function face(kind) {
    let s = `<path d="${RIDGE}" fill="none" stroke="${INK}" stroke-width="${2.2 / FIT.s}" stroke-linecap="round"/>`;
    s += `<path d="${MOUTH}" fill="none" stroke="${INK}" stroke-width="${3.2 / FIT.s}" stroke-linecap="round"/>`;
    const e = EYE;
    if (kind === "happy") {
      s += `<path d="M${e.x - e.r * 0.85} ${e.y + 1.5} Q ${e.x} ${e.y - e.r} ${e.x + e.r * 0.85} ${e.y + 1.5}" fill="none" stroke="${INK}" stroke-width="${3.4 / FIT.s}" stroke-linecap="round"/>`;
    } else {
      const oops = kind === "oops";
      s += `<circle cx="${e.x}" cy="${e.y}" r="${oops ? e.r + 1.5 : e.r}" fill="#FFFFFF" stroke="${INK}" stroke-width="${3.2 / FIT.s}"/>`;
      s += `<circle cx="${oops ? e.x : e.x + e.r * 0.3}" cy="${e.y}" r="${oops ? e.r * 0.22 : e.r * 0.48}" fill="${INK}"/>`;
    }
    return s;
  }

  // Fills are white so the game can tint them with the current mix.
  const LAYERS = {
    body_line: `<g fill="${INK}" stroke="${INK}" stroke-width="${line * 2}" stroke-linejoin="round">${parts}</g>`,
    tail_fill: `<path d="${tail}" fill="#FFFFFF"/>`,
    tail_line: `<path d="${tail}" fill="none" stroke="${INK}" stroke-width="${line}" stroke-linejoin="round"/>`,
    body_fill: `<g fill="#FFFFFF">${parts}</g>`,
    face_normal: face("normal"),
    face_happy: face("happy"),
    face_oops: face("oops"),
  };
  const fitted = (inner) => `<g transform="translate(${FIT.x} ${FIT.y}) scale(${FIT.s})">${inner}</g>`;

  return {
    INK,
    OUTLINE,
    /** The layer names, in the order CameliFigure stacks them (faces last; it shows one at a time). */
    LAYER_NAMES: Object.keys(LAYERS),
    /** One layer, placed in the 100 x 100 square. */
    layer: (name) => fitted(LAYERS[name]),
    /** The whole figure stacked like CameliFigure, the fills in `fill`, in the 100 x 100 square. */
    figure: (fill = "#FFFFFF", faceKind = "normal") => fitted(LAYERS.body_line +
      LAYERS.tail_fill.replace("#FFFFFF", fill) + LAYERS.tail_line +
      LAYERS.body_fill.replace("#FFFFFF", fill) + LAYERS["face_" + faceKind]),
  };
})();
