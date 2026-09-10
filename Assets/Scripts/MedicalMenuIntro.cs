using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays a simple fade + scale-in whenever this GameObject (the menu root) becomes
/// active: OnEnable is called both the first time the scene loads (if the menu
/// starts active) and every time something does menu.SetActive(true) afterwards,
/// so this covers "when Medical_Menu appears" without any extra wiring.
///
/// No DOTween in this project, so easing is done by hand with Mathf.SmoothStep
/// (an ease-in-out curve) inside a coroutine.
/// </summary>
public class MedicalMenuIntro : MonoBehaviour
{
    [SerializeField] private float duration = 0.35f;

    private static readonly int FadeAlphaId = Shader.PropertyToID("_FadeAlpha");

    private Vector3 _targetScale;
    private Renderer[] _fadeRenderers; // panel + button backgrounds (the Holo shader), not the text meshes
    private TMP_Text[] _texts;
    private float[] _textBaseAlphas;
    private MaterialPropertyBlock _mpb;
    private Coroutine _playing;

    private void Awake()
    {
        _targetScale = transform.localScale;

        _fadeRenderers = GetComponentsInChildren<Renderer>(true)
            .Where(r => r.GetComponent<TMP_Text>() == null)
            .ToArray();

        _texts = GetComponentsInChildren<TMP_Text>(true);
        _textBaseAlphas = _texts.Select(t => t.alpha).ToArray();
        _mpb = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        if (_playing != null) StopCoroutine(_playing);
        _playing = StartCoroutine(PlayIntro());
    }

    /// <summary>
    /// Reverse of the intro: fades + shrinks out, then deactivates the object.
    /// Scale/alpha are restored first so the next OnEnable starts from a clean state.
    /// </summary>
    public void PlayHide()
    {
        if (!gameObject.activeInHierarchy) return;

        if (_playing != null) StopCoroutine(_playing);
        _playing = StartCoroutine(PlayOutro());
    }

    private IEnumerator PlayOutro()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float eased = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / duration));

            transform.localScale = _targetScale * eased;
            SetVisualAlpha(eased);
            yield return null;
        }

        transform.localScale = _targetScale;
        SetVisualAlpha(1f);
        _playing = null;
        gameObject.SetActive(false);
    }

    private IEnumerator PlayIntro()
    {
        transform.localScale = Vector3.zero;
        SetVisualAlpha(0f);

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float linear = Mathf.Clamp01(t / duration);
            float eased = Mathf.SmoothStep(0f, 1f, linear); // ease-in-out, no DOTween needed

            transform.localScale = _targetScale * eased;
            SetVisualAlpha(eased);
            yield return null;
        }

        transform.localScale = _targetScale;
        SetVisualAlpha(1f);
        _playing = null;
    }

    private void SetVisualAlpha(float a)
    {
        foreach (var r in _fadeRenderers)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FadeAlphaId, a);
            r.SetPropertyBlock(_mpb);
        }

        for (int i = 0; i < _texts.Length; i++)
        {
            _texts[i].alpha = _textBaseAlphas[i] * a;
        }
    }
}
