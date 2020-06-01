using System.Collections.Generic;
using FMODUnity;
using KN_Core;
using UnityEngine;

namespace KN_Cinematic {
  public class Cinematic : BaseMod {
    private const string FreeCamTag = "kn_freecam";
    private const float EnableButtonOffset = Gui.Height + Gui.OffsetY;
    private const float ScrollScale = 1.0f;
    private const float FovMin = 10.0f;
    private const float FovMax = 120.0f;

    public bool CinematicEnabled { get; private set; }
    public CTKeyframe CurrentKeyframe { get; set; }
    public GameObject MainCamera { get; private set; }

    public CTCamera FreeCamera { get; private set; }
    public CTCamera ActiveCamera { get; private set; }

    private int ctCameraId_;
    private readonly IList<CTCamera> ctCameras_;
    private float ctListScrollH_;
    private Vector2 ctListScroll_;

    private bool cameraTabActive_ = true;
    private bool animationTabActive_;
    private bool replayTabActive_;

    private float speed_;
    private float speedMultiplier_;

    private float kfListScrollH_;
    private Vector2 kfListScroll_;

    private string animationScaleString_ = "0.0";
    private string animationBeginString_ = "0.0";

    public Cinematic(Core core) : base(core, "CINEMATIC", 1) {
      ctCameras_ = new List<CTCamera>();

      Core.Timeline.OnPlay += OnTimelinePlay;
      Core.Timeline.OnStop += OnTimelineStop;
      Core.Timeline.OnDrag += OnTimelineDrag;
      Core.Timeline.OnKeyframe += OnTimelineKeyframe;
      Core.Timeline.OnKeyframeEdit += OnTimelineKeyframeEdit;
    }

    public override void OnStart() {
      speed_ = Core.ModConfig.Get<float>("freecam_speed");
      speedMultiplier_ = Core.ModConfig.Get<float>("freecam_speed_multiplier");
    }

    public override void ResetState() {
      ResetPickers();
      cameraTabActive_ = true;
      animationTabActive_ = false;
      replayTabActive_ = false;
      Core.DrawTimeline = CinematicEnabled;
      Core.Timeline.IsPlaying = Core.Timeline.IsPlaying && CinematicEnabled;
    }

    public override void ResetPickers() {
      ActiveCamera?.ResetState();
    }

    private void ResetAll() {
      ResetState();

      if (FreeCamera != null) {
        Object.Destroy(FreeCamera.GameObject);
        FreeCamera = null;
      }
      if (ActiveCamera != null) {
        Object.Destroy(ActiveCamera.GameObject);
        ActiveCamera = null;
      }

      ctCameraId_ = 0;
      ctCameras_.Clear();

      SetMainCamera(true);
      ToggleCinematicCamera();

      Core.Timeline.Reset();
    }

    public override void Update(int id) {
      if (MainCamera == null) {
        CinematicEnabled = false;
        ResetAll();
      }

      Core.Replay.Update();

      if (ActiveCamera != null) {
        ActiveCamera.Move(speed_, speedMultiplier_);

        if (Controls.Key("freecam_rotation")) {
          ActiveCamera.Rotate(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0.0f);
        }

        ActiveCamera.Fov += Input.mouseScrollDelta.y * ScrollScale;
        if (ActiveCamera.Fov < FovMin) {
          ActiveCamera.Fov = FovMin;
        }
        else if (ActiveCamera.Fov > FovMax) {
          ActiveCamera.Fov = FovMax;
        }

        if (ActiveCamera.Animation.AllowPlay && Core.Timeline.IsPlaying) {
          ActiveCamera.UpdateAnimation(Core.Timeline.CurrentTime);
        }

        CurrentKeyframe = ActiveCamera.Animation.CurrentFrame;
        if (CurrentKeyframe == null) {
          Core.Timeline.IsKeyframeEditing = false;
        }

        ActiveCamera.Update();
      }

      if (Core.Timeline.IsPlaying) {
        Core.Replay.TimeUpdate(Core.Timeline.CurrentTime, false);
      }
    }

    public override void FixedUpdate(int id) {
      if (id != Id) {
        return;
      }

      Core.Replay.FixedUpdate();
    }

    #region gui
    public override void OnGUI(int id, Gui gui, ref float x, ref float y) {
      if (id != Id) {
        return;
      }

      GuiSideBar(gui, ref x, ref y);

      x += Gui.OffsetGuiX;
      gui.Line(x, y, 1.0f, Core.GuiTabsHeight - Gui.OffsetY * 2.0f, Skin.SeparatorColor);
      x += Gui.OffsetGuiX;

      string text = CinematicEnabled ? "ENABLED" : "DISABLED";
      if (gui.Button(ref x, ref y, Gui.Width, Gui.Height, "CINEMATIC | " + text, CinematicEnabled ? Skin.ButtonActive : Skin.Button)) {
        CinematicEnabled = !CinematicEnabled;
        Core.DrawTimeline = CinematicEnabled;
        ToggleCinematic();
      }

      float width = Core.GuiTabsWidth - Gui.IconSize * 2.0f;
      gui.Line(x, y, width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (cameraTabActive_) {
        GuiCameraTab(gui, ref x, ref y);
      }
      else if (animationTabActive_) {
        GuiAnimationTab(gui, ref x, ref y);
      }
      else if (replayTabActive_) {
        GuiReplay(gui, ref x, ref y);
      }
    }

    #region camera gui
    private void GuiCameraTab(Gui gui, ref float x, ref float y) {
      float yBegin = y;
      float xBegin = x;

      if (!CinematicEnabled) {
        return;
      }

      if (gui.Button(ref x, ref y, "RESET ALL", Skin.Button)) {
        ResetAll();
      }

      if (gui.SliderH(ref x, ref y, ref speed_, 1.0f, 100.0f, $"SPEED: {speed_:F1}")) {
        Core.ModConfig.Set("freecam_speed", speed_);
      }

      if (gui.SliderH(ref x, ref y, ref speedMultiplier_, 1.0f, 10.0f, $"SPEED MULTIPLIER: {speedMultiplier_:F1}")) {
        Core.ModConfig.Set("freecam_speed_multiplier", speedMultiplier_);
      }

      float fov = ActiveCamera?.Fov ?? 0.0f;
      if (gui.SliderH(ref x, ref y, ref fov, FovMin, FovMax, $"FOV: {fov:F1}")) {
        if (ActiveCamera != null) {
          ActiveCamera.Fov = fov;
        }
      }

      gui.Line(x, y, Gui.Width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      bool cameraOk = ActiveCamera != null && ActiveCamera != FreeCamera;
      bool guiEnabled = GUI.enabled;

      GUI.enabled = cameraOk;
      ActiveCamera?.GuiTransformAdjust(gui, ref x, ref y);
      GUI.enabled = guiEnabled;

      gui.Line(x, y, Gui.Width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (gui.Button(ref x, ref y, "RESET TRANSFORM", Skin.Button)) {
        ResetTransform();
      }

      gui.Line(x, y, Gui.Width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (gui.Button(ref x, ref y, "HIDE CARX UI", Core.HideCxUi ? Skin.ButtonActive : Skin.Button)) {
        Core.HideCxUi = !Core.HideCxUi;
        if (CinematicEnabled) {
          Core.ToggleCxUi(!Core.HideCxUi);
        }
      }

      float yAfterMain = y;
      y = yBegin;
      x += Gui.Width;

      x += Gui.OffsetGuiX;
      gui.Line(x, y, 1.0f, Core.GuiTabsHeight - Gui.OffsetY * 3.0f - EnableButtonOffset, Skin.SeparatorColor);
      x += Gui.OffsetGuiX;

      GUI.enabled = cameraOk;
      ActiveCamera?.GuiTransformMode(gui, ref x, ref y);
      GUI.enabled = guiEnabled;

      gui.Line(x, y, Gui.Width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (gui.Button(ref x, ref y, "SWITCH TO FREE CAMERA", Skin.Button)) {
        DisableAllCamerasBut(FreeCamTag);
        if (FreeCamera != null) {
          SetActiveCamera(FreeCamera);
        }
        else {
          ResetAll();
        }
      }

      if (gui.Button(ref x, ref y, "ADD CAMERA", Skin.Button)) {
        AddCamera();
      }

      GuiCameraList(gui, ref x, ref y);

      x = xBegin;
      y = y > yAfterMain ? y : yAfterMain;
    }

    private void GuiCameraList(Gui gui, ref float x, ref float y) {
      const float listHeight = 250.0f;
      gui.BeginScrollV(ref x, ref y, listHeight, ctListScrollH_, ref ctListScroll_, "CAMERAS");

      float sx = x;
      float sy = y;
      const float offset = Gui.ScrollBarWidth / 2.0f;
      bool scrollVisible = ctListScrollH_ > listHeight;
      float width = scrollVisible ? Gui.WidthScroll - offset : Gui.WidthScroll + offset;
      foreach (var cam in ctCameras_) {
        //break if cam was deleted
        if (!cam.OnGUI(gui, ref sx, ref sy, width)) {
          break;
        }
      }

      ctListScrollH_ = gui.EndScrollV(ref x, ref y, sx, sy);
      y += Gui.OffsetSmall;
    }
    #endregion

    #region animation gui
    private void GuiAnimationTab(Gui gui, ref float x, ref float y) {
      float yBegin = y;
      bool cameraOk = ActiveCamera != null && ActiveCamera != FreeCamera;

      bool old = GUI.enabled;
      GUI.enabled = cameraOk;

      string text = "SELECT CAMERA TO USE";
      bool playAnim = false;
      if (cameraOk) {
        playAnim = ActiveCamera.Animation.AllowPlay;
        text = playAnim ? "DISABLE" : "ENABLE";
      }
      if (gui.Button(ref x, ref y, text, playAnim ? Skin.ButtonActive : Skin.Button)) {
        if (cameraOk) {
          playAnim = !playAnim;
          ActiveCamera.Animation.AllowPlay = playAnim;
          Log.Write($"[KN_Cinematic]: Set animation for camera to '{playAnim}'");
        }
      }

      if (gui.Button(ref x, ref y, "RESET ANIMATION", Skin.Button)) {
        if (cameraOk) {
          ActiveCamera.RemoveAnimation();
          Log.Write($"[KN_Cinematic]: Reset animation for camera '{ActiveCamera.Tag}'");
        }
      }

      float currentLength = ActiveCamera?.Animation.ActualLength ?? 0.0f;
      animationScaleString_ = $"{currentLength:F}";
      if (gui.TextField(ref x, ref y, ref animationScaleString_, "LENGTH", 6, Config.FloatRegex)) {
        float.TryParse(animationScaleString_, out float scaledLength);
        if (cameraOk && scaledLength > 0.01f) {
          ActiveCamera.Animation.Scale(scaledLength);
          Log.Write($"[KN_Cinematic]: Scale animation for camera '{ActiveCamera.Tag}' ({currentLength:F} -> {scaledLength:F})");
        }
      }

      float beginTime = ActiveCamera?.Animation.BeginTime ?? 0.0f;
      animationBeginString_ = $"{beginTime:F}";
      if (gui.TextField(ref x, ref y, ref animationBeginString_, "BEGIN TIME", 6, Config.FloatRegex)) {
        float.TryParse(animationBeginString_, out float begin);
        if (cameraOk && begin >= 0.0f) {
          ActiveCamera.Animation.BeginTime = begin;
          Log.Write($"[KN_Cinematic]: Set animation begin time for camera '{ActiveCamera.Tag}' ({beginTime:F} -> {begin:F})");
        }
      }

      float smooth = ActiveCamera?.Animation.Smooth ?? 1.0f;
      if (gui.SliderH(ref x, ref y, Gui.Width, ref smooth, 1.0f, 20.0f, $"SMOOTHNESS: {smooth:F}", Skin.RedSkin)) {
        if (cameraOk) {
          ActiveCamera.Animation.Smooth = smooth;
          ActiveCamera.Animation.MakeAnimation();
        }
      }

      GUI.enabled = old;

      GuiKeyframesList(gui, ref x, ref y);

      y = yBegin;
      x += Gui.Width + 2.0f;

      x += Gui.OffsetGuiX;
      gui.Line(x, y, 1.0f, Core.GuiTabsHeight - Gui.OffsetY * 3.0f - EnableButtonOffset, Skin.SeparatorColor);
      x += Gui.OffsetGuiX;

      GuiKeyframeEdit(gui, ref x, ref y);
    }

    private void GuiKeyframesList(Gui gui, ref float x, ref float y) {
      const float listHeight = 300.0f;

      bool cameraOk = ActiveCamera != null && ActiveCamera != FreeCamera;

      gui.BeginScrollV(ref x, ref y, listHeight, kfListScrollH_, ref kfListScroll_, "KEYFRAMES");

      float sx = x;
      float sy = y;
      const float offset = Gui.ScrollBarWidth / 2.0f;
      bool scrollVisible = kfListScrollH_ > listHeight;
      float width = scrollVisible ? Gui.WidthScroll - offset : Gui.WidthScroll + offset;

      if (cameraOk) {
        foreach (var kf in ActiveCamera.Animation.Keyframes) {
          //break if keyframe was deleted
          if (!kf.OnGUI(gui, ref sx, ref sy, width)) {
            break;
          }
        }
      }

      kfListScrollH_ = gui.EndScrollV(ref x, ref y, sx, sy);
      y += Gui.OffsetSmall;
    }

    private void GuiKeyframeEdit(Gui gui, ref float x, ref float y) {
      bool cameraOk = ActiveCamera != null && ActiveCamera != FreeCamera;
      bool kfOk = cameraOk && CurrentKeyframe != null;
      bool kfEdit = kfOk && Core.Timeline.IsKeyframeEditing;
      bool lookAt = cameraOk && ActiveCamera.Target != null && ActiveCamera.LookAt;
      bool hookTo = cameraOk && ActiveCamera.Parent != null && ActiveCamera.HookTo;

      bool old = GUI.enabled;
      GUI.enabled = kfEdit;

      const float width = Gui.Width * 1.5f;

      gui.Box(x, y, width, Gui.Height, "KEYFRAME SETTINGS", Skin.MainContainerDark);
      y += Gui.Height + Gui.OffsetSmall;

      var pos = CurrentKeyframe?.Position ?? Vector3.zero;
      const float offset = 1.0f;

      bool oldGui = GUI.enabled;
      GUI.enabled = !hookTo && oldGui;
      if (gui.SliderH(ref x, ref y, width, ref pos.x, pos.x - offset, pos.x + offset, $"POSITION X: {pos.x:F}")) {
        if (kfOk) {
          ActiveCamera.GameObject.transform.position = pos;
          CurrentKeyframe.Position.x = pos.x;
          ActiveCamera.Animation.MakeAnimation();
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref pos.y, pos.y - offset, pos.y + offset, $"POSITION Y: {pos.y:F}")) {
        if (kfOk) {
          ActiveCamera.GameObject.transform.position = pos;
          CurrentKeyframe.Position.y = pos.y;
          ActiveCamera.Animation.MakeAnimation();
        }
      }

      if (gui.SliderH(ref x, ref y, width, ref pos.z, pos.z - offset, pos.z + offset, $"POSITION Z: {pos.z:F}")) {
        if (kfOk) {
          ActiveCamera.GameObject.transform.position = pos;
          CurrentKeyframe.Position.z = pos.z;
          ActiveCamera.Animation.MakeAnimation();
        }
      }
      GUI.enabled = oldGui;

      gui.Line(x, y, width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      oldGui = GUI.enabled;

      float time = 0.0f;
      if (CurrentKeyframe != null) {
        time = CurrentKeyframe.Time > 0.0f ? CurrentKeyframe.Time - 0.0001f : CurrentKeyframe.Time;
      }

      if (lookAt) {
        GUI.enabled = oldGui;
        float hx = CurrentKeyframe?.HeadingX ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref hx, hx - offset, hx + offset, $"HEADING X: {hx:F}")) {
          if (kfOk) {
            CurrentKeyframe.HeadingX = hx;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }

        float hy = CurrentKeyframe?.HeadingY ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref hy, hy - offset, hy + offset, $"HEADING Y: {hy:F}")) {
          if (kfOk) {
            CurrentKeyframe.HeadingY = hy;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }

        float hz = CurrentKeyframe?.HeadingZ ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref hz, hz - offset, hz + offset, $"HEADING Z: {hz:F}")) {
          if (kfOk) {
            CurrentKeyframe.HeadingZ = hz;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }
      }
      else {
        GUI.enabled = oldGui;
        float pitch = CurrentKeyframe?.Pitch ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref pitch, -360.0f, 360.0f, $"PITCH: {pitch:F}")) {
          if (kfOk) {
            CurrentKeyframe.Pitch = pitch;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }

        float yaw = CurrentKeyframe?.Yaw ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref yaw, -360.0f, 360.0f, $"YAW: {yaw:F}")) {
          if (kfOk) {
            CurrentKeyframe.Yaw = yaw;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }

        float roll = CurrentKeyframe?.Roll ?? 0.0f;
        if (gui.SliderH(ref x, ref y, width, ref roll, -360.0f, 360.0f, $"ROLL: {roll:F}")) {
          if (kfOk) {
            CurrentKeyframe.Roll = roll;
            ActiveCamera.Animation.MakeAnimation();
            ActiveCamera.UpdateAnimation(time);
          }
        }
      }
      GUI.enabled = oldGui;

      float fov = CurrentKeyframe?.Fov ?? 0.0f;
      if (gui.SliderH(ref x, ref y, width, ref fov, FovMin, FovMax, $"FOV: {fov:F}")) {
        if (kfOk) {
          CurrentKeyframe.Fov = fov;
          ActiveCamera.Fov = fov;
          ActiveCamera.Animation.MakeAnimation();
          ActiveCamera.UpdateAnimation(time);
        }
      }

      gui.Line(x, y, width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (gui.Button(ref x, ref y, width, Gui.Height, "DUPLICATE", Skin.Button)) {
        if (kfOk) {
          var copy = new CTKeyframe(CurrentKeyframe);
          CurrentKeyframe.Active = false;
          CurrentKeyframe = copy;
          ActiveCamera.Animation.Add(copy);
        }
      }

      GUI.enabled = old;
    }
    #endregion

    #region replay
    private void GuiReplay(Gui gui, ref float x, ref float y) {
      bool guiEnabled = GUI.enabled;
      GUI.enabled = !Core.IsInGarage;

      Core.Replay.OnGui(gui, ref x, ref y);

      gui.Line(x, y, Gui.Width, 1.0f, Skin.SeparatorColor);
      y += Gui.OffsetY;

      if (gui.Button(ref x, ref y, "FORCE PLAY / SP ONLY", Skin.Button)) {
        var gm = Object.FindObjectOfType<GameManager>();
        if (gm != null) {
          gm.SetGamePause(false);
          ActiveCamera.GameObject.GetComponent<Camera>().nearClipPlane = 0.1f;
          ActiveCamera.GameObject.GetComponent<Camera>().farClipPlane = 1000.0f;
        }
      }

      GUI.enabled = guiEnabled;
    }
    #endregion

    private void GuiSideBar(Gui gui, ref float x, ref float y) {
      float yBegin = y;

      x += Gui.OffsetSmall;
      if (gui.ImageButton(ref x, ref y, cameraTabActive_ ? Skin.IconCamActive : Skin.IconCam)) {
        cameraTabActive_ = true;
        animationTabActive_ = false;
        replayTabActive_ = false;
        Core.ShowCars = false;
        Core.FilePicker.IsPicking = false;
      }

      if (gui.ImageButton(ref x, ref y, animationTabActive_ ? Skin.IconAnimActive : Skin.IconAnim)) {
        cameraTabActive_ = false;
        animationTabActive_ = true;
        replayTabActive_ = false;
        Core.ShowCars = false;
        Core.FilePicker.IsPicking = false;
      }

      if (gui.ImageButton(ref x, ref y, replayTabActive_ ? Skin.IconReplayActive : Skin.IconReplay)) {
        cameraTabActive_ = false;
        animationTabActive_ = false;
        replayTabActive_ = true;
        Core.ShowCars = false;
        Core.FilePicker.IsPicking = false;
      }

      x += Gui.IconSize;
      y = yBegin;
    }
    #endregion

    #region timeline callbacks
    private void OnTimelineStop(float time) {
      if (ActiveCamera != null && ActiveCamera != FreeCamera) {
        ActiveCamera.UpdateAnimation(time);
      }

      Core.Replay.Stop(time);
    }

    private void OnTimelineDrag(float time) {
      if (ActiveCamera != null && ActiveCamera != FreeCamera) {
        ActiveCamera.UpdateAnimation(time);
      }

      Core.Replay.TimeUpdate(time, true);
    }

    private void OnTimelinePlay(bool play) {
      // if (activeCamera_ != null && activeCamera_ != freeCamera_) {
      //   if (activeCamera_.Animation.Keyframes.Count > 0) {
      //     activeCamera_.Animation.AllowPlay = play;
      //   }
      // }

      Core.Replay.PlayPause(play);
    }

    private void OnTimelineKeyframe(float time) {
      if (ActiveCamera != null) {
        if (ActiveCamera != FreeCamera) {
          ActiveCamera.MakeKeyframe(time);
        }
        else {
          AddCamera();
          ActiveCamera.MakeKeyframe(time);
        }

        cameraTabActive_ = false;
        animationTabActive_ = true;
        replayTabActive_ = false;
      }
    }

    private void OnTimelineKeyframeEdit(bool enabled) {
      if (ActiveCamera == null || ActiveCamera == FreeCamera) {
        return;
      }

      if (CurrentKeyframe == null) {
        Core.Timeline.IsKeyframeEditing = false;
        return;
      }

      cameraTabActive_ = false;
      animationTabActive_ = true;
      replayTabActive_ = false;
    }
    #endregion

    #region utils
    public void SetActiveCamera(CTCamera camera) {
      if (ActiveCamera != null) {
        ActiveCamera.Enabled = false;
      }

      ActiveCamera = camera ?? FreeCamera;
      if (ActiveCamera != null) {
        ActiveCamera.Enabled = true;
      }

      if (ActiveCamera != null && ActiveCamera != FreeCamera) {
        ActiveCamera.Animation.AllowPlay = false;
      }
    }

    private void ToggleCinematic() {
      ToggleCinematicCamera();

      if (Core.HideCxUi) {
        Core.ToggleCxUi(!CinematicEnabled);
      }
      else {
        if (!CinematicEnabled) {
          Core.ToggleCxUi(true);
        }
      }
    }

    private bool SetMainCamera(bool enabled) {
      MainCamera = GameObject.FindGameObjectWithTag(Config.CxMainCameraTag);
      if (MainCamera != null) {
        MainCamera.GetComponent<Camera>().enabled = enabled;
        MainCamera.GetComponent<StudioListener>().enabled = enabled;
        return true;
      }
      return false;
    }

    public void DisableAllCamerasBut(string tag) {
      foreach (var cam in ctCameras_) {
        if (cam.Tag != tag && cam.Enabled) {
          cam.Enabled = false;
          cam.Animation.AllowPlay = false;
        }
      }
    }

    private void AddCamera() {
      if (ActiveCamera != null) {
        var newCam = new CTCamera(this, ActiveCamera.GameObject, $"Camera_{ctCameraId_:D}");
        ctCameras_.Add(newCam);
        ++ctCameraId_;
        SetActiveCamera(newCam);

        Log.Write($"[KN_Cinematic]: New camera '{newCam.Tag}' was added");
      }
    }

    private void ToggleCinematicCamera() {
      if (CinematicEnabled) {
        if (SetMainCamera(false)) {
          if (FreeCamera == null) {
            FreeCamera = new CTCamera(this, MainCamera, FreeCamTag);
            Log.Write("[KN_Cinematic]: Free camera created");
            SetActiveCamera(FreeCamera);
          }
          else {
            SetActiveCamera(FreeCamera);
          }
        }
        else {
          Log.Write("[KN_Cinematic]: Unable to locate main camera");
        }
      }
      else {
        if (FreeCamera != null) {
          FreeCamera.Enabled = false;
        }

        if (ActiveCamera != null) {
          ActiveCamera.Enabled = false;
        }

        SetActiveCamera(null);
        SetMainCamera(true);
        if (FreeCamera != null) {
          FreeCamera.Enabled = false;
        }
      }
    }

    public void RemoveCamera(CTCamera camera) {
      if (camera == FreeCamera || camera == null) {
        return;
      }

      if (camera == ActiveCamera) {
        ActiveCamera = FreeCamera;
        ActiveCamera.Enabled = true;
      }
      camera.Enabled = false;
      camera.RemoveAnimation();
      camera.ResetState();
      ctCameras_.Remove(camera);
    }

    private void ResetTransform() {
      if (MainCamera == null || ActiveCamera == null) {
        return;
      }

      var transform = MainCamera.transform;
      ActiveCamera.GameObject.transform.position = transform.position;
      ActiveCamera.GameObject.transform.rotation = transform.rotation;
    }
    #endregion
  }
}