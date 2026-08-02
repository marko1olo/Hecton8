// ============================================================================
// HECTON-8 Гўв‚¬вЂќ PlayerActionController.cs
// ГђЕЎГђВѕГђВЅГ‘вЂљГ‘в‚¬ГђВѕГђВ»ГђВ»ГђВµГ‘в‚¬ ГђВѕГ‘вЂљГђВ»ГђВѕГђВ¶ГђВµГђВЅГђВЅГ‘вЂ№Г‘вЂ¦ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВ№ ГђВёГђВіГ‘в‚¬ГђВѕГђВєГђВ° (ГђВµГђВґГђВ°, ГђВјГђВµГђВґГђВёГђВєГђВ°ГђВјГђВµГђВЅГ‘вЂљГ‘вЂ№).
//
// ГђЕѕГђВўГђвЂ™ГђвЂўГђВўГђВЎГђВўГђвЂ™ГђвЂўГђВќГђВќГђЕѕГђВЎГђВўГђЛњ:
//   1. ГђвЂ”ГђВ°ГђВїГ‘Ж’Г‘ВЃГђВє Г‘вЂљГђВ°ГђВ№ГђВјГђВµГ‘в‚¬ГђВ° ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ (ГђВµГђВґГђВ° 1Г‘ВЃ, ГђВјГђВµГђВґГђВёГђВєГђВёГ‘вЂљ 3Г‘ВЃ).
//   2. ГђЕёГ‘Ж’ГђВ±ГђВ»ГђВёГђВєГђВ°Г‘вЂ ГђВёГ‘ВЏ ГђВїГ‘в‚¬ГђВѕГђВіГ‘в‚¬ГђВµГ‘ВЃГ‘ВЃГђВ° Г‘вЂЎГђВµГ‘в‚¬ГђВµГђВ· SignalBus (ГђВґГђВ»Г‘ВЏ UI).
//   3. ГђЕѕГђВ±Г‘в‚¬ГђВ°ГђВ±ГђВѕГ‘вЂљГђВєГђВ° ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГђВ№: ГђВґГђВІГђВёГђВ¶ГђВµГђВЅГђВёГђВµ, Г‘ВЃГђВјГђВµГђВЅГђВ° ГђВёГђВЅГ‘ВЃГ‘вЂљГ‘в‚¬Г‘Ж’ГђВјГђВµГђВЅГ‘вЂљГђВ°, Г‘Ж’Г‘в‚¬ГђВѕГђВЅ.
//   4. ГђвЂ”ГђВ°ГђВІГђВµГ‘в‚¬Г‘Л†ГђВµГђВЅГђВёГђВµ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ: ГђВІГ‘вЂ№ГђВ·ГђВѕГђВІ ConsumableItem.TryConsume().
//   5. ГђЕЎГђВ°ГђВјГђВµГ‘в‚¬ГђВЅГ‘вЂ№ГђВ№ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВє Г‘вЂЎГђВµГ‘в‚¬ГђВµГђВ· CameraJuiceProcessor (ГђВјГђВёГђВєГ‘в‚¬ГђВѕ-ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГђВµ).
//   6. ГђвЂ”ГђВІГ‘Ж’ГђВєГђВѕГђВІГђВѕГђВ№ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВє Г‘вЂЎГђВµГ‘в‚¬ГђВµГђВ· SpatialAudioManager.
//
// ZERO GC:
//   Гўв‚¬Вў ITickable state machine Гўв‚¬вЂќ ГђВЅГђВёГђВєГђВ°ГђВєГђВёГ‘вЂ¦ ГђВєГђВѕГ‘в‚¬Г‘Ж’Г‘вЂљГђВёГђВЅ.
//   Гўв‚¬Вў Pre-cached strings ГђВґГђВ»Г‘ВЏ UI.
//   Гўв‚¬Вў SignalBus ГђВґГђВ»Г‘ВЏ UI/Sound hooks Гўв‚¬вЂќ ГђВґГђВёГђВ·ГђВ°ГђВ№ГђВЅГђВµГ‘в‚¬Г‘вЂ№ ГђВЅГђВµ Г‘вЂљГ‘в‚¬ГђВѕГђВіГђВ°Г‘ЕЅГ‘вЂљ ГђВєГђВѕГђВґ.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Inventory;
using Hecton8.Items;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// ГђЕЎГђВѕГђВЅГ‘вЂљГ‘в‚¬ГђВѕГђВ»ГђВ»ГђВµГ‘в‚¬ ГђВѕГ‘вЂљГђВ»ГђВѕГђВ¶ГђВµГђВЅГђВЅГ‘вЂ№Г‘вЂ¦ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВ№ ГђВёГђВіГ‘в‚¬ГђВѕГђВєГђВ°.
    /// ГђВЈГђВїГ‘в‚¬ГђВ°ГђВІГђВ»Г‘ВЏГђВµГ‘вЂљ Г‘вЂљГђВ°ГђВ№ГђВјГђВµГ‘в‚¬ГђВѕГђВј, ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГ‘ВЏГђВјГђВё ГђВё ГђВ·ГђВ°ГђВІГђВµГ‘в‚¬Г‘Л†ГђВµГђВЅГђВёГђВµГђВј ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9920)]
    public sealed class PlayerActionController : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IPlayerActionInterruptSink, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private static int s_x001PlayerActionControllerSignalPushDropCount;
        private const float TwoPi = 6.28318530718f;
        private const uint KccVelocityInterruptMaxAgeFrames = 12u;
        private const byte ActionAudioClipNone = 0;
        private const byte ActionAudioClipEating = 1;
        private const byte ActionAudioClipHealing = 2;
        private const byte ActionAudioClipCancel = 3;
        private const byte ActionAudioClipItemUseSound = 4;
        private const byte ActionCameraBobCommandNone = 0;
        private const byte ActionCameraBobCommandApply = 1;
        private const byte ActionCameraBobCommandClear = 2;

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ActionAudioRequest
        {
            [FieldOffset(0)] public Vector3 Position;
            [FieldOffset(12)] public uint EventId;
            [FieldOffset(16)] public uint ItemHash;
            [FieldOffset(20)] public byte ClipKind;
            [FieldOffset(21)] public byte Dirty;
            [FieldOffset(22)] public ushort Reserved0;
            [FieldOffset(24)] public uint Reserved1;
            [FieldOffset(28)] public uint Reserved2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct ActionCameraBobRequest
        {
            [FieldOffset(0)] public float Intensity;
            [FieldOffset(4)] public float Frequency;
            [FieldOffset(8)] public byte Command;
            [FieldOffset(9)] public byte Reserved0;
            [FieldOffset(10)] public ushort Reserved1;
            [FieldOffset(12)] public uint Reserved2;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  SINGLETON
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  INSPECTOR
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        [Header("ГўвЂќв‚¬ГўвЂќв‚¬ Interrupt Settings ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬")]
        [Tooltip("ГђЕ“ГђВёГђВЅГђВёГђВјГђВ°ГђВ»Г‘Е’ГђВЅГђВ°Г‘ВЏ Г‘ВЃГђВєГђВѕГ‘в‚¬ГђВѕГ‘ВЃГ‘вЂљГ‘Е’ ГђВґГђВІГђВёГђВ¶ГђВµГђВЅГђВёГ‘ВЏ ГђВґГђВ»Г‘ВЏ ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.")]
        [SerializeField] private float movementInterruptThreshold = 2f;

        [Header("ГўвЂќв‚¬ГўвЂќв‚¬ Camera Juice ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬")]
        [Tooltip("ГђВЎГ‘ВЃГ‘вЂ№ГђВ»ГђВєГђВ° ГђВЅГђВ° CameraJuiceProcessor ГђВґГђВ»Г‘ВЏ ГђВІГђВёГђВ·Г‘Ж’ГђВ°ГђВ»Г‘Е’ГђВЅГђВѕГђВіГђВѕ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВєГђВ°.")]
        [SerializeField] private CameraJuiceProcessor cameraJuiceProcessor;

        [Tooltip("ГђЛњГђВЅГ‘вЂљГђВµГђВЅГ‘ВЃГђВёГђВІГђВЅГђВѕГ‘ВЃГ‘вЂљГ‘Е’ ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВєГђВ°ГђВјГђВµГ‘в‚¬Г‘вЂ№ ГђВІГђВѕ ГђВІГ‘в‚¬ГђВµГђВјГ‘ВЏ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.")]
        [SerializeField, Range(0f, 0.02f)] private float actionCameraBobIntensity = 0.008f;

        [Tooltip("ГђВ§ГђВ°Г‘ВЃГ‘вЂљГђВѕГ‘вЂљГђВ° ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВєГђВ°ГђВјГђВµГ‘в‚¬Г‘вЂ№ (Г‘вЂ ГђВёГђВєГђВ»ГђВѕГђВІ ГђВІ Г‘ВЃГђВµГђВєГ‘Ж’ГђВЅГђВґГ‘Ж’).")]
        [SerializeField, Range(0.5f, 3f)] private float actionCameraBobFrequency = 1.5f;

        [Header("ГўвЂќв‚¬ГўвЂќв‚¬ Audio ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬ГўвЂќв‚¬")]
        [Tooltip("ГђвЂ”ГђВІГ‘Ж’ГђВє ГђВїГђВѕГђВµГђВґГђВ°ГђВЅГђВёГ‘ВЏ ГђВµГђВґГ‘вЂ№.")]
        [SerializeField] private AudioClip eatingSound;

        [Tooltip("ГђвЂ”ГђВІГ‘Ж’ГђВє ГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВјГђВµГђВґГђВёГђВєГђВ°ГђВјГђВµГђВЅГ‘вЂљГђВѕГђВІ.")]
        [SerializeField] private AudioClip healingSound;

        [Tooltip("ГђвЂ”ГђВІГ‘Ж’ГђВє ГђВѕГ‘вЂљГђВјГђВµГђВЅГ‘вЂ№ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.")]
        [SerializeField] private AudioClip cancelSound;

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  SIGNAL OUTPUT
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  STATE MACHINE
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private enum ActionState
        {
            Idle,
            InProgress
        }

        private ActionState _state = ActionState.Idle;
        private ItemData _activeItem;
        private int _inventoryAnchorX = -1;  // Inventory position for atomic removal
        private int _inventoryAnchorY = -1;
        private float _actionTimer;
        private float _actionDuration;
        private int _lastToolSlotIndex = -1;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _isInitialized;
        private bool _runtimeOwnerAborted;
        private float _cameraBobPhase;
        private ActionAudioRequest _pendingActionAudio;
        private ActionCameraBobRequest _pendingActionCameraBob;

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  CACHED REFERENCES
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _toolManager;
        private HectonSurvivalSystem _survivalSystem;
        private Transform _cachedTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IPlayerInventoryService _playerInventoryService;
        private IAudioService _audioService;

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PUBLIC API
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>ГђЛњГђВґГ‘вЂГ‘вЂљ ГђВ»ГђВё Г‘ВЃГђВµГђВ№Г‘вЂЎГђВ°Г‘ВЃ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ.</summary>
        public bool IsActionInProgress => _state == ActionState.InProgress;

        public bool IsInitialized =>
            !_runtimeOwnerAborted &&
            _isInitialized &&
            _serviceRegistered &&
            isActiveAndEnabled &&
            ReferenceEquals(GlobalRegistry.PlayerActions, this);

        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => IsInitialized;

        /// <summary>ГђВўГђВµГђВєГ‘Ж’Г‘вЂ°ГђВёГђВ№ ГђВїГ‘в‚¬ГђВѕГђВіГ‘в‚¬ГђВµГ‘ВЃГ‘ВЃ (0-1).</summary>
        public float Progress => ResolveProgress01();

        /// <summary>ГђВђГђВєГ‘вЂљГђВёГђВІГђВЅГ‘вЂ№ГђВ№ ГђВїГ‘в‚¬ГђВµГђВґГђВјГђВµГ‘вЂљ (null ГђВµГ‘ВЃГђВ»ГђВё ГђВЅГђВµГ‘вЂљ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ).</summary>
        public ItemData ActiveItem => _activeItem;

        internal static PlayerActionController ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
            s_x001PlayerActionControllerSignalPushDropCount = 0;
        }

        public static PlayerActionController EnsureRuntimeInstance()
        {
            PlayerActionController runtime = ResolveUsableRuntime();
            if (runtime != null)
                return runtime;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Delayed player action/audio service must exist when bootstrap registration is reordered.
            GameObject runtimeRoot = new GameObject("[PlayerActionController]"); // COLD ALLOC: GameObject[1] - bootstrap-owned delayed player action/audio service root - owner: PlayerActionController
            return runtimeRoot.AddComponent<PlayerActionController>();
        }

        public void InitializeService()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegisterHotSwap();
            TryRegister();
            CacheRegistryServicesCold();
        }

        /// <summary>
        /// ГђвЂ”ГђВ°ГђВїГ‘Ж’Г‘ВЃГђВєГђВ°ГђВµГ‘вЂљ ГђВѕГ‘вЂљГђВ»ГђВѕГђВ¶ГђВµГђВЅГђВЅГђВѕГђВµ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ.
        /// </summary>
        /// <param name="item">ГђЕёГ‘в‚¬ГђВµГђВґГђВјГђВµГ‘вЂљ ГђВґГђВ»Г‘ВЏ ГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГ‘ВЏ.</param>
        /// <returns>true ГђВµГ‘ВЃГђВ»ГђВё ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ ГђВ·ГђВ°ГђВїГ‘Ж’Г‘вЂ°ГђВµГђВЅГђВѕ.</returns>
        public bool StartAction(ItemData item)
        {
            return StartAction(item, -1, -1);
        }

        /// <summary>
        /// ГђвЂ”ГђВ°ГђВїГ‘Ж’Г‘ВЃГђВєГђВ°ГђВµГ‘вЂљ ГђВѕГ‘вЂљГђВ»ГђВѕГђВ¶ГђВµГђВЅГђВЅГђВѕГђВµ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ Г‘ВЃ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВµГђВ№ ГђВІ ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬ГђВµ ГђВґГђВ»Г‘ВЏ ГђВ°Г‘вЂљГђВѕГђВјГђВ°Г‘в‚¬ГђВЅГђВѕГђВіГђВѕ Г‘Ж’ГђВґГђВ°ГђВ»ГђВµГђВЅГђВёГ‘ВЏ.
        /// </summary>
        /// <param name="item">ГђЕёГ‘в‚¬ГђВµГђВґГђВјГђВµГ‘вЂљ ГђВґГђВ»Г‘ВЏ ГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГ‘ВЏ.</param>
        /// <param name="anchorX">X ГђВєГђВѕГђВѕГ‘в‚¬ГђВґГђВёГђВЅГђВ°Г‘вЂљГђВ° ГђВІ ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬ГђВµ (-1 ГђВµГ‘ВЃГђВ»ГђВё ГђВЅГђВµ ГђВёГђВ· ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬Г‘ВЏ).</param>
        /// <param name="anchorY">Y ГђВєГђВѕГђВѕГ‘в‚¬ГђВґГђВёГђВЅГђВ°Г‘вЂљГђВ° ГђВІ ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬ГђВµ (-1 ГђВµГ‘ВЃГђВ»ГђВё ГђВЅГђВµ ГђВёГђВ· ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬Г‘ВЏ).</param>
        /// <returns>true ГђВµГ‘ВЃГђВ»ГђВё ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ ГђВ·ГђВ°ГђВїГ‘Ж’Г‘вЂ°ГђВµГђВЅГђВѕ.</returns>
        public bool StartAction(ItemData item, int anchorX, int anchorY)
        {
            RefreshPlayerOwnedReferencesCold();

            if (item == null) return false;
            if (_state == ActionState.InProgress) return false;
            if (!CanUseInventoryAnchor(anchorX, anchorY, item)) return false;
            if (!CanApplyConsumableEffects(item)) return false;

            if (item.UseDuration <= 0f)
            {
                // ГђЕ“ГђВіГђВЅГђВѕГђВІГђВµГђВЅГђВЅГђВѕГђВµ ГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГђВµ - Г‘Ж’ГђВґГђВ°ГђВ»Г‘ВЏГђВµГђВј ГђВёГђВ· ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬Г‘ВЏ ГђВµГ‘ВЃГђВ»ГђВё ГђВµГ‘ВЃГ‘вЂљГ‘Е’ ГђВєГђВѕГђВѕГ‘в‚¬ГђВґГђВёГђВЅГђВ°Г‘вЂљГ‘вЂ№
                if (HasInventoryAnchor(anchorX, anchorY) && !TryRemoveItemFromInventory(anchorX, anchorY, item))
                    return false;

                ConsumableItem.TryConsumeWithoutAudio(item, _survivalSystem);
                PlayCompletionSound(item);
                PublishActionCompleted(item, anchorX, anchorY);
                return true;
            }

            _activeItem = item;
            _inventoryAnchorX = anchorX;
            _inventoryAnchorY = anchorY;
            _actionDuration = item.UseDuration;
            _actionTimer = 0f;
            _state = ActionState.InProgress;
            _cameraBobPhase = 0f;

            // ГђвЂ”ГђВ°ГђВїГђВѕГђВјГђВёГђВЅГђВ°ГђВµГђВј Г‘вЂљГђВµГђВєГ‘Ж’Г‘вЂ°ГђВёГђВ№ Г‘ВЃГђВ»ГђВѕГ‘вЂљ ГђВёГђВЅГ‘ВЃГ‘вЂљГ‘в‚¬Г‘Ж’ГђВјГђВµГђВЅГ‘вЂљГђВ° ГђВґГђВ»Г‘ВЏ ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГ‘ВЏ
            _lastToolSlotIndex = _toolManager != null ? _toolManager.CurrentSlotIndex : -1;

            return true;
        }

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВёГђВЅГ‘Ж’ГђВґГђВёГ‘вЂљГђВµГђВ»Г‘Е’ГђВЅГђВѕ ГђВѕГ‘вЂљГђВјГђВµГђВЅГ‘ВЏГђВµГ‘вЂљ Г‘вЂљГђВµГђВєГ‘Ж’Г‘вЂ°ГђВµГђВµ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГђВµ.
        /// ГђЕёГ‘в‚¬ГђВµГђВґГђВјГђВµГ‘вЂљ ГђВѕГ‘ВЃГ‘вЂљГђВ°Г‘вЂГ‘вЂљГ‘ВЃГ‘ВЏ ГђВІ ГђВёГђВЅГђВІГђВµГђВЅГ‘вЂљГђВ°Г‘в‚¬ГђВµ (atomicity).
        /// </summary>
        public void CancelAction()
        {
            if (_state != ActionState.InProgress) return;

            ItemData cancelledItem = _activeItem;
            float cancelledProgress = ResolveProgress01();

            _state = ActionState.Idle;
            _activeItem = null;
            _inventoryAnchorX = -1;
            _inventoryAnchorY = -1;
            _actionTimer = 0f;
            _actionDuration = 0f;

            // ГђЕѕГ‘вЂЎГђВёГ‘вЂ°ГђВ°ГђВµГђВј ГђВєГђВ°ГђВјГђВµГ‘в‚¬ГђВЅГ‘вЂ№ГђВ№ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВє
            QueueActionCameraBobClear();

            PlayCancelSound();
            PublishActionCancelled(cancelledItem, cancelledProgress, PlayerActionCancelledSignal.ReasonGeneric);
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  LIFECYCLE
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private void Awake()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            _cachedTransform = transform;

            // ГђЕЎГ‘ВЌГ‘Л†ГђВёГ‘в‚¬Г‘Ж’ГђВµГђВј Г‘ВЃГ‘ВЃГ‘вЂ№ГђВ»ГђВєГђВё
            TryGetComponent(out _playerMovement);
            TryGetComponent(out _toolManager);
            CacheRegistryServicesCold();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || !EnsureSingletonOwnership())
                return;

            // ГђЕЎГ‘ВЌГ‘Л†ГђВёГ‘в‚¬Г‘Ж’ГђВµГђВј SurvivalSystem
            if (_survivalSystem == null)
                TryGetComponent(out _survivalSystem);

            ConsumableItem.BindSurvivalSystemCold(_survivalSystem);
            if (!TryRegisterService())
                return;

            _isInitialized = true;
            TryRegister();
            TryRegisterHotSwap();
            CacheRegistryServicesCold();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(ActiveRuntimeInstance, this))
                    ActiveRuntimeInstance = null;

                return;
            }

            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    RefreshPlayerOwnedReferencesCold();
                    break;
                case GlobalRegistryServiceSlot.PlayerInventory:
                    CachePlayerInventoryService(currentService as IPlayerInventoryService);
                    RefreshPlayerOwnedReferencesCold();
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheAudioService(currentService as IAudioService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister(clearQueuedPresentation: false);
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  ITickable
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        public void Tick(float deltaTime)
        {
            if (_state != ActionState.InProgress) return;

            float safeDeltaTime = math.max(0f, deltaTime);

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђЕёГ‘в‚¬ГђВѕГђВІГђВµГ‘в‚¬ГђВєГђВ° ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГђВ№ ГўвЂќв‚¬ГўвЂќв‚¬
            if (CheckInterrupts())
            {
                CancelAction();
                return;
            }

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђЕѕГђВ±ГђВЅГђВѕГђВІГђВ»ГђВµГђВЅГђВёГђВµ Г‘вЂљГђВ°ГђВ№ГђВјГђВµГ‘в‚¬ГђВ° ГўвЂќв‚¬ГўвЂќв‚¬
            _actionTimer += safeDeltaTime;

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђЕЎГђВ°ГђВјГђВµГ‘в‚¬ГђВЅГ‘вЂ№ГђВ№ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВє (ГђВјГђВёГђВєГ‘в‚¬ГђВѕ-ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГђВµ) ГўвЂќв‚¬ГўвЂќв‚¬
            ApplyCameraJuice(safeDeltaTime);

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђЕёГ‘Ж’ГђВ±ГђВ»ГђВёГђВєГђВ°Г‘вЂ ГђВёГ‘ВЏ ГђВїГ‘в‚¬ГђВѕГђВіГ‘в‚¬ГђВµГ‘ВЃГ‘ВЃГђВ° ГўвЂќв‚¬ГўвЂќв‚¬
            float progress = ResolveProgress01();
            PublishActionProgress(progress);

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђвЂ”ГђВ°ГђВІГђВµГ‘в‚¬Г‘Л†ГђВµГђВЅГђВёГђВµ ГўвЂќв‚¬ГўвЂќв‚¬
            if (_actionTimer >= _actionDuration)
            {
                CompleteAction();
            }
        }

        public void LateFrameTick()
        {
            FlushQueuedActionCameraBob();
            FlushQueuedActionAudio();
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PRIVATE Гўв‚¬вЂќ CAMERA JUICE
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВёГђВјГђВµГђВЅГ‘ВЏГђВµГ‘вЂљ ГђВјГђВёГђВєГ‘в‚¬ГђВѕ-ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГђВµ ГђВєГђВ°ГђВјГђВµГ‘в‚¬Г‘вЂ№ ГђВІГђВѕ ГђВІГ‘в‚¬ГђВµГђВјГ‘ВЏ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.
        /// ГђЛњГђВјГђВёГ‘вЂљГђВёГ‘в‚¬Г‘Ж’ГђВµГ‘вЂљ ГђВґГђВІГђВёГђВ¶ГђВµГђВЅГђВёГђВµ Г‘в‚¬Г‘Ж’ГђВє ГђВїГђВµГ‘в‚¬Г‘ВЃГђВѕГђВЅГђВ°ГђВ¶ГђВ°.
        /// </summary>
        private void ApplyCameraJuice(float deltaTime)
        {
            // ГђВЎГђВёГђВЅГ‘Ж’Г‘ВЃГђВѕГђВёГђВґГђВ°ГђВ»Г‘Е’ГђВЅГђВѕГђВµ ГђВїГђВѕГђВєГђВ°Г‘вЂЎГђВёГђВІГђВ°ГђВЅГђВёГђВµ Г‘ВЃ ГђВ·ГђВ°Г‘вЂљГ‘Ж’Г‘вЂ¦ГђВ°ГђВЅГђВёГђВµГђВј ГђВє ГђВєГђВѕГђВЅГ‘вЂ Г‘Ж’ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ
            _cameraBobPhase += deltaTime * actionCameraBobFrequency * TwoPi;

            float progress = ResolveProgress01();
            float fadeOut = 1f - (progress * progress); // ГђЕЎГђВІГђВ°ГђВґГ‘в‚¬ГђВ°Г‘вЂљГђВёГ‘вЂЎГђВЅГђВѕГђВµ ГђВ·ГђВ°Г‘вЂљГ‘Ж’Г‘вЂ¦ГђВ°ГђВЅГђВёГђВµ

            // ГђВ ГђВµГђВіГђВёГ‘ВЃГ‘вЂљГ‘в‚¬ГђВёГ‘в‚¬Г‘Ж’ГђВµГђВј ГђВјГђВёГђВєГ‘в‚¬ГђВѕ-bob ГђВєГђВ°ГђВ¶ГђВґГ‘вЂ№ГђВ№ ГђВєГђВ°ГђВґГ‘в‚¬ Г‘ВЃ ГђВ·ГђВ°Г‘вЂљГ‘Ж’Г‘вЂ¦ГђВ°Г‘ЕЅГ‘вЂ°ГђВµГђВ№ ГђВёГђВЅГ‘вЂљГђВµГђВЅГ‘ВЃГђВёГђВІГђВЅГђВѕГ‘ВЃГ‘вЂљГ‘Е’Г‘ЕЅ
            float intensity = actionCameraBobIntensity * fadeOut;
            QueueActionCameraBob(intensity, actionCameraBobFrequency);
        }

        private float ResolveProgress01()
        {
            return _state == ActionState.InProgress && _actionDuration > 0.0001f
                ? math.saturate(_actionTimer * math.rcp(_actionDuration))
                : 0f;
        }

        private void PublishActionProgress(float progress01)
        {
            ItemData item = _activeItem;
            PlayerActionProgressSignal signal = new PlayerActionProgressSignal
            {
                Progress01 = math.saturate(progress01),
                ItemHash = ResolveItemHash(item),
                Frame = SystemDispatcher.CurrentFrameId,
                ActiveToolSlot = PackActiveToolSlot(_lastToolSlotIndex),
                ActionKind = ResolveActionKind(item),
                Flags = item != null ? PlayerActionProgressSignal.FlagHasItem : (byte)0
            };

            SignalBus<PlayerActionProgressSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
        }

        private void PublishActionCompleted(ItemData item, int anchorX, int anchorY)
        {
            byte flags = item != null ? PlayerActionCompletedSignal.FlagHasItem : (byte)0;
            if (anchorX >= 0 && anchorY >= 0)
                flags |= PlayerActionCompletedSignal.FlagInventoryAnchorValid;

            PlayerActionCompletedSignal signal = new PlayerActionCompletedSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = SystemDispatcher.CurrentFrameId,
                InventoryAnchorX = PackInventoryAnchor(anchorX),
                InventoryAnchorY = PackInventoryAnchor(anchorY),
                ActionKind = ResolveActionKind(item),
                Flags = flags
            };

            SignalBus<PlayerActionCompletedSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
        }

        private void PublishActionCancelled(ItemData item, float progress01, byte reason)
        {
            PlayerActionCancelledSignal signal = new PlayerActionCancelledSignal
            {
                ItemHash = ResolveItemHash(item),
                Frame = SystemDispatcher.CurrentFrameId,
                Progress01 = math.saturate(progress01),
                ActionKind = ResolveActionKind(item),
                Reason = reason,
                Flags = item != null ? PlayerActionCancelledSignal.FlagHasItem : (byte)0
            };

            SignalBus<PlayerActionCancelledSignal>.TryPushTracked(in signal, ref s_x001PlayerActionControllerSignalPushDropCount);
        }

        private static uint ResolveItemHash(ItemData item)
        {
            return item != null ? unchecked((uint)item.PersistentHashId) : 0u;
        }

        private static byte ResolveActionKind(ItemData item)
        {
            if (item == null)
                return PlayerActionProgressSignal.ActionKindGeneric;
            if (item.integrityRestore > 0f)
                return PlayerActionProgressSignal.ActionKindMedical;
            if (item.oxygenRestore > 0f)
                return PlayerActionProgressSignal.ActionKindOxygen;
            if (item.hungerRestore > 0f || item.thirstRestore > 0f)
                return PlayerActionProgressSignal.ActionKindFood;
            return PlayerActionProgressSignal.ActionKindGeneric;
        }

        private static ushort PackInventoryAnchor(int anchor)
        {
            return anchor >= 0 ? (ushort)math.min(anchor, ushort.MaxValue - 1) : ushort.MaxValue;
        }

        private static ushort PackActiveToolSlot(int slotIndex)
        {
            return slotIndex >= 0 ? (ushort)math.min(slotIndex, ushort.MaxValue - 1) : ushort.MaxValue;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PRIVATE Гўв‚¬вЂќ INTERRUPTS
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВѕГђВІГђВµГ‘в‚¬Г‘ВЏГђВµГ‘вЂљ Г‘Ж’Г‘ВЃГђВ»ГђВѕГђВІГђВёГ‘ВЏ ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВґГђВµГђВ№Г‘ВЃГ‘вЂљГђВІГђВёГ‘ВЏ.
        /// </summary>
        private bool CheckInterrupts()
        {
            // ГўвЂќв‚¬ГўвЂќв‚¬ 1. ГђЕёГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГђВµ ГђВїГђВѕ ГђВґГђВІГђВёГђВ¶ГђВµГђВЅГђВёГ‘ЕЅ ГўвЂќв‚¬ГўвЂќв‚¬
            if (TryResolveKccVelocity(out Vector3 velocity))
            {
                float speedSqr = velocity.x * velocity.x + velocity.y * velocity.y + velocity.z * velocity.z;
                float interruptThresholdSqr = movementInterruptThreshold * movementInterruptThreshold;
                if (speedSqr > interruptThresholdSqr)
                    return true;
            }

            // ГўвЂќв‚¬ГўвЂќв‚¬ 2. ГђЕёГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГђВµ ГђВїГђВѕ Г‘ВЃГђВјГђВµГђВЅГђВµ ГђВёГђВЅГ‘ВЃГ‘вЂљГ‘в‚¬Г‘Ж’ГђВјГђВµГђВЅГ‘вЂљГђВ° ГўвЂќв‚¬ГўвЂќв‚¬
            if (_toolManager != null && _lastToolSlotIndex >= 0)
            {
                if (_toolManager.CurrentSlotIndex != _lastToolSlotIndex)
                    return true;
            }

            return false;
        }

        private static bool TryResolveKccVelocity(out Vector3 velocity)
        {
            velocity = Vector3.zero;
            if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) || signal.Sequence == 0u)
                return false;

            uint currentFrame = SystemDispatcher.CurrentFrameId;
            uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
            if (currentFrame != 0u &&
                signalFrame != 0u &&
                (signalFrame > currentFrame || currentFrame - signalFrame > KccVelocityInterruptMaxAgeFrames))
            {
                return false;
            }

            float3 value = signal.Velocity;
            if (!math.all(math.isfinite(value)))
                return false;

            velocity = new Vector3(value.x, value.y, value.z);
            return true;
        }

        /// <summary>
        /// ГђвЂ™ГђВЅГђВµГ‘Л†ГђВЅГђВёГђВ№ ГђВјГђВµГ‘вЂљГђВѕГђВґ ГђВґГђВ»Г‘ВЏ ГђВїГ‘в‚¬ГђВµГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВЅГђВёГ‘ВЏ ГђВїГђВѕ Г‘Ж’Г‘в‚¬ГђВѕГђВЅГ‘Ж’.
        /// ГђвЂ™Г‘вЂ№ГђВ·Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљГ‘ВЃГ‘ВЏ ГђВёГђВ· HectonSurvivalSystem ГђВёГђВ»ГђВё ГђВґГ‘в‚¬Г‘Ж’ГђВіГђВёГ‘вЂ¦ Г‘ВЃГђВёГ‘ВЃГ‘вЂљГђВµГђВј.
        /// </summary>
        public void OnDamageTaken()
        {
            if (_state == ActionState.InProgress)
                CancelAction();
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PRIVATE Гўв‚¬вЂќ COMPLETION
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private void CompleteAction()
        {
            ItemData completedItem = _activeItem;
            int anchorX = _inventoryAnchorX;
            int anchorY = _inventoryAnchorY;

            _state = ActionState.Idle;
            _activeItem = null;
            _inventoryAnchorX = -1;
            _inventoryAnchorY = -1;
            _actionTimer = 0f;
            _actionDuration = 0f;

            // ГђЕѕГ‘вЂЎГђВёГ‘вЂ°ГђВ°ГђВµГђВј ГђВєГђВ°ГђВјГђВµГ‘в‚¬ГђВЅГ‘вЂ№ГђВ№ Г‘вЂћГђВёГђВґГђВ±ГђВµГђВє
            QueueActionCameraBobClear();

            // ГўвЂќв‚¬ГўвЂќв‚¬ ATOMIC: Remove item from inventory ONLY on completion ГўвЂќв‚¬ГўвЂќв‚¬
            if (completedItem != null)
            {
                RefreshPlayerOwnedReferencesCold();

                if (!CanApplyConsumableEffects(completedItem))
                {
                    PublishActionCancelled(completedItem, 1f, PlayerActionCancelledSignal.ReasonGeneric);
                    return;
                }

                if (HasInventoryAnchor(anchorX, anchorY) && !TryRemoveItemFromInventory(anchorX, anchorY, completedItem))
                {
                    PublishActionCancelled(completedItem, 1f, PlayerActionCancelledSignal.ReasonGeneric);
                    return;
                }

                ConsumableItem.TryConsumeWithoutAudio(completedItem, _survivalSystem);
                PlayCompletionSound(completedItem);
            }

            PublishActionCompleted(completedItem, anchorX, anchorY);
        }

        /// <summary>
        /// Removes one item from inventory at the specified position.
        /// Called only on successful action completion (atomicity).
        /// </summary>
        private bool TryRemoveItemFromInventory(int anchorX, int anchorY, ItemData expectedItem)
        {
            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null)
                return false;

            int expectedHash = expectedItem != null ? expectedItem.PersistentHashId : 0;
            if (expectedHash != 0 && inventory.GetItemHashAt(anchorX, anchorY) != expectedHash)
                return false;

            int removedHash = inventory.RemoveOneItem(anchorX, anchorY);
            return removedHash != 0 && (expectedHash == 0 || removedHash == expectedHash);
        }

        private bool CanUseInventoryAnchor(int anchorX, int anchorY, ItemData expectedItem)
        {
            if (!HasInventoryAnchor(anchorX, anchorY))
                return true;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null)
                return false;

            int expectedHash = expectedItem != null ? expectedItem.PersistentHashId : 0;
            return expectedHash == 0 || inventory.GetItemHashAt(anchorX, anchorY) == expectedHash;
        }

        private bool CanApplyConsumableEffects(ItemData item)
        {
            return item == null ||
                   !item.isConsumable ||
                   !ConsumableItem.HasAnyEffect(item) ||
                   _survivalSystem != null;
        }

        private static bool HasInventoryAnchor(int anchorX, int anchorY)
        {
            return anchorX >= 0 && anchorY >= 0;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PRIVATE Гўв‚¬вЂќ AUDIO
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private void PlayCompletionSound(ItemData item)
        {
            if (item == null) return;

            AudioClip clip = null;
            byte clipKind = ActionAudioClipNone;
            uint eventId = item.UseAudioEventId;
            uint itemHash = ResolveItemHash(item);

            // ГђЕѕГђВїГ‘в‚¬ГђВµГђВґГђВµГђВ»Г‘ВЏГђВµГђВј Г‘вЂљГђВёГђВї ГђВ·ГђВІГ‘Ж’ГђВєГђВ° ГђВїГђВѕ Г‘ВЌГ‘вЂћГ‘вЂћГђВµГђВєГ‘вЂљГђВ°ГђВј ГђВїГ‘в‚¬ГђВµГђВґГђВјГђВµГ‘вЂљГђВ°
            if (item.integrityRestore > 0f)
            {
                clip = healingSound;
                clipKind = ActionAudioClipHealing;
            }
            else if (item.hungerRestore > 0f || item.thirstRestore > 0f)
            {
                clip = eatingSound;
                clipKind = ActionAudioClipEating;
            }
            else if (item.useSound != null)
            {
                clip = item.useSound;
                clipKind = ActionAudioClipItemUseSound;
            }

            if (clip == null && eventId == 0u) return;

            if (ResolveAudioService() != null && _cachedTransform != null)
                QueueActionAudio(clipKind, eventId, itemHash, _cachedTransform.position);
        }

        private void QueueActionCameraBob(float intensity, float frequency)
        {
            if (intensity <= 0f)
                return;

            _pendingActionCameraBob.Intensity = intensity;
            _pendingActionCameraBob.Frequency = frequency;
            _pendingActionCameraBob.Command = ActionCameraBobCommandApply;
            _pendingActionCameraBob.Reserved0 = 0;
            _pendingActionCameraBob.Reserved1 = 0;
            _pendingActionCameraBob.Reserved2 = 0u;
        }

        private void QueueActionCameraBobClear()
        {
            _pendingActionCameraBob.Intensity = 0f;
            _pendingActionCameraBob.Frequency = 0f;
            _pendingActionCameraBob.Command = ActionCameraBobCommandClear;
            _pendingActionCameraBob.Reserved0 = 0;
            _pendingActionCameraBob.Reserved1 = 0;
            _pendingActionCameraBob.Reserved2 = 0u;
        }

        private void FlushQueuedActionCameraBob()
        {
            if (_pendingActionCameraBob.Command == ActionCameraBobCommandNone)
                return;

            ActionCameraBobRequest request = _pendingActionCameraBob;
            _pendingActionCameraBob = default;

            CameraJuiceProcessor processor = cameraJuiceProcessor;
            if (processor == null)
                return;

            if (request.Command == ActionCameraBobCommandApply)
                processor.RegisterActionBob(ResolveActionCameraBobPresentationIntensity(request.Intensity), request.Frequency);
            else if (request.Command == ActionCameraBobCommandClear)
                processor.ClearActionBob();
        }

        private static float ResolveActionCameraBobPresentationIntensity(float intensity)
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return intensity * math.lerp(0.65f, 1.15f, quality);
        }

        private void PlayCancelSound()
        {
            if (cancelSound == null) return;

            if (ResolveAudioService() != null && _cachedTransform != null)
                QueueActionAudio(ActionAudioClipCancel, 0u, 0u, _cachedTransform.position);
        }

        private void QueueActionAudio(byte clipKind, uint eventId, uint itemHash, Vector3 position)
        {
            _pendingActionAudio.Position = position;
            _pendingActionAudio.EventId = eventId;
            _pendingActionAudio.ItemHash = itemHash;
            _pendingActionAudio.ClipKind = clipKind;
            _pendingActionAudio.Dirty = 1;
            _pendingActionAudio.Reserved0 = 0;
            _pendingActionAudio.Reserved1 = 0u;
            _pendingActionAudio.Reserved2 = 0u;
        }

        private void FlushQueuedActionAudio()
        {
            if (_pendingActionAudio.Dirty == 0)
                return;

            ActionAudioRequest request = _pendingActionAudio;
            _pendingActionAudio = default;

            IAudioService audioService = ResolveAudioService();
            if (audioService == null)
                return;

            float volume = ResolveActionAudioPresentationVolume();
            if (request.EventId != 0u && audioService.IsAudioRuntimeReady)
            {
                CoreAudioEvent audioEvent = new CoreAudioEvent(request.EventId, request.Position, volume, 1f);
                if (audioService.QueueAudioEvent(in audioEvent))
                    return;
            }

            AudioClip clip = ResolveActionAudioClip(in request);
            if (clip != null)
                audioService.PlayAtPoint(clip, request.Position, volume, 1f);
        }

        private void ClearQueuedActionAudio()
        {
            _pendingActionAudio = default;
            _pendingActionCameraBob = default;
        }

        private AudioClip ResolveActionAudioClip(in ActionAudioRequest request)
        {
            switch (request.ClipKind)
            {
                case ActionAudioClipEating:
                    return eatingSound;
                case ActionAudioClipHealing:
                    return healingSound;
                case ActionAudioClipCancel:
                    return cancelSound;
                case ActionAudioClipItemUseSound:
                    return ResolveItemUseSound(request.ItemHash);
                default:
                    return null;
            }
        }

        private AudioClip ResolveItemUseSound(uint itemHash)
        {
            if (itemHash == 0u)
                return null;

            IPlayerInventoryService inventoryService = _playerInventoryService;
            PlayerInventory inventory = inventoryService != null ? inventoryService.Inventory : null;
            if (inventory == null || inventory.ItemCatalog == null || inventory.ItemCatalog.HasLookupAmbiguity)
                return null;

            ItemData item = inventory.ItemCatalog.FindByHash(unchecked((int)itemHash));
            return item != null ? item.useSound : null;
        }

        private static float ResolveActionAudioPresentationVolume()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.lerp(0.75f, 1f, quality);
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PRIVATE Гўв‚¬вЂќ REGISTRATION
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Player);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregister(bool clearQueuedPresentation = true)
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

            if (clearQueuedPresentation)
                ClearQueuedActionAudio();
        }

        private bool EnsureSingletonOwnership()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            PlayerActionController activeRuntime = ActiveRuntimeInstance;
            if (!ReferenceEquals(activeRuntime, null) && !ReferenceEquals(activeRuntime, this))
            {
                if (IsPlayerActionRuntimeUsable(activeRuntime))
                {
                    AbortDuplicateRuntimeOwner();
                    return false;
                }

                ActiveRuntimeInstance = null;
            }

            if (TryAbortForUsableExistingRuntime())
                return false;

            ActiveRuntimeInstance = this;
            return true;
        }

        private void ShutdownServiceState()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_state == ActionState.InProgress)
                CancelAction();

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
            _isInitialized = false;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _audioService = null;
            _playerMovement = null;
            _toolManager = null;
            _survivalSystem = null;
            _cachedTransform = null;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private bool TryRegisterService()
        {
            if (_serviceRegistered)
            {
                if (ReferenceEquals(GlobalRegistry.PlayerActions, this))
                    return true;

                _serviceRegistered = false;
            }

            if (!Application.isPlaying)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterPlayerActionRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.PlayerActions, this);
            return _serviceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsPlayerActionRuntimeUsable(registered))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            GlobalRegistry.UnregisterPlayerActionRuntime(registered);
            return false;
        }

        private static bool IsPlayerActionRuntimeUsable(PlayerActionController controller)
        {
            return controller != null &&
                   !controller._runtimeOwnerAborted &&
                   controller._serviceRegistered &&
                   controller.isActiveAndEnabled &&
                   ReferenceEquals(GlobalRegistry.PlayerActions, controller);
        }

        private static PlayerActionController ResolveUsableRuntime()
        {
            PlayerActionController runtime = ActiveRuntimeInstance;
            if (IsPlayerActionRuntimeUsable(runtime))
                return runtime;

            PlayerActionController registered = GlobalRegistry.PlayerActions;
            if (IsPlayerActionRuntimeUsable(registered))
            {
                ActiveRuntimeInstance = registered;
                return registered;
            }

            ActiveRuntimeInstance = null;
            return null;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwap();
            TryUnregister();
            TryUnregisterService();
            _isInitialized = false;
            _runtimeOwnerAborted = true;
            _playerRuntimeContext = null;
            _playerInventoryService = null;
            _audioService = null;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (Application.isPlaying)
                Destroy(this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterPlayerActionRuntime(this);
            _serviceRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            CachePlayerRuntimeContext(GlobalRegistry.Player ?? PlayerRuntimeContextService.ActiveRuntimeContext);
            CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
            CacheAudioService(GlobalRegistry.Audio);
            RefreshPlayerOwnedReferencesCold();
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = IsPlayerRuntimeContextUsable(playerRuntimeContext) ? playerRuntimeContext : null;
        }

        private void CachePlayerInventoryService(IPlayerInventoryService playerInventoryService)
        {
            _playerInventoryService = playerInventoryService != null && playerInventoryService.IsInitialized
                ? playerInventoryService
                : null;
        }

        private void RefreshPlayerOwnedReferencesCold()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (!IsPlayerRuntimeContextUsable(playerRuntimeContext))
            {
                CachePlayerRuntimeContext(GlobalRegistry.Player ?? PlayerRuntimeContextService.ActiveRuntimeContext);
                playerRuntimeContext = _playerRuntimeContext;
            }

            if (IsPlayerRuntimeContextUsable(playerRuntimeContext))
            {
                _cachedTransform = playerRuntimeContext.PlayerTransform != null ? playerRuntimeContext.PlayerTransform : _cachedTransform;
                _playerMovement = playerRuntimeContext.PlayerMovement;
                _toolManager = playerRuntimeContext.ToolManager;
                _survivalSystem = playerRuntimeContext.SurvivalSystem;
            }
            else
            {
                ClearPlayerOwnedReferences();
            }

            IPlayerInventoryService inventoryService = _playerInventoryService;
            if (inventoryService == null || !inventoryService.IsInitialized)
            {
                CachePlayerInventoryService(GlobalRegistry.PlayerInventory);
                inventoryService = _playerInventoryService;
            }

            if (inventoryService != null)
            {
                if (_toolManager == null)
                    _toolManager = inventoryService.ToolManager;
            }

            if (_cachedTransform == null)
                _cachedTransform = transform;

            ConsumableItem.BindSurvivalSystemCold(_survivalSystem);
        }

        private static bool IsPlayerRuntimeContextUsable(IPlayerRuntimeContext playerRuntimeContext)
        {
            return playerRuntimeContext != null &&
                   playerRuntimeContext.IsInitialized &&
                   playerRuntimeContext.PlayerObject != null &&
                   playerRuntimeContext.PlayerTransform != null;
        }

        private void ClearPlayerOwnedReferences()
        {
            _playerMovement = null;
            _toolManager = null;
            _survivalSystem = null;
            _cachedTransform = transform;
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = IsAudioServiceUsable(audioService) ? audioService : null;
        }

        private IAudioService ResolveAudioService()
        {
            IAudioService audioService = _audioService;
            if (IsAudioServiceUsable(audioService))
                return audioService;

            _audioService = null;
            return null;
        }

        private static bool IsAudioServiceUsable(IAudioService audioService)
        {
            if (audioService == null || !audioService.IsAudioRuntimeReady)
                return false;

            if (audioService is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterHotSwap()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }
    }
}
