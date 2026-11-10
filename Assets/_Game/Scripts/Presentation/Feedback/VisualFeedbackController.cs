using DontTrustYourLuck.Domain.Match;
using DontTrustYourLuck.Presentation.Board;
using DontTrustYourLuck.Presentation.Match;
using DontTrustYourLuck.Presentation.UI;
using UnityEngine;

namespace DontTrustYourLuck.Presentation.Feedback
{
    /// <summary>
    /// Domain event (played back by the MatchDirector) -> visual feedback: active-player marker, banners,
    /// and pooled particle bursts (one ParticleSystem per effect, reused with Emit). Fire-and-forget: it never
    /// blocks playback and never touches match state.
    /// </summary>
    public sealed class VisualFeedbackController : MonoBehaviour
    {
        [SerializeField] private MatchDirector director;
        [SerializeField] private GameHUD hud;
        [Tooltip("Floating marker attached to the piece whose turn it is.")]
        [SerializeField] private Transform activeMarker;
        [SerializeField] private float markerHeight = 1.55f;

        [Header("Particles (shared, emitted on demand)")]
        [SerializeField] private ParticleSystem confetti;
        [SerializeField] private ParticleSystem puff;
        [SerializeField] private ParticleSystem sparkle;

        [Header("Banner icons")]
        [SerializeField] private Sprite unluckyIcon;
        [SerializeField] private Sprite shieldIcon;

        [Header("Banner durations (seconds, non-blocking)")]
        [SerializeField] private float turnBanner = 0.6f;
        [Tooltip("0 = bots get no turn banner (the floating marker is enough).")]
        [SerializeField] private float botTurnBanner = 0f;
        [SerializeField] private float unluckyBanner = 1f;
        [SerializeField] private float cardBanner = 0.9f;
        [SerializeField] private float eliminationBanner = 1f;

        private bool _subscribed;
        private static readonly HudText DefaultText = new HudText(); // fallback when no HUD is wired

        public Transform ActiveMarker => activeMarker;
        public int MarkedPlayerId { get; private set; } = -1;

        public void Configure(MatchDirector matchDirector, GameHUD gameHud, Transform marker, ParticleSystem confettiFx,
            ParticleSystem puffFx, ParticleSystem sparkleFx, Sprite unlucky, Sprite shield)
        {
            director = matchDirector; hud = gameHud; activeMarker = marker;
            confetti = confettiFx; puff = puffFx; sparkle = sparkleFx;
            unluckyIcon = unlucky; shieldIcon = shield;
        }

        private void OnEnable() => Subscribe();
        private void Start() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed || director == null) return;
            director.EventPresenting -= OnEventPresenting;
            _subscribed = false;
        }

        private void Subscribe()
        {
            if (_subscribed || director == null) return;
            director.EventPresenting += OnEventPresenting;
            _subscribed = true;
        }

        private void OnEventPresenting(MatchEvent e)
        {
            var text = hud != null && hud.Text != null ? hud.Text : DefaultText;
            switch (e)
            {
                case MatchStartedEvent _:
                    DetachMarker();
                    if (hud != null && hud.Banner != null) hud.Banner.Hide();
                    break;

                case UnluckyNumberDrawnEvent unlucky:
                    bool zone = director != null && director.HasUnluckyZone;
                    ShowBanner(zone ? string.Format(text.bannerUnluckyZone, director.UnluckyDisplayFor(unlucky.UnluckyNumber))
                            : string.Format(text.bannerUnlucky, unlucky.UnluckyNumber),
                        zone ? text.bannerUnluckyZoneSub : text.bannerUnluckySub,
                        HudPalette.Danger, unluckyIcon, unluckyBanner);
                    break;

                case TurnChangedEvent turn:
                    AttachMarker(turn.PlayerId);
                    if (director.TryGetPlayer(turn.PlayerId, out var p))
                    {
                        bool bot = director.IsBotPlayer(turn.PlayerId);
                        ShowBanner(string.Format(text.bannerTurn, p.Name), bot ? string.Format(text.bannerTurnBot, p.Name) : null,
                            p.Color, p.Portrait, bot ? botTurnBanner : turnBanner);
                    }
                    break;

                case CardPlayedEvent played:
                    var asset = director.GetCardAsset(played.Card.CardId);
                    string cardName = asset != null && !string.IsNullOrEmpty(asset.DisplayName) ? asset.DisplayName : played.Card.CardId;
                    ShowBanner(string.Format(text.bannerCard, cardName), string.Format(text.bannerCardSub, director.PlayerName(played.PlayerId)),
                        HudPalette.Lucky, asset != null ? asset.Art : null, cardBanner);
                    Burst(sparkle, played.PlayerId, 18);
                    break;

                case ShieldConsumedEvent shield:
                    if (!shield.BlockedElimination) break;
                    ShowBanner(string.Format(text.bannerShieldBlocked, director.PlayerName(shield.PlayerId)), null,
                        HudPalette.Green, shieldIcon, cardBanner);
                    Burst(sparkle, shield.PlayerId, 24);
                    break;

                case PlayerEliminatedEvent eliminated:
                    Burst(puff, eliminated.PlayerId, 26);
                    ShowBanner(string.Format(text.bannerEliminated, director.PlayerName(eliminated.PlayerId)), null,
                        HudPalette.Danger, unluckyIcon, eliminationBanner);
                    if (MarkedPlayerId == eliminated.PlayerId) DetachMarker();
                    break;

                case PlayerWonEvent won:
                    Burst(confetti, won.PlayerId, 70);
                    break;

                case MatchFinishedEvent _:
                    DetachMarker();
                    break;
            }
        }

        private void ShowBanner(string title, string subtitle, Color accent, Sprite icon, float duration)
        {
            if (hud == null || hud.Banner == null || duration <= 0f) return;
            hud.Banner.Show(title, subtitle, accent, icon, duration);
        }

        private void AttachMarker(int playerId)
        {
            if (activeMarker == null) return;
            if (!director.TryGetPiece(playerId, out var piece))
            {
                DetachMarker();
                return;
            }
            // Parented to the piece root: it follows hops without any per-frame tracking code.
            activeMarker.SetParent(piece.transform, false);
            activeMarker.localPosition = new Vector3(0f, markerHeight, 0f);
            activeMarker.localRotation = Quaternion.identity;
            activeMarker.gameObject.SetActive(true);
            MarkedPlayerId = playerId;
        }

        private void DetachMarker()
        {
            MarkedPlayerId = -1;
            if (activeMarker == null) return;
            activeMarker.SetParent(transform, false);
            activeMarker.gameObject.SetActive(false);
        }

        private void Burst(ParticleSystem system, int playerId, int count)
        {
            if (system == null || !director.TryGetPiece(playerId, out var piece)) return;
            system.transform.position = piece.transform.position + Vector3.up * 0.5f;
            system.Emit(count);
        }
    }
}
