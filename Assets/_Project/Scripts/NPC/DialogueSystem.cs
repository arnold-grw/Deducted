using System.Collections.Generic;
using Deducted.Interaction;
using Deducted.Player;
using Deducted.Shop;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deducted.NPC
{
    /// <summary>
    /// Runs NPC talks: switches from the main camera to the interaction camera (placed at the NPC's
    /// camera point) and shows the NPC's pages at the bottom. Player movement is locked meanwhile.
    /// D (or Space on line pages) = next page (first reveals the whole line), A = previous page (A on the first page leaves),
    /// Tab = leave.
    /// Shop pages: W/S select, hold Space to buy (bar fills; release before the next purchase).
    /// Built by Deducted > Build Dialogue UI (+ Build Shop UI). One per scene.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.current != null;
        /// <summary>Frame the last talk closed in, so Tab that closed it doesn't also open the body overview.</summary>
        public static int ClosedFrame { get; private set; } = -1;

        [Tooltip("Disabled until a talk starts. Tag it MainCamera so dithered UI and overlays follow it.")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("Shown when the line is fully revealed.")]
        [SerializeField] private GameObject continueHint;
        [Tooltip("Shop list, shown on shop pages (Deducted > Build Shop UI).")]
        [SerializeField] private ShopView shopView;

        [Header("Keys")]
        [SerializeField] private Key nextKey = Key.D;
        [SerializeField] private Key backKey = Key.A;
        [SerializeField] private Key upKey = Key.W;
        [SerializeField] private Key downKey = Key.S;
        [Tooltip("Turns pages on line pages; hold to buy on shop pages.")]
        [SerializeField] private Key confirmKey = Key.Space;
        [Tooltip("Closes the whole talk at once.")]
        [SerializeField] private Key closeKey = Key.Tab;
        [Tooltip("Second leave key (same as Tab, like the body overview).")]
        [SerializeField] private Key closeKeyAlt = Key.E;
        [Tooltip("Seconds Space must be held to buy.")]
        [SerializeField] private float holdDuration = 1.5f;

        [Header("Text")]
        [Tooltip("Letters per second. 0 = whole line at once.")]
        [SerializeField] private float lettersPerSecond = 40f;
        [Tooltip("Shown in the box on shop pages without their own text.")]
        [SerializeField] private string shopHintText = "W / S  select      hold SPACE  confirm";

        [Header("Sounds (optional)")]
        [Tooltip("Going to the next / previous page.")]
        [SerializeField] private AudioClip nextSound;
        [Tooltip("Moving the selection in a shop.")]
        [SerializeField] private AudioClip selectSound;
        [Tooltip("A purchase went through.")]
        [SerializeField] private AudioClip buySound;
        [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
        [Tooltip("Hide the player (all its renderers, incl. props and particles) during talks, so it can't block the view.")]
        [SerializeField] private bool hidePlayer = true;

        [Header("Fade")]
        [Tooltip("Seconds to fade to black before the view switches (entering and leaving a talk).")]
        [SerializeField, Min(0f)] private float fadeInTime = 0.25f;
        [Tooltip("Seconds to fade back in after the switch.")]
        [SerializeField, Min(0f)] private float fadeOutTime = 0.35f;

        private NPCDialogue current;
        private Camera mainCamera;
        private PlayerMovement movement;
        private int index;
        private int openedFrame;
        private bool transitioning; // fading to black: no input until the switch
        private float lineStartTime;
        private int lineLength;
        private readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        // Shop state.
        private readonly ShopContext shop = new ShopContext();
        private int selected;
        private float holdProgress;
        private bool buyNeedsRelease;

        private DialoguePage Page => current.Pages[index];

        private void Awake()
        {
            Instance = this;
            // TextMeshPro needs these on a Screen Space - Camera canvas, or its text stays invisible.
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                                                   | AdditionalCanvasShaderChannels.Normal
                                                   | AdditionalCanvasShaderChannels.Tangent;
            if (root != null) root.SetActive(false);
            if (shopView != null) shopView.gameObject.SetActive(false);
            if (interactionCamera != null) interactionCamera.enabled = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Open(NPCDialogue npc)
        {
            if (current != null || npc == null || npc.Pages.Count == 0) return;
            current = npc;
            index = 0;
            openedFrame = Time.frameCount;

            // Movement is locked right away; the view switches once the screen is black.
            movement = FindFirstObjectByType<PlayerMovement>();
            if (movement != null) movement.Lock(this);
            mainCamera = Camera.main;

            shop.body = FindFirstObjectByType<Deducted.Body.PlayerBody>();
            shop.cigarettes = FindFirstObjectByType<Cigarettes>();
            shop.vendor = npc.SpeakerName;

            transitioning = true;
            Deducted.UI.ScreenFade.Transition(fadeInTime, fadeOutTime, ShowTalk);
        }

        // At full black: cut to the interaction camera and show the first page.
        private void ShowTalk()
        {
            transitioning = false;
            if (current == null) return;
            NPCDialogue npc = current;
            openedFrame = Time.frameCount;
            if (hidePlayer && movement != null) HidePlayer();

            // Cut to the interaction camera (stays on the game camera if the NPC has no Camera Point).
            if (npc.CameraPoint == null)
                Debug.LogWarning("NPCDialogue has no Camera Point, keeping the game camera.", npc);
            else if (interactionCamera != null)
            {
                interactionCamera.fieldOfView = npc.FieldOfView;
                PlaceCamera();
                interactionCamera.enabled = true;
                if (mainCamera != null && mainCamera != interactionCamera) mainCamera.enabled = false;
            }

            if (nameText != null) nameText.text = npc.SpeakerName;
            if (root != null) root.SetActive(true);
            ShowPage(false);
        }

        /// <summary>Ends the talk right away, no fade (e.g. the Collector grabs the player). No OnDialogueFinished.</summary>
        public void ForceClose()
        {
            if (current == null) return;
            if (transitioning) Deducted.UI.ScreenFade.Cancel();
            transitioning = false;
            Close(false);
        }

        // Leaving normally: fade to black, switch back to the game view, fade in.
        private void BeginClose(bool finishedNormally)
        {
            if (transitioning) return;
            transitioning = true;
            Deducted.UI.ScreenFade.Transition(fadeInTime, fadeOutTime, () =>
            {
                transitioning = false;
                if (current != null) Close(finishedNormally);
            });
        }

        private void Update()
        {
            if (current == null || transitioning || Deducted.UI.PauseMenu.BlocksInput) return;

            bool isShop = Page.type == PageType.Shop;
            bool revealed = isShop || UpdateReveal();
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (Pressed(kb, closeKey) || Pressed(kb, closeKeyAlt))
            {
                BeginClose(false);
                return;
            }

            if (isShop) UpdateShop(kb);
            if (current == null || Time.frameCount == openedFrame) return;

            // Space turns pages like D on line pages; on shop pages it only buys.
            if (Pressed(kb, nextKey) || (!isShop && Pressed(kb, confirmKey)))
            {
                if (!revealed) lineStartTime = float.MinValue; // show the whole line first
                else Turn(+1);
            }
            else if (Pressed(kb, backKey))
            {
                Turn(-1);
            }
        }

        private void LateUpdate()
        {
            if (current != null && !transitioning) PlaceCamera();
        }

        private static bool Pressed(Keyboard kb, Key key) => key != Key.None && kb[key].wasPressedThisFrame;

        // ---------------------------------------------------------------- pages

        private void Turn(int direction)
        {
            if (nextSound != null) Deducted.Audio.UISound.Play(nextSound, soundVolume);
            index += direction;
            if (index < 0) { index = 0; BeginClose(false); }        // A on the first page: leave
            else if (index >= current.Pages.Count) { index--; BeginClose(true); } // D past the last page: done
            else ShowPage(direction < 0);
        }

        // Going back shows the line at once, it was already read.
        private void ShowPage(bool instant)
        {
            DialoguePage page = Page;
            bool isShop = page.type == PageType.Shop;

            if (shopView != null) shopView.gameObject.SetActive(isShop);
            else if (isShop) Debug.LogWarning("Shop page but no Shop View on the DialogueSystem (Deducted > Build Shop UI).", this);

            if (isShop)
            {
                lineText.text = string.IsNullOrEmpty(page.text) ? shopHintText : page.text;
                lineText.maxVisibleCharacters = 99999;
                if (continueHint != null) continueHint.SetActive(true);
                selected = 0;
                holdProgress = 0f;
                buyNeedsRelease = true; // Space may still be down from opening the talk
                RefreshShop();
                return;
            }

            lineText.text = page.text;
            // Counted from the string: TextMeshPro's own count can still be 0 right after the box appears,
            // which hid the whole line.
            lineLength = StripTags(page.text).Length;
            lineStartTime = instant ? float.MinValue : Time.unscaledTime;
            UpdateReveal();
        }

        // Typewriter. Returns true when the whole line is visible.
        private bool UpdateReveal()
        {
            int visible = lettersPerSecond <= 0f || lineStartTime == float.MinValue
                ? lineLength
                : Mathf.FloorToInt((Time.unscaledTime - lineStartTime) * lettersPerSecond);
            visible = Mathf.Min(visible, lineLength);
            bool revealed = visible >= lineLength;
            lineText.maxVisibleCharacters = revealed ? 99999 : visible;
            if (continueHint != null) continueHint.SetActive(revealed);
            return revealed;
        }

        // ---------------------------------------------------------------- shop

        private void UpdateShop(Keyboard kb)
        {
            List<ShopOffer> offers = Page.offers;
            bool held = confirmKey != Key.None && kb[confirmKey].isPressed;
            if (!held) buyNeedsRelease = false;

            if (offers.Count > 0)
            {
                int before = selected;
                if (Pressed(kb, upKey)) selected--;
                if (Pressed(kb, downKey)) selected++;
                selected = Mathf.Clamp(selected, 0, offers.Count - 1);
                if (selected != before)
                {
                    if (selectSound != null) Deducted.Audio.UISound.Play(selectSound, soundVolume);
                    holdProgress = 0f;
                    buyNeedsRelease = held; // switching while holding doesn't carry over
                }

                ShopOffer offer = offers[selected];
                if (held && !buyNeedsRelease && offer.IsAvailable(shop))
                {
                    holdProgress += Time.unscaledDeltaTime / Mathf.Max(holdDuration, 0.01f);
                    if (holdProgress >= 1f)
                    {
                        holdProgress = 0f;
                        buyNeedsRelease = true; // one purchase per hold
                        if (offer.Buy(shop) && buySound != null) Deducted.Audio.UISound.Play(buySound, soundVolume);
                    }
                }
                else holdProgress = 0f;
            }

            RefreshShop();
        }

        private void RefreshShop()
        {
            if (shopView != null && current != null)
                shopView.Refresh(Page.offers, selected, holdProgress, shop);
        }

        // ---------------------------------------------------------------- camera / player

        private void PlaceCamera()
        {
            if (interactionCamera == null || current.CameraPoint == null) return;
            interactionCamera.transform.SetPositionAndRotation(current.CameraPoint.position, current.CameraPoint.rotation);
        }

        private void HidePlayer()
        {
            hiddenRenderers.Clear();
            foreach (Renderer r in movement.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;
                r.enabled = false;
                hiddenRenderers.Add(r); // only these come back, things that were off stay off
            }
        }

        private void ShowPlayer()
        {
            foreach (Renderer r in hiddenRenderers)
                if (r != null) r.enabled = true;
            hiddenRenderers.Clear();
        }

        private static string StripTags(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");

        private void Close(bool finishedNormally = true)
        {
            NPCDialogue finished = current;
            current = null;
            ClosedFrame = Time.frameCount;

            if (root != null) root.SetActive(false);
            if (shopView != null) shopView.gameObject.SetActive(false);
            if (mainCamera != null) mainCamera.enabled = true;
            if (interactionCamera != null) interactionCamera.enabled = false;
            if (movement != null) movement.Unlock(this);
            ShowPlayer();
            Interactable.BlockedUntilFrame = Time.frameCount + 1;

            if (finishedNormally) finished.OnDialogueFinished();
        }
    }
}
