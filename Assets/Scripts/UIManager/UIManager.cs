using UnityEngine;
using Unity.Netcode;
using System.Reflection;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject netUI;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private CanvasGroup gameOverCanvasGroup; // Add a Canvas Group to your panel for fading

    [Header("Audio")]
    [SerializeField] private AudioSource gameOverAudio;
    [SerializeField] private AudioSource GameStartAudio;

    [Header("Settings")]
    [SerializeField] private float fadeDuration = 1.5f;

    private PlayerController localPlayer;
    private FieldInfo deathField;
    private bool screenShown = false;

    void Start()
    {
        GameStartAudio.Play();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameOverCanvasGroup != null) gameOverCanvasGroup.alpha = 0;

        deathField = typeof(PlayerController).GetField("isDead", 
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    void Update()
    {
        HandleConnectionUI();
        HandleGameOverUI();

        
       
    }

    private void HandleConnectionUI()
    {
        if (NetworkManager.Singleton == null || netUI == null) return;

        // Automatically hide the NetUI (Server/Host/Client buttons) when game starts
        if (NetworkManager.Singleton.IsListening && netUI.activeSelf)
        {
            netUI.SetActive(false);
        }
    }

    private void HandleGameOverUI()
    {
        if (screenShown) {
            if(localPlayer != null && localPlayer.IsDead().Equals(false)) {
                GameStartAudio.Play();
                screenShown = false;
                gameOverAudio.Stop();
                gameOverPanel.SetActive(false);
            }
            return;
        }

        if (localPlayer == null)
        {
            FindLocalPlayer();
            return;
        }

        bool isDead = (bool)deathField.GetValue(localPlayer);

        if (isDead)
        {
            StartCoroutine(TriggerGameOverSequence());
            screenShown = true; 
        }
    }

    private IEnumerator TriggerGameOverSequence()
    {
        // 1. Play Sound
        if (gameOverAudio != null) {
            gameOverAudio.Play();
            GameStartAudio.Stop();
        
        }

        // 2. Show Panel
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            
            // 3. Smooth Fade-In
            if (gameOverCanvasGroup != null)
            {
                float counter = 0;
                while (counter < fadeDuration)
                {
                    counter += Time.deltaTime;
                    gameOverCanvasGroup.alpha = Mathf.Lerp(0, 1, counter / fadeDuration);
                    yield return null;
                }
            }
        }
        
        Debug.Log("Game Over Sequence Complete!");
    }

    private void FindLocalPlayer()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p.IsOwner)
            {
                localPlayer = p;
                break;
            }
        }
    }
}
