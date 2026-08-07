// Static story-beat catalog ported from the original stage-story-catalog
// (spec §8). Pure data: no Unity, no Sim types, zero allocation per lookup.
// Beat kinds: stageStart (watcher narration), bossEntry, bossPhase2 (taunt),
// bossPhase3 (last warning), completion (warden retrospective).
namespace CinderCourt.View
{
    /// <summary>Palette class of a story speaker (SpeechBubbleView tints).</summary>
    public enum SpeakerVoice
    {
        Ambient,   // watcher narration
        Boss,
        Warden,
    }

    public static class StoryCatalog
    {
        // Beat-kind keys (use these constants to avoid stringly-typed drift).
        public const string StageStart = "stageStart";
        public const string BossEntry = "bossEntry";
        public const string BossPhase2 = "bossPhase2";
        // P3 last-warning beat (spec §8 L154 "보스 20% (P3) | 최후 경고 (P2와
        // 다른 대사)"). Additive: the P3 lines are NEW — not part of the frozen
        // original catalog — and remain subject to operator review. Existing
        // keys and lines are untouched.
        public const string BossPhase3 = "bossPhase3";
        public const string Completion = "completion";

        // Speakers. The watcher narrates stage openings as a caption; boss and
        // warden names carry their own voice class (see VoiceOf).
        public const string Watcher = "감시자";
        public const string DuskWarden = "DUSK WARDEN";
        public const string CinderWarden = "CINDER WARDEN";
        public const string VeilTactician = "VEIL TACTICIAN";
        public const string GateSovereign = "GATE SOVEREIGN";
        // Cycle-2 executor-wing bosses (dungeon-roster-spec §비트).
        public const string SluiceKeeper = "SLUICE KEEPER";
        public const string BastionSentinel = "BASTION SENTINEL";
        public const string AshMagistrate = "ASH MAGISTRATE";

        /// <summary>
        /// Presentation class of a speaker — the palette key SpeechBubbleView
        /// resolves. Classification lives next to the speaker constants on
        /// purpose: the cycle-2 wing shipped rendering as ambient narration
        /// because the bubble matched name PREFIXES from a list three files
        /// away, so adding a boss here silently mis-coloured it. An exact
        /// switch over the same constants cannot drift that way.
        /// </summary>
        public static SpeakerVoice VoiceOf(string speaker)
        {
            switch (speaker)
            {
                case CinderWarden:
                case VeilTactician:
                case GateSovereign:
                case SluiceKeeper:
                case BastionSentinel:
                case AshMagistrate:
                    return SpeakerVoice.Boss;
                case DuskWarden:
                    return SpeakerVoice.Warden;
                default:
                    return SpeakerVoice.Ambient;   // watcher narration and anything unlisted
            }
        }

        /// <summary>
        /// Looks up the line for a stage/beat pair. Lines are verbatim from the
        /// original catalog (frozen — do not edit). Returns false for unknown
        /// stage ids or beat kinds; out params are null in that case.
        /// </summary>
        public static bool TryGet(string stageId, string beatKind,
                                  out string speaker, out string text)
        {
            switch (stageId)
            {
                case "cinder-span":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "서쪽 불씨를 버티고 사슬의 진실을 확인하세요.";
                            return true;
                        case BossEntry:
                            speaker = CinderWarden;
                            text = "등불을 내려라. 네가 찾는 길은 내 사슬 아래서 끝난다.";
                            return true;
                        case BossPhase2:
                            speaker = CinderWarden;
                            text = "봉인을 풀면 길이 열리는 게 아니다. 네 뒤의 다리가 먼저 무너진다.";
                            return true;
                        case BossPhase3:
                            speaker = CinderWarden;
                            text = "사슬이 끊긴다면… 다리도, 너도, 나와 함께 재가 된다!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "그는 문을 지킨 게 아니었다. 문이 올라오지 못하게 묶고 있었다.";
                            return true;
                    }
                    break;

                case "ember-gallery":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "불씨가 늘어선 회랑을 지나, 같은 사슬의 다른 매듭을 찾으세요.";
                            return true;
                        case BossEntry:
                            speaker = CinderWarden;
                            text = "불꽃이 늘었다고 길이 늘어난 것은 아니다.";
                            return true;
                        case BossPhase2:
                            speaker = CinderWarden;
                            text = "회랑 끝의 재는 네 발자국을 모두 기억한다.";
                            return true;
                        case BossPhase3:
                            speaker = CinderWarden;
                            text = "마지막 불씨다 — 꺼지기 전에 네 이름부터 태워 주마!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "불씨들은 길을 비췄다. 이제 어느 문이 진짜인지 골라야 한다.";
                            return true;
                    }
                    break;

                case "abyss-chancel":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "거울이 먼저 내놓은 답을 거부하세요.";
                            return true;
                        case BossEntry:
                            speaker = VeilTactician;
                            text = "또 같은 등불, 또 같은 서약.";
                            return true;
                        case BossPhase2:
                            speaker = VeilTactician;
                            text = "거울이 깨져도, 왕좌가 사라지는 것은 아니다.";
                            return true;
                        case BossPhase3:
                            speaker = VeilTactician;
                            text = "이 파편이 마지막 수다. 네 결말은 아직 내 손안에 있다!";
                            return true;
                        case Completion:
                            speaker = VeilTactician;
                            text = "그렇다면 왕좌도 너를 분류하지 못하겠군.";
                            return true;
                    }
                    break;

                case "witness-well":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "증언의 우물은 대답보다 먼저, 무엇을 잊었는지 묻습니다.";
                            return true;
                        case BossEntry:
                            speaker = VeilTactician;
                            text = "우물은 거짓말하지 않는다. 다만 전부 말하지 않을 뿐이지.";
                            return true;
                        case BossPhase2:
                            speaker = VeilTactician;
                            text = "네가 들은 증언은 아직 결말을 고르지 못했다.";
                            return true;
                        case BossPhase3:
                            speaker = VeilTactician;
                            text = "우물이 마른다… 그 전에 네 증언을 끝내 주마!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "우물은 잠잠해졌다. 남은 목소리는 네 선택을 기다린다.";
                            return true;
                    }
                    break;

                case "echo-throne":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "빈 왕좌보다 오래 남은 명령을 끓으세요.";
                            return true;
                        case BossEntry:
                            speaker = GateSovereign;
                            text = "마침내 내가 놓았던 등불을 네가 들고 왔다.";
                            return true;
                        case BossPhase2:
                            speaker = GateSovereign;
                            text = "단상을 차지해도 왕좌의 명령은 너에게 돌아온다.";
                            return true;
                        case BossPhase3:
                            speaker = GateSovereign;
                            text = "왕좌가 무너져도 마지막 명령은 남는다 — 꿇어라!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "왕좌는 비었다. 그런데 명령은 내 등불 안에서 계속된다.";
                            return true;
                    }
                    break;

                case "ash-verdict":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "재의 판결 앞에서, 왕좌가 남긴 명령의 무게를 견디세요.";
                            return true;
                        case BossEntry:
                            speaker = GateSovereign;
                            text = "판결은 끝났다. 남은 것은 네가 복종할 차례다.";
                            return true;
                        case BossPhase2:
                            speaker = GateSovereign;
                            text = "재가 되어도 명령은 사라지지 않는다.";
                            return true;
                        case BossPhase3:
                            speaker = GateSovereign;
                            text = "이것이 최후의 판결이다. 네 등불과 함께 재가 되어라!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "판결은 끝났다. 이제 등불은 네 손에서 다른 길을 밝힌다.";
                            return true;
                    }
                    break;

                // --- cycle-2 executor wing (additive — dungeon-roster-spec §비트).
                // Table format is "화자: 대사"; the speaker rides the speaker
                // field, the text below is the line after the colon, verbatim.
                case "cinder-sluice":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "판결문은 잿물이 되어 수문 아래로 흐른다.";
                            return true;
                        case BossEntry:
                            speaker = SluiceKeeper;
                            text = "기록은 흘려보내야 한다.";
                            return true;
                        case BossPhase2:
                            speaker = SluiceKeeper;
                            text = "역류는… 허락되지 않는다!";
                            return true;
                        case BossPhase3:
                            speaker = SluiceKeeper;
                            text = "수문이 버티지 못한다… 그렇다면 너까지 쓸어 내리겠다!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "말소된 이름 하나가 물살을 거슬러 떠올랐다.";
                            return true;
                    }
                    break;

                case "ember-bastion":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "위증자들이 방벽 뒤에서 숨죽인다.";
                            return true;
                        case BossEntry:
                            speaker = BastionSentinel;
                            text = "증언은 방패다. 뚫어 보아라.";
                            return true;
                        case BossPhase2:
                            speaker = BastionSentinel;
                            text = "방벽이 무너져도 위증은 남는다!";
                            return true;
                        case BossPhase3:
                            speaker = BastionSentinel;
                            text = "마지막 방벽이다… 이 몸이 무너지기 전에 너부터 묻겠다!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "방벽이 꺼지자 위증의 불씨가 사그라들었다.";
                            return true;
                    }
                    break;

                case "ash-march":
                    switch (beatKind)
                    {
                        case StageStart:
                            speaker = Watcher;
                            text = "재의 장벽이 행진한다 — 판결은 멈추지 않는다.";
                            return true;
                        case BossEntry:
                            speaker = AshMagistrate;
                            text = "형은 이미 집행되고 있다.";
                            return true;
                        case BossPhase2:
                            speaker = AshMagistrate;
                            text = "재 앞에서 모든 걸음은 무의미하다!";
                            return true;
                        case BossPhase3:
                            speaker = AshMagistrate;
                            text = "행진이 멈추는 일은 없다… 네 형장이 여기다!";
                            return true;
                        case Completion:
                            speaker = DuskWarden;
                            text = "행진이 멎었다. 랜턴이 마지막 기록을 비춘다.";
                            return true;
                    }
                    break;
            }

            speaker = null;
            text = null;
            return false;
        }
    }
}
