using System;
using UnityEngine;

namespace TMKOC.CoinHunt
{
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private UIManager uiManager;
        [SerializeField] private JethalalController jethalalController;
        private static GameManager instance;


        public static GameManager Instance { get { return instance; } }
        public LevelManager LevelManager { get { return levelManager; } }
        public UIManager UIManager { get { return uiManager; } }
        public JethalalController JethalalController { get { return jethalalController; } }
     
      
        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }


        #region Events
        public event Action OnLevelWin;      
        public event Action OnLevelStart;
        public void InvokeLevelStart() => OnLevelStart?.Invoke();
        public void InvokeLevelWin() => OnLevelWin?.Invoke();      
        #endregion
    }
}
