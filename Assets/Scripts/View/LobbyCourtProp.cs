// Marks a lobby court piece as a PROP: lit and shadow-receiving, but not a caster.
//
// WHY A COMPONENT AND NOT A NAME CHECK. The lobby's shadow contract
// (GameDirectorCampaignRouteTests.Lobby_CarriesStageMoodTerrainAndShadowLease) sweeps
// every renderer in the diorama and requires it to cast into the key light. That
// contract was written when the diorama was three actors; it is right about actors and
// about architecture, and it is wrong about small scenery — measured 2026-08-13, with
// every court piece casting, very-dark pixels in the play area went from 1.90% to
// 9.75% and two opaque black masses appeared beside the warden and the boss, because
// the key is a low hard directional and a candelabra throws a long solid blob.
//
// So the sweep needs a way to be told "this one is scenery". A marker component says
// it in the object graph, where the sweep already is, and cannot drift from
// LobbyCourt's layout table the way a name prefix or an index would.
using UnityEngine;

namespace CinderCourt.View
{
    /// <summary>Present on lobby court pieces that deliberately do not cast.</summary>
    public sealed class LobbyCourtProp : MonoBehaviour
    {
    }
}
