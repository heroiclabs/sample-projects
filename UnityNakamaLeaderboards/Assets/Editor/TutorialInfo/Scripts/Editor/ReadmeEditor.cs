using UnityEngine;
using UnityEditor;
using System.IO;
using System.Reflection;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
public class ReadmeEditor : Editor
{
    static readonly string s_ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";

    private const float k_Space = 16f;

    static ReadmeEditor()
    {
        EditorApplication.delayCall += SelectReadmeAutomatically;
    }

    static void SelectReadmeAutomatically()
    {
        if (!SessionState.GetBool(s_ShowedReadmeSessionStateName, false))
        {
            var readme = SelectReadme();
            SessionState.SetBool(s_ShowedReadmeSessionStateName, true);

            if (readme && !readme.loadedLayout)
            {
                LoadLayout();
                readme.loadedLayout = true;
            }
        }
    }

    static void LoadLayout()
    {
        var assembly = typeof(EditorApplication).Assembly;
        var windowLayoutType = assembly.GetType("UnityEditor.WindowLayout", true);
        var method = windowLayoutType.GetMethod("LoadWindowLayout", BindingFlags.Public | BindingFlags.Static);
        if (method != null)
            method.Invoke(null, new object[] { Path.Combine(Application.dataPath, "TutorialInfo/Layout.wlt"), false });
    }

    static Readme SelectReadme()
    {
        var ids = AssetDatabase.FindAssets("Readme t:Readme");
        if (ids.Length == 1)
        {
            var readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));

            Selection.objects = new[] { readmeObject };

            return (Readme)readmeObject;
        }
        else
        {
            Debug.Log("Couldn't find a readme");
            return null;
        }
    }

    protected override void OnHeaderGUI()
    {
        var readme = (Readme)target;
        Init();

        var iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / 3f - 20f, 128f);

        GUILayout.BeginHorizontal("In BigTitle");
        {
            if (readme.icon != null)
            {
                GUILayout.Space(k_Space);
                GUILayout.Label(readme.icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
            }
            GUILayout.Space(k_Space);
            GUILayout.BeginVertical();
            {

                GUILayout.FlexibleSpace();
                GUILayout.Label(readme.title, TitleStyle);
                GUILayout.FlexibleSpace();
            }
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }
        GUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI()
    {
        var readme = (Readme)target;
        Init();

        foreach (var section in readme.sections)
        {
            if (!string.IsNullOrEmpty(section.heading))
            {
                GUILayout.Label(section.heading, HeadingStyle);
            }

            if (!string.IsNullOrEmpty(section.text))
            {
                GUILayout.Label(section.text, BodyStyle);
            }

            if (!string.IsNullOrEmpty(section.linkText))
            {
                if (LinkLabel(new GUIContent(section.linkText)))
                {
                    Application.OpenURL(section.url);
                }
            }

            GUILayout.Space(k_Space);
        }
    }

    private bool m_Initialized;

    private GUIStyle LinkStyle => m_LinkStyle;

    [SerializeField] private GUIStyle m_LinkStyle;

    private GUIStyle TitleStyle => m_TitleStyle;

    [SerializeField] private GUIStyle m_TitleStyle;

    private GUIStyle HeadingStyle => m_HeadingStyle;

    [SerializeField] private GUIStyle m_HeadingStyle;

    private GUIStyle BodyStyle => m_BodyStyle;

    [SerializeField]
    GUIStyle m_BodyStyle;

    private GUIStyle ButtonStyle => m_ButtonStyle;

    [SerializeField]
    GUIStyle m_ButtonStyle;

    void Init()
    {
        if (m_Initialized)
            return;
        m_BodyStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            fontSize = 14,
            richText = true
        };

        m_TitleStyle = new GUIStyle(m_BodyStyle)
        {
            fontSize = 26
        };

        m_HeadingStyle = new GUIStyle(m_BodyStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = 18
        };

        m_LinkStyle = new GUIStyle(m_BodyStyle)
        {
            wordWrap = false,
            normal =
            {
                // Match selection color which works nicely for both light and dark skins
                textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f)
            },
            stretchWidth = false
        };

        m_ButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold
        };

        m_Initialized = true;
    }

    bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
    {
        var position = GUILayoutUtility.GetRect(label, LinkStyle, options);

        Handles.BeginGUI();
        Handles.color = LinkStyle.normal.textColor;
        Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
        Handles.color = Color.white;
        Handles.EndGUI();

        EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

        return GUI.Button(position, label, LinkStyle);
    }
}
