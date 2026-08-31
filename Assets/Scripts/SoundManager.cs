using System;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private AudioMapper audioMapper;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private SFXData[] sfxData;
        private bool playOnce;


        private void Start()
        {
            playOnce = false;
        }
        // Plays the one-time intro voice line and returns how long to wait before it's safe to start
        // the currency cue / show the game (0 on every call after the first, since there's no intro
        // clip to wait out then). The caller (LevelManager) is responsible for actually waiting and
        // for playing the currency intro afterward — this used to schedule that itself via a fixed
        // delay running in parallel with the visual entrance, which let coins spawn in and become
        // tappable while the intro clip was still playing.
        public float PlayIntro()
        {
            if (playOnce) return 0f;

            playOnce = true;
            return RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.Intro);
        }
        public float PlayCurrencyIntro(CoinType c)
        {
            return RuntimeAudioLoader.Instance.PlayRuntimeAudio(Array.Find(audioMapper.coinAudios, x => x.coinType == c).coinAudio);
        }
        public void PlayCorrect()
        {
            if (RuntimeAudioLoader.Instance._commonAudioSource.isPlaying)
            {
                return;
            }
            else
            {
                int randomIndex = UnityEngine.Random.Range(0, audioMapper.correctAudios.Length);
                RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.correctAudios[randomIndex]);
            }
        }
        public void PlayInCorrect()
        {
            if (RuntimeAudioLoader.Instance._commonAudioSource.isPlaying)
            {
                return;
            }
            else
            {
                int randomIndex = UnityEngine.Random.Range(0, audioMapper.incorrectAudios.Length);
                RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.incorrectAudios[randomIndex]);
            }
        }
        public void PlayPlayerWin()
        {
            StopAllAudio();
            RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.playerWin);
        }
        public void PlayJethaWin()
        {
            StopAllAudio();
            RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.jethaWin);
        }
        // Called right before win/lose audio plays so nothing still-playing (e.g. a currency-change
        // cue whose coroutine hadn't been cancelled yet, or a correct/incorrect SFX) bleeds through
        // over it. PlayRuntimeAudio already stops _commonAudioSource on its own, but sfxSource is a
        // separate source it never touches, so that needs stopping here explicitly too.
        private void StopAllAudio()
        {
            if (RuntimeAudioLoader.Instance != null) RuntimeAudioLoader.Instance.StopCommonAudioSource();
            if (sfxSource != null) sfxSource.Stop();
        }
        public void PlaySFX(sfxEnum e)
        {
            AudioClip clip = Array.Find(sfxData, x => x.sfxEnum == e)?.audioClip;
            if (clip != null)
            {
                if (sfxSource.isPlaying)
                {
                    sfxSource.Stop();
                }
                sfxSource.PlayOneShot(clip);
            }
            else
            {
                Debug.Log("Clip Not Assigned or Found");
            }
        }
    }
    [System.Serializable]
    public class SFXData
    {
        public AudioClip audioClip;
        public sfxEnum sfxEnum;
    }
    public enum sfxEnum
    {
        None,
        Correct,
        Incorrect,
        jethaCollect
    }
    [System.Serializable]
    public class AudioMapper
    {
        public string Intro;
        public string[] incorrectAudios;
        public string[] correctAudios;
        public CoinAudios[] coinAudios;
        public string playerWin;
        public string jethaWin;
    }
    [System.Serializable]
    public class CoinAudios
    {
        public CoinType coinType;
        public string coinAudio;
    }
}
