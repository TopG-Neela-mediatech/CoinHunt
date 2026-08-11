using System;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    public class SoundManager : MonoBehaviour
    {
        [SerializeField] private AudioMapper audioMapper;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private SFXData[] sfxData;


        private void PlaySFXSource(AudioClip c)
        {
            if (c != null)
            {
                sfxSource.PlayOneShot(c);
            }
        }
        public void PlaySFX(sfxEnum e)
        {
            AudioClip clip = Array.Find(sfxData, x => x.sfxEnum == e)?.audioClip;
            if (clip != null)
            {
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
