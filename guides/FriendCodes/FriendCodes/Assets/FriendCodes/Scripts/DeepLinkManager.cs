using System;
using UnityEngine;

public class DeepLinkManager : MonoBehaviour
{
    public static DeepLinkManager Instance { get; private set; }

    public string PendingInviteCode { get; private set; }

    public event Action<string> OnInviteCodeReceived;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Application.deepLinkActivated += OnDeepLinkActivated;

        if (!string.IsNullOrEmpty(Application.absoluteURL))
        {
            OnDeepLinkActivated(Application.absoluteURL);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Application.deepLinkActivated -= OnDeepLinkActivated;
        }
    }

    private void OnDeepLinkActivated(string url)
    {
        var code = ExtractInviteCode(url);
        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        PendingInviteCode = code;
        OnInviteCodeReceived?.Invoke(code);
    }

    private static string ExtractInviteCode(string url)
    {
        try
        {
            var uri = new Uri(url);

            var query = uri.Query.TrimStart('?');
            if (!string.IsNullOrEmpty(query))
            {
                foreach (var pair in query.Split('&'))
                {
                    var kv = pair.Split('=');
                    if (kv.Length == 2 && kv[0] == "code")
                    {
                        return Uri.UnescapeDataString(kv[1]);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to parse friend invite code '{url}': {e.Message}");
        }

        return null;
    }
}
