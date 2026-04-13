using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Attach to a GameObject that has a UIDocument component.
/// Wires the UI Toolkit buttons to MainMenuController.
///
/// HIERARCHY SETUP:
///   MenuButtonsUI              ← GameObject with UIDocument + MenuButtonsUI (this script)
///     (UIDocument component)
///       Panel Settings: create one via Assets > Create > UI Toolkit > Panel Settings
///       Source Asset:   drag MenuButtons.uxml here
///
/// The UIDocument renders on its own panel completely separate from uGUI,
/// so it is immune to CanvasGroup / GraphicRaycaster issues.
///
/// POSITION:
///   In the Panel Settings asset set Sort Order to 30 (same as your old MainMenuCanvas).
///   The button-container is positioned via USS (left:32px top:120px) —
///   tweak those values in MenuButtons.uss to align with your folder graphic.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MenuButtonsUI : MonoBehaviour
{
    [Header("═══ References")]
    [Tooltip("The MainMenuController that handles the actual slide/fade logic.")]
    public MainMenuController menuController;

    // ─────────────────────────────────────────────────────────────
    // PRIVATE STATE
    // ─────────────────────────────────────────────────────────────

    private UIDocument _doc;
    private Button _btnStart;
    private Button _btnOptions;
    private Button _btnCredits;
    private Button _btnQuit;

    // ─────────────────────────────────────────────────────────────
    // UNITY LIFECYCLE
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        _doc = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = _doc.rootVisualElement;

        _btnStart   = root.Q<Button>("btn-start");
        _btnOptions = root.Q<Button>("btn-options");
        _btnCredits = root.Q<Button>("btn-credits");
        _btnQuit    = root.Q<Button>("btn-quit");

        _btnStart?.RegisterCallback<ClickEvent>(e => menuController?.OnStartPressed());
        _btnOptions?.RegisterCallback<ClickEvent>(e => menuController?.OnOptionsPressed());
        _btnCredits?.RegisterCallback<ClickEvent>(e => menuController?.OnCreditsPressed());
        _btnQuit?.RegisterCallback<ClickEvent>(e => menuController?.OnQuitPressed());
    }

    private void OnDisable()
    {
        _btnStart?.UnregisterCallback<ClickEvent>(e => menuController?.OnStartPressed());
        _btnOptions?.UnregisterCallback<ClickEvent>(e => menuController?.OnOptionsPressed());
        _btnCredits?.UnregisterCallback<ClickEvent>(e => menuController?.OnCreditsPressed());
        _btnQuit?.UnregisterCallback<ClickEvent>(e => menuController?.OnQuitPressed());
    }

    /// <summary>Hide the button panel (called by MainMenuController after Start is pressed).</summary>
    public void HideButtons()
    {
        if (_doc != null)
            _doc.rootVisualElement.style.display = DisplayStyle.None;
    }

    /// <summary>Show the button panel.</summary>
    public void ShowButtons()
    {
        if (_doc != null)
            _doc.rootVisualElement.style.display = DisplayStyle.Flex;
    }
}
