using System;
using System.Collections;
using System.IO;
using Pyramid.CameraRig;
using Pyramid.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PyramidsMaps.Sphinx
{
    /// <summary>
    /// For screenshots (e.g. a mod page):
    ///  F11  free camera on/off: arrow keys move, PageUp / PageDown up and down, Shift faster, mouse looks.
    ///       The HUD is hidden while it's on.
    ///  F10  clean screenshot: hides the HUD for that frame and saves a PNG to
    ///       BepInEx\plugins\PyramidsMaps\screenshots.
    /// </summary>
    internal sealed class CatPhoto : MonoBehaviour
    {
        private bool _free;
        private GameCamera _gameCam;
        private float _yaw, _pitch;
        private bool _hudWasHidden;

        /// <summary>Renderers hidden for clean shots (first-person hands, the build beacons), to turn back on.</summary>
        private readonly System.Collections.Generic.List<Renderer> _hidden = new System.Collections.Generic.List<Renderer>();

        private void HideClutter(Camera cam)
        {
            ShowClutter();
            void Hide(Component root)
            {
                if (root == null) return;
                foreach (var r in root.GetComponentsInChildren<Renderer>(false))
                    if (r.enabled) { r.enabled = false; _hidden.Add(r); }
            }
            // anything drawn as part of the camera (first-person hands, held bricks)
            for (int i = 0; i < cam.transform.childCount; i++) Hide(cam.transform.GetChild(i));
            // the light column at the work front and the beam over the final brick
            foreach (var b in FindObjectsByType<Pyramid.World.WorkFrontBeacon>()) Hide(b);
            foreach (var b in FindObjectsByType<Pyramid.Ending.CapstoneBeacon>()) Hide(b);
        }

        private void ShowClutter()
        {
            foreach (var r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
        }

        private const float Speed = 8f, FastSpeed = 30f, LookSensitivity = 0.08f;

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.f11Key.wasPressedThisFrame) ToggleFree();
            if (_free) Fly(kb);
            if (kb.f10Key.wasPressedThisFrame) StartCoroutine(Shot());
        }

        private void ToggleFree()
        {
            var cam = Camera.main;
            if (cam == null) return;
            _free = !_free;
            _gameCam = cam.GetComponent<GameCamera>();
            if (_free)
            {
                if (_gameCam != null) _gameCam.enabled = false;
                var e = cam.transform.eulerAngles;
                _yaw = e.y;
                _pitch = e.x > 180f ? e.x - 360f : e.x;
                _hudWasHidden = HudVisibility.Hidden;
                HudVisibility.DevSet(true);
                HideClutter(cam);
                Plugin.Log.LogInfo("Free camera on (F11 to leave): arrows move, PageUp/PageDown up/down, Shift faster, F10 saves a shot.");
            }
            else
            {
                if (_gameCam != null) _gameCam.enabled = true;
                HudVisibility.DevSet(_hudWasHidden);
                ShowClutter();
                Plugin.Log.LogInfo("Free camera off.");
            }
        }

        private void Fly(Keyboard kb)
        {
            var cam = Camera.main;
            if (cam == null) { _free = false; return; }
            var t = cam.transform;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                var d = mouse.delta.ReadValue();
                _yaw += d.x * LookSensitivity;
                _pitch = Mathf.Clamp(_pitch - d.y * LookSensitivity, -89f, 89f);
            }
            t.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            var move = Vector3.zero;
            if (kb.upArrowKey.isPressed) move += t.forward;
            if (kb.downArrowKey.isPressed) move -= t.forward;
            if (kb.rightArrowKey.isPressed) move += t.right;
            if (kb.leftArrowKey.isPressed) move -= t.right;
            if (kb.pageUpKey.isPressed) move += Vector3.up;
            if (kb.pageDownKey.isPressed) move -= Vector3.up;
            float speed = kb.shiftKey.isPressed ? FastSpeed : Speed;
            t.position += move * speed * Time.unscaledDeltaTime;
        }

        private IEnumerator Shot()
        {
            string dir = Path.Combine(Plugin.PluginDir, "screenshots");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "cat_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");

            bool wasHidden = HudVisibility.Hidden;
            HudVisibility.DevSet(true);
            yield return null;                    // let the HUD disappear
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(file, 1);
            yield return null;
            yield return null;
            if (!_free) HudVisibility.DevSet(wasHidden);
            Plugin.Log.LogInfo($"Screenshot saved: {file}");
        }
    }
}
