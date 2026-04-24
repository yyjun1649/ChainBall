using System.Collections.Generic;
using System.Linq;
using CS.AudioToolkit;
using UnityEngine;
using UnityEngine.Audio;

namespace Library
{
    public class SoundHandler : MonoBehaviour
    {
        #region field - normal

        public enum eBgmPlayType
        {
            NORMAL,
            OVERRIDE
        }

        public enum eSoundMode
        {
            NORMAL,
            UI_INTENSE
        }

        public AudioMixer audioMixer;
        public static List<AudioObject> listUIBgmStack = new();
        public static bool isLowPassFilterOn = false;
        public static bool IsSkipSound = false;

        private static string[] bgmGroup = { "BGM", "BGM_UI" };
        private static string[] sfxGroup = { "SFX", "AMB", "SFX_UI" };
        private static string[] voxGroup = { "VOX" };

        #endregion

        public void Initialize()
        {
            SetMasterVolume(GetMasterVolume(), false);
            SetBGMVolume(GetBGMVolume());
            SetSFXVolume(GetSFXVolume());
            SetVoxVolume(GetVoxVolume());
            SetSoundMode(eSoundMode.NORMAL);
        }

        #region BGM

        /// <summary>
        /// BGM 플레이
        /// </summary>
        internal static void PlayBGM(string bgm, eBgmPlayType type = eBgmPlayType.NORMAL, float startTime = 0)
        {
            if (AudioController.Instance == null)
            {
                return;
            }

            if (type != eBgmPlayType.OVERRIDE &&
                AudioController.GetAudioChannel(AudioChannelType.Music).currentlyPlaying != null &&
                AudioController.GetAudioChannel(AudioChannelType.Music).currentlyPlaying.audioID == bgm)
            {
                return; // 재생하려는 BGM이 재생 중인 BGM과 같을 경우 새로 재생하지 않고 재생을 유지한다.
            }

            StopBGM();
            AudioController.PlayMusic(bgm, AudioController.Instance.transform, startTime: startTime);
        }

        /// <summary>
        /// UI용 BGM 플레이
        /// 기존 일반 BGM은 잠시 중단
        /// </summary>
        internal static void PlayUIBGM(string bgm, eBgmPlayType type = eBgmPlayType.NORMAL)
        {
            if (AudioController.Instance == null)
            {
                return;
            }

            if (type != eBgmPlayType.OVERRIDE)
            {
                var playingObjects = AudioController.GetPlayingAudioObjects();
                for (int i = 0; i < playingObjects.Count; i++)
                {
                    if (playingObjects[i].audioID == bgm)
                    {
                        return; // 재생하려는 BGM이 재생 중인 BGM과 같을 경우 새로 재생하지 않고 재생을 유지한다.
                    }
                }
            }

            foreach (var uiBgm in listUIBgmStack)
            {
                if (uiBgm == null) continue;

                uiBgm.Pause();
            }

            Handlers.Sound.SetSoundMode(eSoundMode.UI_INTENSE);

            listUIBgmStack.Add(AudioController.Play(bgm, AudioController.Instance.transform));
        }

        /// <summary>
        /// 현재 UI BGM을 중단, 이전 스택의 UI BGM 출력
        /// </summary>
        internal static void StopAndPlayPrevUIBGM(string bgm)
        {
            if (AudioController.Instance == null)
            {
                return;
            }

            AudioObject audioObject = null;
            for (int i = 0; i < listUIBgmStack.Count; i++)
            {
                if (listUIBgmStack[i].audioID == bgm)
                {
                    audioObject = listUIBgmStack[i];
                    break;
                }
            }

            if (audioObject != null)
            {
                listUIBgmStack.Remove(audioObject);
            }

            AudioController.Stop(bgm);

            if (listUIBgmStack.Count == 0)
            {
                Handlers.Sound.SetSoundMode(eSoundMode.NORMAL);
            }
            else
            {
                var lastBgm = listUIBgmStack[listUIBgmStack.Count - 1];
                if (lastBgm != null)
                {
                    lastBgm.Unpause();
                }
            }
        }

        /// <summary>
        /// 해당 BGM이 속해 있는 카테고리 반환
        /// </summary>
        internal static AudioCategory GetBGMCategory(string bgm)
        {
            if (AudioController.GetAudioItem(bgm) == null)
            {
                return null;
            }

            return AudioController.GetAudioItem(bgm).category;
        }

        /// <summary>
        /// BGM 정지
        /// </summary>
        internal static void StopBGM()
        {
            AudioController.StopChannel(AudioChannelType.Music);
        }

        /// <summary>
        /// BGM 정지
        /// </summary>
        internal static void StopBGM(string bgm)
        {
            AudioController.Stop(bgm);
        }

        #endregion


        #region SFX

        /// <summary>
        /// SFX 플레이
        /// </summary>
        internal static void PlaySFX(string sfx, float fixedPitch = 0)
        {
            if (IsSkipSound)
            {
                return;
            }

            if (AudioController.Instance == null || string.IsNullOrEmpty(sfx))
            {
                return;
            }

            var audioObject = AudioController.Play(sfx, AudioController.Instance.transform);
            if (audioObject != null)
            {
                if (fixedPitch != 0)
                {
                    audioObject.pitch = fixedPitch;
                }
            }
        }

        /// <summary>
        /// SFX 중단
        /// </summary>
        internal static void StopSFX(string sfx) // SFX 정지
        {
            AudioController.Stop(sfx);
        }

        #endregion


        #region AMB

        /// <summary>
        /// 환경음 플레이
        /// </summary>
        internal static void PlayAMB(string sfx)
        {
            if (AudioController.Instance == null)
            {
                return;
            }

            StopAMB();
            AudioController.PlayAmbienceSound(sfx, AudioController.Instance.transform);
        }

        /// <summary>
        /// 환경음 중단
        /// </summary>
        internal static void StopAMB()
        {
            AudioController.StopChannel(AudioChannelType.Ambience);
        }

        #endregion


        #region Volume

        internal float GetBGMVolume()
        {
            return PlayerPrefs.GetFloat("OPTION_VOLUME_BGM", 1f);
        }

        internal void SetBGMVolume(float volume)
        {
            foreach (var str in bgmGroup)
            {
                AudioController.SetCategoryVolume(str, volume);
            }

            PlayerPrefs.SetFloat("OPTION_VOLUME_BGM", volume);
        }

        internal float GetMasterVolume()
        {
            return PlayerPrefs.GetFloat("OPTION_VOLUME_MASTER", 1f);
        }

        internal void SetMasterVolume(float volume, bool isChangePrefs)
        {
            AudioController.SetGlobalVolume(volume);

            if (isChangePrefs)
            {
                PlayerPrefs.SetFloat("OPTION_VOLUME_MASTER", volume);
            }
        }

        internal float GetSFXVolume()
        {
            return PlayerPrefs.GetFloat("OPTION_VOLUME_SFX", 1f);
        }

        internal void SetSFXVolume(float volume)
        {
            foreach (var str in sfxGroup)
            {
                AudioController.SetCategoryVolume(str, volume);
            }

            PlayerPrefs.SetFloat("OPTION_VOLUME_SFX", volume);
        }

        internal float GetVoxVolume()
        {
            return PlayerPrefs.GetFloat("OPTION_VOLUME_VOX", 1f);
        }

        internal void SetVoxVolume(float volume)
        {
            foreach (var str in voxGroup)
            {
                AudioController.SetCategoryVolume(str, volume);
            }

            PlayerPrefs.SetFloat("OPTION_VOLUME_VOX", volume);
        }

        #endregion


        #region public

        /// <summary>
        /// 클릭 사운드
        /// </summary>
        internal static void PlayClick()
        {
            if (AudioController.Instance == null)
            {
                return;
            }

            AudioController.Play("sfx_click", AudioController.Instance.transform);
        }

        /// <summary>
        /// 전체 사운드 정지
        /// </summary>
        internal static void StopAll()
        {
            // ToList() 제거 - 배열을 직접 순회
            foreach (var str in bgmGroup)
            {
                if (str != "BGM_UI")
                {
                    AudioController.StopCategory(str);
                }
            }

            foreach (var str in sfxGroup)
            {
                AudioController.StopCategory(str);
            }

            foreach (var str in voxGroup)
            {
                AudioController.StopCategory(str);
            }
        }

        /// <summary>
        /// 사운드 모드 변경
        /// </summary>
        /// <param name="mode"></param>
        internal void SetSoundMode(eSoundMode mode)
        {
            float bgmVolume = 0;
            float bgmUIVolume = 0;

            switch (mode)
            {
                case eSoundMode.NORMAL:
                {
                    bgmVolume = GetBGMVolume();
                    bgmUIVolume = 0f;
                    AudioController.StopCategory("BGM_UI");
                    AudioController.UnpauseCategory("BGM");

                    foreach (var str in sfxGroup)
                    {
                        AudioController.SetCategoryVolume(str, GetSFXVolume());
                    }

                    foreach (var str in voxGroup)
                    {
                        AudioController.SetCategoryVolume(str, GetVoxVolume());
                    }

                    break;
                }
                case eSoundMode.UI_INTENSE:
                {
                    bgmVolume = 0f;
                    bgmUIVolume = GetBGMVolume();
                    AudioController.PauseCategory("BGM");
                    foreach (var str in sfxGroup)
                    {
                        if (str.Equals("SFX_UI"))
                        {
                            AudioController.SetCategoryVolume(str, GetSFXVolume());
                        }
                        else
                        {
                            AudioController.SetCategoryVolume(str, 0f);
                        }
                    }

                    foreach (var str in voxGroup)
                    {
                        AudioController.SetCategoryVolume(str, 0f);
                    }

                    break;
                }
            }

            AudioController.SetCategoryVolume("BGM", bgmVolume);
            AudioController.SetCategoryVolume("BGM_UI", bgmUIVolume);
        }

        #endregion
    }
}
