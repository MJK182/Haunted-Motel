using UnityEngine;

public class MessageUI : MonoBehaviour
{
    static MessageUI inst;
    string text;
    float until;

    public static void Show(string t, float time = 3f)
    {
        if (inst == null) inst = new GameObject("MessageUI").AddComponent<MessageUI>();
        inst.text = t;
        inst.until = Time.time + time;
    }

    void OnGUI()
    {
        if (Time.time > until) return;
        var s = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 24, wordWrap = true };
        GUI.Label(new Rect(Screen.width * 0.1f, Screen.height * 0.7f, Screen.width * 0.8f, 80), text, s);
    }
}