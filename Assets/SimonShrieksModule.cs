using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimonShrieks;
using UnityEngine;
using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Simon Shrieks
/// Created by IFBeetle, implemented by Timwi
/// </summary>
public class SimonShrieksModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;

    public KMSelectable[] Buttons;
    public Material[] ButtonColors;
    public Light[] Lights;
    public Transform Arrow;

    private int[] _buttonColors;
    private int[] _flashingButtons;
    private int[] _colorsToPress;
    private int _subprogress;
    private int _arrow;
    private int _stage;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private static readonly string _colorNames = "RYGCBWM";
    private static readonly string[] _grid = new[]
    {
        "GMCBYRCYBWR",
        "GWCWMYRWWRC",
        "YBWGGCWBRWM",
        "BRMYCRYGMBR",
        "WCYBGBRWGYC",
        "GYRCMRMGWRB",
        "YMGCMMGBCMW",
        "MBRYGYBWYRW",
        "RCYBCBGRCBM",
        "YYMGBMCYWCW",
        "WCGMRGCMGBB"
    };

    void Start()
    {
        _moduleId = _moduleIdCounter++;
        _buttonColors = Enumerable.Range(0, 7).ToArray().Shuffle();
        _flashingButtons = Enumerable.Range(0, 8).Select(i => Rnd.Range(0, 7)).ToArray();
        for (int i = 0; i < 7; i++)
        {
            Buttons[i].GetComponent<MeshRenderer>().material = ButtonColors[_buttonColors[i]];
            Buttons[i].OnInteract = getButtonPressHandler(i);
        }

        _arrow = Rnd.Range(0, 7);
        Arrow.localEulerAngles = new Vector3(0, -13 + 360f / 7 * (_arrow + 1), 0);

        setStage(0);
        StartCoroutine(flashing());
    }

    private void setStage(int newStage)
    {
        _stage = newStage;
        _subprogress = 0;

        if (_stage == 3)
        {
            Module.HandlePass();
            return;
        }

        var xs = new[] { 2, 8, 2, 8, 2, 8, 5 };
        var ys = new[] { 2, 2, 8, 8, 5, 5, 5 };

        int cx = xs[(_flashingButtons[0] + 7 - _arrow) % 7], cy = ys[(_flashingButtons[0] + 7 - _arrow) % 7];
        var journey = new StringBuilder();
        journey.AppendFormat("{0}/{1}", cx, cy);
        for (int i = 1; i < _stage * 2 + 4; i++)
        {
            int nx = xs[(_flashingButtons[i] + 7 - _arrow) % 7], ny = ys[(_flashingButtons[i] + 7 - _arrow) % 7];
            int dx = Math.Abs(cx - nx), dy = Math.Abs(cy - ny), sx = Math.Sign(nx - cx), sy = Math.Sign(ny - cy);

            if (dx >= dy)
                cx += sx;
            if (dx <= dy)
                cy += sy;
            journey.AppendFormat(" → {0}/{1}", cx, cy);
        }
        Debug.LogFormat(@"[Simon Shrieks #{0}] Stage {1} square center: {2}", _moduleId, _stage + 1, journey.ToString());

        var countColors = new int[7];
        var firstOccurrence = new int[7];
        var cells = 25;
        var square = new StringBuilder();
        for (int y = 2; y >= -2; y--)
            for (int x = 2; x >= -2; x--)
            {
                countColors[_colorNames.IndexOf(_grid[cy + y][cx + x])]++;
                firstOccurrence[_colorNames.IndexOf(_grid[cy + y][cx + x])] = cells;
                square.Append(_grid[cy + y][cx + x]);
                cells--;
            }

        var hasVowel = Bomb.GetSerialNumberLetters().Any(ch => "AEIOU".Contains(ch));
        _colorsToPress = Enumerable.Range(0, 7).Where(ix => countColors[ix] % 2 == (hasVowel ? 0 : 1)).ToArray();
        Array.Sort(_colorsToPress, (v1, v2) =>
            countColors[v1] > countColors[v2] ? 1 :
            countColors[v1] < countColors[v2] ? -1 :
            firstOccurrence[v1] > firstOccurrence[v2] ? 1 :
            firstOccurrence[v1] < firstOccurrence[v2] ? -1 : 0);

        Debug.LogFormat(@"[Simon Shrieks #{0}] Stage {1} colors to press: {2}", _moduleId, _stage + 1, _colorsToPress.Select(ix => _colorNames[ix]).JoinString(", "));
    }

    private KMSelectable.OnInteractHandler getButtonPressHandler(int i)
    {
        return delegate
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Buttons[i].transform);
            Buttons[i].AddInteractionPunch(.4f);

            if (_colorsToPress[_subprogress] == _buttonColors[i])
            {
                Debug.LogFormat(@"[Simon Shrieks #{0}] Pressing {1} was correct.", _moduleId, _buttonColors[i]);
                _subprogress++;
                if (_subprogress == _colorsToPress.Length)
                    setStage(_stage + 1);
            }
            else
            {
                Debug.LogFormat(@"[Simon Shrieks #{0}] You pressed {1} when I expected {2}.", _moduleId, _buttonColors[i], _colorsToPress[_subprogress]);
                Module.HandleStrike();
            }

            return false;
        };
    }

    private IEnumerator flashing()
    {
        for (int i = 0; i < Lights.Length; i++)
            Lights[i].enabled = false;

        while (_stage < 3)
        {
            for (int i = 0; _stage < 3 && i < _stage * 2 + 4; i++)
            {
                Lights[_flashingButtons[i]].enabled = true;
                yield return new WaitForSeconds(.7f);
                Lights[_flashingButtons[i]].enabled = false;
                yield return new WaitForSeconds(.1f);
            }

            yield return new WaitForSeconds(1.2f);
        }
    }
}
