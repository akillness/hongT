// Static story-beat catalog ported from the original stage-story-catalog
// (spec §8). Pure data: no Unity, no Sim types, zero allocation per lookup.
// Beat kinds: stageStart (watcher narration), bossEntry, bossPhase2 (taunt),
// completion (warden retrospective).
namespace CinderCourt.View
{
    public static class StoryCatalog
    {
        // Beat-kind keys (use these constants to avoid stringly-typed drift).
        public const string StageStart = "stageStart";
        public const string BossEntry = "bossEntry";
        public const string BossPhase2 = "bossPhase2";
        public const string Completion = "completion";

        // Speakers. The watcher narrates stage openings as a caption; boss and
        // warden names double as the color key in SpeechBubbleView.
        public const string Watcher = "감시자";
        public const string DuskWarden = "DUSK WARDEN";
        public const string CinderWarden = "CINDER WARDEN";
        public const string VeilTactician = "VEIL TACTICIAN";
        public const string GateSovereign = "GATE SOVEREIGN";

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
                        case Completion:
                            speaker = DuskWarden;
                            text = "그는 문을 지킨 게 아니었다. 문이 올라오지 못하게 묶고 있었다.";
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
                        case Completion:
                            speaker = VeilTactician;
                            text = "그렇다면 왕좌도 너를 분류하지 못하겠군.";
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
                        case Completion:
                            speaker = DuskWarden;
                            text = "왕좌는 비었다. 그런데 명령은 내 등불 안에서 계속된다.";
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
