using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int keysRequired = 3;
    public TextMeshProUGUI keyText;
    public TextMeshProUGUI messageText;

    public int KeysCollected { get; private set; }
    public bool GameEnded { get; private set; }
    public bool HasAllKeys => KeysCollected >= keysRequired;

    float messageTimer;

    void Awake()
    {
        Instance = this;
        Time.timeScale = 1f;
    }

    void Start()
    {
        UpdateKeyUI();
        if (messageText) messageText.text = "";
    }

    void Update()
    {
        if (messageTimer > 0f)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0f && !GameEnded && messageText) messageText.text = "";
        }

        if (GameEnded && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void CollectKey()
    {
        KeysCollected++;
        UpdateKeyUI();
        ShowMessage(HasAllKeys ? "Kaikki avaimet! Juokse ulko-ovelle!" : "Löysit avaimen!");
    }

    public void ShowMessage(string msg, float duration = 3f)
    {
        if (!messageText) return;
        messageText.text = msg;
        messageTimer = duration;
    }

    public void GameOver()
    {
        if (GameEnded) return;
        GameEnded = true;
        messageText.text = "Zombi sai sinut!\nPaina R yrittääksesi uudelleen";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Win()
    {
        if (GameEnded) return;
        GameEnded = true;
        messageText.text = "Pääsit pakoon!\nPaina R pelataksesi uudelleen";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void UpdateKeyUI()
    {
        if (keyText) keyText.text = $"Avaimet: {KeysCollected} / {keysRequired}";
    }
}

