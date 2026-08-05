// §Lane V4 quality gate. Desktop keeps URP post processing (bloom+vignette):
// live-build p95 measured 10.0 ms against the 16.7 ms budget (~6.7 ms head-
// room; bloom at quarter-res costs well under that). Mobile browsers are
// UNMEASURED from this harness — the spec's rule is degrade, not
// ship-and-hope, so the camera flag turns off there. Decoration only;
// nothing in the sim or input path reads this.
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CinderCourt.View
{
    [RequireComponent(typeof(Camera))]
    public sealed class PostFxGate : MonoBehaviour
    {
        void Awake()
        {
            var data = GetComponent<UniversalAdditionalCameraData>();
            if (data == null) return;
            if (Application.isMobilePlatform)
                data.renderPostProcessing = false;   // unmeasured tier -> off
        }
    }
}
