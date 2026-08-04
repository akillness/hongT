import {
  AbsoluteFill,
  Img,
  interpolate,
  random,
  spring,
  staticFile,
  useCurrentFrame,
  useVideoConfig,
} from 'remotion';

// Brand palette (Abyssal Lantern)
const BG = '#050812';
const EMBER = '#f3592c';
const CYAN = '#2cadd6';
const GOLD = '#ddc869';
const INK = '#e8e6f2';
const INK_DIM = 'rgba(232, 230, 242, 0.62)';

const KR_FONT =
  '"Apple SD Gothic Neo", "Noto Sans KR", sans-serif';

const NUM_EMBERS = 18;

type EmberSpec = {
  x: number; // base x position (px)
  startY: number; // initial y offset (px)
  size: number; // px
  speed: number; // px per frame upward
  swayAmp: number; // px
  swayFreq: number; // rad per frame
  phase: number; // rad
  baseOpacity: number;
};

// Deterministic per-index particle specs — remotion's random() is
// a pure seeded hash, never Math.random().
const makeEmber = (i: number): EmberSpec => ({
  x: random(`ember-x-${i}`) * 1280,
  startY: random(`ember-y-${i}`) * 900,
  size: 2 + random(`ember-s-${i}`) * 3.5,
  speed: 0.9 + random(`ember-v-${i}`) * 1.6,
  swayAmp: 10 + random(`ember-a-${i}`) * 22,
  swayFreq: 0.02 + random(`ember-f-${i}`) * 0.035,
  phase: random(`ember-p-${i}`) * Math.PI * 2,
  baseOpacity: 0.35 + random(`ember-o-${i}`) * 0.5,
});

const EMBERS: EmberSpec[] = Array.from({length: NUM_EMBERS}, (_, i) =>
  makeEmber(i)
);

const Embers: React.FC<{frame: number}> = ({frame}) => {
  return (
    <AbsoluteFill>
      {EMBERS.map((e, i) => {
        const travel = 720 + 120; // wrap span incl. offscreen margin
        const y = ((e.startY - e.speed * frame) % travel + travel) % travel - 60;
        const x = e.x + Math.sin(frame * e.swayFreq + e.phase) * e.swayAmp;
        const flicker =
          0.75 + 0.25 * Math.sin(frame * 0.11 + e.phase * 3);
        return (
          <div
            key={i}
            style={{
              position: 'absolute',
              left: x,
              top: y,
              width: e.size,
              height: e.size,
              borderRadius: '50%',
              backgroundColor: EMBER,
              opacity: e.baseOpacity * flicker,
              boxShadow: `0 0 ${e.size * 2.5}px ${e.size * 0.8}px rgba(243, 89, 44, 0.35)`,
            }}
          />
        );
      })}
    </AbsoluteFill>
  );
};

export const BrandBumper: React.FC = () => {
  const frame = useCurrentFrame();
  const {fps} = useVideoConfig();

  // --- Phase 1 (0-60): lantern springs in ---
  const lanternSpring = spring({
    frame: frame - 8,
    fps,
    config: {damping: 14, mass: 0.9, stiffness: 90},
  });
  const lanternOpacity = interpolate(frame, [8, 34], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const lanternScale = interpolate(lanternSpring, [0, 1], [0.55, 1]);

  // --- Phase 2 (50-140): title slides up, subtitle fades in ---
  const titleY = interpolate(frame, [52, 82], [46, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const titleOpacity = interpolate(frame, [52, 84], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const subtitleOpacity = interpolate(frame, [86, 116], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // --- Phase 3 (130-220): hero group rises/shrinks; team card enters ---
  const heroRise = interpolate(frame, [132, 162], [0, -104], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const heroScale = interpolate(frame, [132, 162], [1, 0.78], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const cardOpacity = interpolate(frame, [146, 176], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const cardY = interpolate(frame, [146, 176], [36, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const dividerWidth = interpolate(frame, [154, 186], [0, 240], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  // --- Phase 4 (210-280): fade everything, final beat ---
  const contentOpacity = interpolate(frame, [210, 242], [1, 0], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });
  const finalOpacity = interpolate(frame, [244, 266], [0, 1], {
    extrapolateLeft: 'clamp',
    extrapolateRight: 'clamp',
  });

  return (
    <AbsoluteFill style={{backgroundColor: BG, fontFamily: KR_FONT}}>
      {/* Scene content: embers + hero + team card, all fade at the end */}
      <AbsoluteFill style={{opacity: contentOpacity}}>
        <Embers frame={frame} />

        {/* Hero group: lantern + title + subtitle */}
        <AbsoluteFill
          style={{
            justifyContent: 'center',
            alignItems: 'center',
            transform: `translateY(${heroRise}px) scale(${heroScale})`,
          }}
        >
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 18,
            }}
          >
            <Img
              src={staticFile('app-lantern.png')}
              style={{
                width: 190,
                height: 190,
                opacity: lanternOpacity,
                transform: `scale(${lanternScale})`,
                filter: 'drop-shadow(0 0 34px rgba(243, 89, 44, 0.45))',
              }}
            />
            <div
              style={{
                color: INK,
                fontSize: 64,
                fontWeight: 800,
                letterSpacing: '0.22em',
                marginLeft: '0.22em', // optically re-center letter-spaced text
                opacity: titleOpacity,
                transform: `translateY(${titleY}px)`,
                whiteSpace: 'nowrap',
              }}
            >
              ABYSSAL LANTERN
            </div>
            <div
              style={{
                color: CYAN,
                fontSize: 24,
                fontWeight: 500,
                letterSpacing: '0.34em',
                marginLeft: '0.34em',
                opacity: subtitleOpacity,
                whiteSpace: 'nowrap',
              }}
            >
              Hold the Cinder Court
            </div>
          </div>
        </AbsoluteFill>

        {/* Team card */}
        <AbsoluteFill
          style={{
            justifyContent: 'flex-end',
            alignItems: 'center',
            paddingBottom: 96,
          }}
        >
          <div
            style={{
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'center',
              gap: 16,
              opacity: cardOpacity,
              transform: `translateY(${cardY}px)`,
            }}
          >
            <div
              style={{
                color: GOLD,
                fontSize: 40,
                fontWeight: 700,
                letterSpacing: '0.1em',
              }}
            >
              Hong팀
            </div>
            <div
              style={{
                width: dividerWidth,
                height: 2,
                backgroundColor: EMBER,
                boxShadow: '0 0 8px rgba(243, 89, 44, 0.6)',
              }}
            />
            <div
              style={{
                color: INK_DIM,
                fontSize: 20,
                fontWeight: 400,
                letterSpacing: '0.08em',
              }}
            >
              정장영 · 이석민 · 정우영
            </div>
          </div>
        </AbsoluteFill>
      </AbsoluteFill>

      {/* Final beat */}
      <AbsoluteFill
        style={{
          justifyContent: 'center',
          alignItems: 'center',
          opacity: finalOpacity,
        }}
      >
        <div
          style={{
            color: CYAN,
            fontSize: 26,
            fontWeight: 500,
            letterSpacing: '0.3em',
            marginLeft: '0.3em',
          }}
        >
          NAN 2026 Game X AI
        </div>
      </AbsoluteFill>

      {/* Vignette overlay */}
      <AbsoluteFill
        style={{
          background:
            'radial-gradient(ellipse at center, rgba(5, 8, 18, 0) 42%, rgba(5, 8, 18, 0.55) 78%, rgba(5, 8, 18, 0.85) 100%)',
          pointerEvents: 'none',
        }}
      />
    </AbsoluteFill>
  );
};
