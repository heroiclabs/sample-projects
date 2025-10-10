using System.Threading.Tasks;
using Facebook.Unity;
using Nakama;
using Nakama.TinyJson;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityNakamaCloudSave
{
    [RequireComponent(typeof(UIDocument))]
    public class NakamaCloudSaveController : MonoBehaviour
    {
        private class PointsData
        {
            public int points;
            public string timestamp;
        }

        [Header("Nakama Settings")] [SerializeField]
        private string scheme = "http";
        [SerializeField]
        private string host = "127.0.0.1";
        [SerializeField]
        private int port = 7350;
        [SerializeField]
        private string serverKey = "defaultkey";

        private VisualElement authenticatedContainer;
        private VisualElement unauthenticatedContainer;
        private Button linkFacebookButton;
        private Button loginFacebookButton;
        private Button loginGuestButton;
        private Button logoutButton;
        private IntegerField pointsField;
        private Button submitPointsButton;
        private Label usernameLabel;
        private Label pointsLabel;

        private VisualElement errorPopup;
        private Button errorCloseButton;
        private Label errorMessage;

        private Client client;
        private ISession session;

        #region Initialization
        private void Start()
        {
            InitializeUI();

            client = new Client(scheme, host, port, serverKey, UnityWebRequestAdapter.Instance);
        }

        private void InitializeUI()
        {
            var rootElement = GetComponent<UIDocument>().rootVisualElement;
            
            authenticatedContainer = rootElement.Q<VisualElement>("authenticated-container");    
            unauthenticatedContainer = rootElement.Q<VisualElement>("unauthenticated-container"); 
            
            usernameLabel = rootElement.Q<Label>("username");
            pointsLabel = rootElement.Q<Label>("points");
            
            linkFacebookButton = rootElement.Q<Button>("link-facebook");
            loginFacebookButton = rootElement.Q<Button>("login-facebook");
            loginGuestButton = rootElement.Q<Button>("login-guest");
            logoutButton = rootElement.Q<Button>("logout");
            
            pointsField = rootElement.Q<IntegerField>("points-input");
            submitPointsButton = rootElement.Q<Button>("submit-data");

            errorPopup = rootElement.Q<VisualElement>("error-popup");
            errorMessage = rootElement.Q<Label>("error-message");
            errorCloseButton = rootElement.Q<Button>("error-close");
            errorCloseButton.RegisterCallback<ClickEvent>(_ => errorPopup.style.display = DisplayStyle.None);

            linkFacebookButton.RegisterCallback<ClickEvent>(OnFacebookAuth);
            loginFacebookButton.RegisterCallback<ClickEvent>(OnFacebookAuth);
            loginGuestButton.RegisterCallback<ClickEvent>(evt => _ = OnGuestLogin());
            logoutButton.RegisterCallback<ClickEvent>(evt => _ = OnLogout());
            submitPointsButton.RegisterCallback<ClickEvent>(evt => _ = OnSubmitScore());

            // Set initial button visibility.
            _ = UpdateUIVisibility();
        }

        private async Task UpdateUIVisibility()
        {
            if (session != null)
            {
                // Swap to authenticated view.
                authenticatedContainer.style.display = DisplayStyle.Flex;
                unauthenticatedContainer.style.display = DisplayStyle.None;

                usernameLabel.text = $"Username: {session.Username}";
                var user = (await client.GetAccountAsync(session)).User;
                linkFacebookButton.text = string.IsNullOrEmpty(user.FacebookId) ? "Link Facebook" : "Unlink Facebook";
            }
            else
            {
                // Swap to unauthenticated view.
                authenticatedContainer.style.display = DisplayStyle.None;
                unauthenticatedContainer.style.display = DisplayStyle.Flex;

                usernameLabel.text = "Username:";
                pointsLabel.text = "Points:";
            }
        }

        // See: https://developers.facebook.com/docs/unity/examples
        private void OnFacebookAuth(ClickEvent _)
        {
            if (!FB.IsInitialized)
            {
                // After FB.Init has finished, AuthenticateWithFacebook will be executed.
                FB.Init(AuthenticateWithFacebook);
            }
            else
            {
                AuthenticateWithFacebook();
            }
        }

        private async Task OnGuestLogin()
        {
            await AuthenticateWithDevice();
            await LoadLatestData();
        }

        private async Task OnSubmitScore()
        {
            try
            {
                await HandleSubmitData(pointsField.value);
                pointsLabel.text = $"Points: {pointsField.value}";
                pointsField.value = 0;
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            }
        }
        #endregion

        #region Authentication
        private async Task AuthenticateWithDevice()
        {
            var deviceId = PlayerPrefs.GetString("deviceId", SystemInfo.deviceUniqueIdentifier);

            if (deviceId == SystemInfo.unsupportedIdentifier)
            {
                deviceId = System.Guid.NewGuid().ToString();
            }

            PlayerPrefs.SetString("deviceId", deviceId);

            try
            {
                session = await client.AuthenticateDeviceAsync(deviceId);
                await UpdateUIVisibility();
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            }
        }

        private async void AuthenticateWithFacebook()
        {
            if (session != null)
            {
                var user = (await client.GetAccountAsync(session)).User;
                if (!string.IsNullOrEmpty(user.FacebookId))
                {
                    // Unlink Facebook if the user already has a Facebook ID linked.
                    await client.UnlinkFacebookAsync(session, AccessToken.CurrentAccessToken.TokenString);
                    linkFacebookButton.text = "Link Facebook";
                    return;
                }
            }

            FB.ActivateApp();
            FB.LogInWithReadPermissions(new[] { "public_profile" }, LoginCallback);

            return;

            async void LoginCallback(ILoginResult _)
            {
                if (!FB.IsLoggedIn) return;

                try
                {
                    if (session != null)
                    {
                        await client.LinkFacebookAsync(session, AccessToken.CurrentAccessToken.TokenString, false);
                    }
                    else
                    {
                        session = await client.AuthenticateFacebookAsync(AccessToken.CurrentAccessToken.TokenString, null, true, false);
                    }

                    await UpdateUIVisibility();
                    await LoadLatestData();
                }
                catch (ApiResponseException e)
                {
                    errorPopup.style.display = DisplayStyle.Flex;
                    errorMessage.text = e.Message;
                }
            }
        }

        private async Task OnLogout()
        {
            await client.SessionLogoutAsync(session);
            session = null;
            await UpdateUIVisibility();
        }
        #endregion

        #region Storage Engine
        private async Task HandleSubmitData(int points)
        {
            var pointsData = new PointsData
            {
                points = points,
                timestamp = System.DateTime.UtcNow.ToString("o")
            };

            var pointsJson = pointsData.ToJson();

            var writeObject = new WriteStorageObject
            {
                Collection = "points",
                Key = "latest_points",
                Value = pointsJson,
                PermissionRead = 1,  // Only the server and owner can read
                PermissionWrite = 1  // Only the server and owner can write
            };

            await client.WriteStorageObjectsAsync(session, new IApiWriteStorageObject[] { writeObject });
        }

        private async Task LoadLatestData()
        {
            if (session == null) return;

            try
            {
                var readObjectId = new StorageObjectId
                {
                    Collection = "points",
                    Key = "latest_points",
                    UserId = session.UserId
                };

                // Attempt to read the latest_points object from storage.
                var result = await client.ReadStorageObjectsAsync(session, new IApiReadStorageObjectId[] { readObjectId });

                using var enumerator = result.Objects.GetEnumerator();
                if (enumerator.MoveNext() && enumerator.Current != null)
                {
                    // If successful, update the points label.
                    var pointsData = enumerator.Current.Value.FromJson<PointsData>();
                    pointsLabel.text = $"Points: {pointsData.points}";
                }
                else
                {
                    pointsLabel.text = "Points: 0";
                }
            }
            catch (ApiResponseException e)
            {
                errorPopup.style.display = DisplayStyle.Flex;
                errorMessage.text = e.Message;
            }
        }
        #endregion
    }
}
