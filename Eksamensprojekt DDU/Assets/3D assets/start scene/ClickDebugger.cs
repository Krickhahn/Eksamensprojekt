using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Attach to any GameObject. Uses the New Input System to detect clicks
/// and logs exactly what the EventSystem raycaster hits.
/// </summary>
public class ClickDebugger : MonoBehaviour
{
    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();

        // ── 1. EventSystem check ─────────────────────────────────
        if (EventSystem.current == null)
        {
            Debug.LogError("[ClickDebug] NO EVENTSYSTEM IN SCENE.");
            return;
        }

        Debug.Log($"[ClickDebug] Click detected at {mousePos}");

        // ── 2. RaycastAll ────────────────────────────────────────
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = mousePos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        if (results.Count == 0)
        {
            Debug.LogWarning("[ClickDebug] RaycastAll hit NOTHING — no UI element under mouse with Raycast Target ON.");
        }
        else
        {
            Debug.Log($"[ClickDebug] {results.Count} hit(s) — front to back:");
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                var graphic = r.gameObject.GetComponent<UnityEngine.UI.Graphic>();
                Debug.Log($"  [{i}] '{r.gameObject.name}' " +
                          $"| sortOrder:{r.sortingOrder} depth:{r.depth} " +
                          $"| RaycastTarget:{graphic?.raycastTarget} " +
                          $"| Canvas:'{r.gameObject.GetComponentInParent<Canvas>()?.name}'");
            }
            Debug.Log($"[ClickDebug] TOP HIT (eating the click): '{results[0].gameObject.name}'");
        }

        // ── 3. All canvases ──────────────────────────────────────
        foreach (var c in FindObjectsOfType<Canvas>())
        {
            var gr = c.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            Debug.Log($"[ClickDebug] Canvas '{c.name}' sortOrder:{c.sortingOrder} " +
                      $"renderMode:{c.renderMode} GR:{(gr != null ? "YES" : "MISSING")} " +
                      $"active:{c.gameObject.activeInHierarchy}");
        }
    }
}