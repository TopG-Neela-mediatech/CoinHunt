using StorySystem.Story;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TMKOC.CoinHunt
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private Button playSchoolBackButton;
        [SerializeField] private GameObject storyPrefab;
        [SerializeField] private StoryController sc;
        private void StartLevel() => GameManager.Instance.InvokeLevelStart();


        private void Awake()
        {                    
            playSchoolBackButton.onClick.AddListener(() => SceneManager.LoadScene(TMKOCPlaySchoolConstants.TMKOCPlayMainMenu));
        }
        private void Start()
        {
            GameManager.Instance.OnLevelStart += OnLevelStart;
            GameManager.Instance.OnLevelWin += OnLevelWin;
            if (sc != null)
            {
                sc.OnStoryFinished += OnStoryFinished;
            }
            StartLevel();         
        }
        private void OnStoryFinished()
        {
            storyPrefab.SetActive(false);         
        }
        private void OnLevelStart()
        {
         
        }
        private void OnLevelWin()
        {
      
        }
        private IEnumerator LoadWinPanelWithDelay(float delay)
        {
            yield return new WaitForSeconds(delay);                    
        }
     
        private void OnDestroy()
        {
            GameManager.Instance.OnLevelStart -= OnLevelStart;
            GameManager.Instance.OnLevelWin -= OnLevelWin;
        }
    }
}
