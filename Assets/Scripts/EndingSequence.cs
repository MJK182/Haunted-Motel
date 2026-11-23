using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndingSequence : MonoBehaviour
{
    [Header("Teksti")]
    public string title = "Pääsit pakoon!";
    [TextArea(2, 4)] public string[] lines;
    public float lineTime = 3.5f;
    public Color titleColor = Color.white;

    [Header("Kamera")]
    public Transform cameraTarget;          // tyhjä objekti: kamera liukuu sen paikkaan ja suuntaan
    public float cameraMoveTime = 3f;

    [Header("Häivytys")]
    public Color fadeColor = Color.black;   // pääovi: valkoinen, kellari: musta
    public float fadeDelay = 1.5f;
    public float fadeTime = 2.5f;

    [Header("Tehosteet (valinnaisia)")]
    public Light flashLight;                // valo, jonka kirkkaus nousee elokuvan aikana
    public float flashIntensity = 8f;
    public AudioSource sound;               // ääni, joka soi elokuvan alussa
    public GameObject[] enableOnStart;      // esim. aamun valo, ulkoalue
    public GameObject[] disableOnStart;     // esim. ovi

    [Header("Scenet")]
    public string gameScene = "Game";
    public string menuScene = "MainMenu";

    bool playing, showButtons;
    float fade, titleAlpha, lineAlpha;
    string currentLine = "";

    public void Play(Inventory inv)
    {
        if (playing) return;
        playing = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        Camera cam = Camera.main;

        // Lukitse pelaaja: sammuta kameran ja pelaajan skriptit (liike, katselu, vuorovaikutus)
        if (cam != null)
            foreach (var mb in cam.GetComponentsInParent<MonoBehaviour>())
                if (mb != this) mb.enabled = false;

        // Pysäytä zombit, ettei niiden kiinniotto lataa scenea uudelleen
        foreach (var z in FindObjectsOfType<ZombieAI>())
        {
            var agent = z.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
            z.enabled = false;
        }

        foreach (var g in enableOnStart) if (g) g.SetActive(true);
        foreach (var g in disableOnStart) if (g) g.SetActive(false);
        if (sound) sound.Play();

        StartCoroutine(FadeRoutine());
        if (flashLight) StartCoroutine(FlashRoutine());

        // Kamera liukuu kohteeseen
        if (cam != null && cameraTarget != null)
        {
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / Mathf.Max(0.1f, cameraMoveTime);
                float s = Mathf.SmoothStep(0f, 1f, t);
                cam.transform.position = Vector3.Lerp(p0, cameraTarget.position, s);
                cam.transform.rotation = Quaternion.Slerp(r0, cameraTarget.rotation, s);
                yield return null;
            }
        }

        // Odota häivytys valmiiksi
        yield return new WaitForSeconds(Mathf.Max(0f, fadeDelay + fadeTime - cameraMoveTime));

        // Otsikko
        yield return Fade(a => titleAlpha = a, 0f, 1f, 1.2f);
        yield return new WaitForSeconds(1.5f);

        // Tarinarivit yksi kerrallaan
        foreach (string line in lines)
        {
            currentLine = line;
            yield return Fade(a => lineAlpha = a, 0f, 1f, 0.8f);
            yield return new WaitForSeconds(lineTime);
            yield return Fade(a => lineAlpha = a, 1f, 0f, 0.8f);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        showButtons = true;
    }

    IEnumerator FadeRoutine()
    {
        yield return new WaitForSeconds(fadeDelay);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, fadeTime);
            fade = Mathf.Clamp01(t);
            yield return null;
        }
        fade = 1f;
    }

    IEnumerator FlashRoutine()
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.1f, cameraMoveTime + fadeDelay);
            flashLight.intensity = Mathf.Lerp(0f, flashIntensity, t);
            yield return null;
        }
    }

    IEnumerator Fade(System.Action<float> set, float from, float to, float time)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / time;
            set(Mathf.Lerp(from, to, t));
            yield return null;
        }
        set(to);
    }

    void OnGUI()
    {
        if (!playing) return;

        GUI.depth = -100;

        Color c = fadeColor; c.a = fade;
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        var big = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.08f) };
        var small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(Screen.height * 0.035f), wordWrap = true };

        Color tc = titleColor; tc.a = titleAlpha;
        GUI.color = tc;
        GUI.Label(new Rect(0, Screen.height * 0.18f, Screen.width, Screen.height * 0.15f), title, big);

        Color lc = titleColor; lc.a = lineAlpha;
        GUI.color = lc;
        GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.42f, Screen.width * 0.7f, Screen.height * 0.2f), currentLine, small);

        if (showButtons)
        {
            GUI.color = Color.white;
            float w = 260f, h = 46f, x = Screen.width / 2f - w / 2f, y = Screen.height * 0.72f;
            if (GUI.Button(new Rect(x, y, w, h), "Pelaa uudelleen")) SceneManager.LoadScene(gameScene);
            if (GUI.Button(new Rect(x, y + h + 12f, w, h), "Päävalikko")) SceneManager.LoadScene(menuScene);
        }
    }
}