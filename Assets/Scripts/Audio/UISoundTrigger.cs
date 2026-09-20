using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hover and click sounds for one UI element. Drop it on a button - or let
/// Tools > Ping Pong > Add UI Sounds put it on all of them - and pick which sound
/// each one makes: Back and Apply have their own, the rest share the plain click.
///
/// A disabled button stays quiet, so a dead end never sounds like it worked.
/// </summary>
public class UISoundTrigger : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Tooltip("Played when the pointer moves onto this. None = silent.")]
    [SerializeField] private SoundId _hover = SoundId.ButtonHover;

    [Tooltip("Played when this is clicked. Back and Apply have their own sounds.")]
    [SerializeField] private SoundId _click = SoundId.ButtonClick;

    private Selectable _selectable;

    private void Awake() => _selectable = GetComponent<Selectable>();

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (Interactable) AudioManager.Play(_hover);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Interactable) AudioManager.Play(_click);
    }

    private bool Interactable => _selectable == null || _selectable.IsInteractable();
}
