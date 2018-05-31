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
    public MeshFilter Arrow;
    public Mesh[] StageMeshes;

    private int[] _buttonColors;
    private int[] _flashingButtons;
    private int[] _colorsToPress;
    private int _subprogress;
    private int _arrow;
    private int _stage;
    private bool _makeSounds;
    private Coroutine _blinker;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private static readonly string _colorNames = "RYGCBWM";
    private static readonly string[] _tpColorNames = new[] { "Red", "Yellow", "Green", "Cyan", "Blue", "White", "Magenta" };
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
        Arrow.transform.localEulerAngles = new Vector3(0, -13 + 360f / 7 * (_arrow + 1), 0);

        setStage(0);
        runBlinker(.1f);
    }

    private void setStage(int newStage)
    {
        _stage = newStage;
        _subprogress = 0;

        if (_stage == 3)
        {
            Arrow.gameObject.SetActive(false);
            StartCoroutine(victory());
            return;
        }
        Arrow.mesh = StageMeshes[newStage];

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
        startBlinker(1f);
    }

    private IEnumerator victory()
    {
        yield return new WaitForSeconds(.25f);

        for (int i = 0; i < 14; i++)
        {
            var ix = (i + 1) % 7;
            Audio.PlaySoundAtTransform("lightup" + (6 - i % 7), Buttons[ix].transform);
            Lights[ix].enabled = true;
            yield return new WaitForSeconds(.1f);
            Lights[ix].enabled = false;
        }
        Module.HandlePass();
    }

    private KMSelectable.OnInteractHandler getButtonPressHandler(int ix)
    {
        return delegate
        {
            if (_stage == 3)
                return false;

            Buttons[ix].AddInteractionPunch();

            _makeSounds = true;
            CancelInvoke("startBlinker");

            if (_buttonColors[ix] != _colorsToPress[_subprogress])
            {
                Debug.LogFormat("[Simon Shrieks #{3}] Expected {0}, but you pressed {1}. Input reset. Now at stage {2} key 1.", _colorNames[_colorsToPress[_subprogress]], _colorNames[_buttonColors[ix]], _stage + 1, _moduleId);
                Module.HandleStrike();
                _subprogress = 0;
                startBlinker(1.5f);
            }
            else
            {
                _subprogress++;
                var logStage = false;
                if (_subprogress == _colorsToPress.Length)
                {
                    setStage(_stage + 1);
                    logStage = true;
                }
                else
                    startBlinker(5f);

                if (_stage < 3)
                {
                    Debug.LogFormat("[Simon Shrieks #{3}] Pressing {0} was correct; now at stage {1} key {2}.", _colorNames[_buttonColors[ix]], _stage + 1, _subprogress + 1, _moduleId);
                    if (logStage)
                        logCurrentStage();
                }
                else
                    Debug.LogFormat("[Simon Shrieks #{1}] Pressing {0} was correct; module solved.", _colorNames[_buttonColors[ix]], _moduleId);
            }

            StartCoroutine(flashUpOne(ix));
            return false;
        };
    }

    private void logCurrentStage()
    {
        Debug.LogFormat("[Simon Shrieks #{2}] Stage {0} sequence: {1}", _stage + 1, Enumerable.Range(0, 4 + 2 * _stage).Select(ix => _colorNames[_flashingButtons[ix]]).JoinString(", "), _moduleId);
        Debug.LogFormat("[Simon Shrieks #{2}] Stage {0} expected keypresses: {1}", _stage + 1, _colorsToPress.Select(ix => _colorNames[ix]).JoinString(", "), _moduleId);
    }

    private void startBlinker(float delay)
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        foreach (var light in Lights)
            light.enabled = false;
        _blinker = StartCoroutine(runBlinker(delay));
    }

    private void startBlinker()
    {
        if (_blinker != null)
            StopCoroutine(_blinker);
        _blinker = StartCoroutine(runBlinker());
    }

    private IEnumerator runBlinker(float delay = 0)
    {
        yield return new WaitForSeconds(delay);

        if (_subprogress != 0)
        {
            Debug.LogFormat("[Simon Shrieks #{1}] Waited too long; input reset. Now at stage {0} key 1.", _stage + 1, _moduleId);
            _subprogress = 0;
        }
        while (_stage < 3)
        {
            for (int i = 0; i < _stage * 2 + 4; i++)
            {
                var ix = _flashingButtons[i];
                if (_makeSounds)
                    Audio.PlaySoundAtTransform("lightup" + ix, transform);
                Lights[ix].enabled = true;
                yield return new WaitForSeconds(.3f);
                Lights[ix].enabled = false;
                yield return new WaitForSeconds(.1f);
            }
            yield return new WaitForSeconds(2.5f);
        }
    }

    private IEnumerator flashUpOne(int ix)
    {
        Audio.PlaySoundAtTransform("lightup" + ix, Buttons[ix].transform);
        Lights[ix].enabled = true;
        yield return new WaitForSeconds(.5f);
        Lights[ix].enabled = false;
        yield return new WaitForSeconds(.1f);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Press the correct colors for each round with “!{0} press Blue Yellow Magenta” or “!{0} B Y M”. Permissible colors are: Red, Yellow, Green, Cyan, Blue, White, Magenta.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var pieces = command.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var skip = 0;
        if (pieces.Length > 0 && pieces[0] == "press")
            skip = 1;

        var buttons = new List<KMSelectable>();
        foreach (var piece in pieces.Skip(skip))
        {
            var ix = _tpColorNames.IndexOf(cs => cs.Equals(piece, StringComparison.InvariantCultureIgnoreCase) || (piece.Length == 1 && cs.StartsWith(piece, StringComparison.InvariantCultureIgnoreCase)));
            if (ix == -1)
                yield break;
            buttons.Add(Buttons[Array.IndexOf(_buttonColors, ix)]);
        }

        yield return null;

        foreach (var btn in buttons)
        {
            btn.OnInteract();
            if (_stage >= 3)
            {
                yield return "solve";
                yield break;
            }
            yield return new WaitForSeconds(.4f);
        }
    }

	private IEnumerator TwitchHandleForcedSolve()
	{
		yield return null;
		while (_stage < 3)
		{
			Buttons[Array.IndexOf(_buttonColors, _colorsToPress[_subprogress])].OnInteract();
			yield return new WaitForSeconds(0.4f);
		}
	}
}
