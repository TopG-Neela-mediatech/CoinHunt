using DG.Tweening;
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
        public void PlayIntro()
        {
            if (!playOnce)
            {
                playOnce = true;
                float delay = RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.Intro);
                DOVirtual.DelayedCall(delay, () => PlayCurrencyIntro(CoinType.Rupee));
                return;
            }
            PlayCurrencyIntro(CoinType.Rupee);
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
            RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.playerWin);
        }
        public void PlayJethaWin()
        {
            RuntimeAudioLoader.Instance.PlayRuntimeAudio(audioMapper.jethaWin);
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
        Incorrect
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
