using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    [SerializeField] private Button playButton;
    [SerializeField] private Button loginButton;
    [SerializeField] private Button updateAttributesButton;
    [SerializeField] private GameObject emailInputPanel;
    [SerializeField] private InputField emailInput;
    [SerializeField] private Button submitEmailButton;
    [SerializeField] private Button cancelEmailButton;
    [SerializeField] private Text errorText;

    private const string EMAIL_PATTERN = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

    private void Start()
    {
        Debug.Log("StartMenu: Start method called");

        // Add listeners to buttons
        if (playButton != null)
        {
            playButton.onClick.AddListener(OnPlayClicked);
            Debug.Log("StartMenu: Play button listener added");
        }
        else
        {
            Debug.LogError("StartMenu: Play button is null!");
        }

        if (loginButton != null)
        {
            loginButton.onClick.AddListener(OnLoginClicked);
            Debug.Log("StartMenu: Login button listener added");
        }
        else
        {
            Debug.LogError("StartMenu: Login button is null!");
        }

        if (updateAttributesButton != null)
        {
            updateAttributesButton.onClick.AddListener(OnUpdateAttributesClicked);
            Debug.Log("StartMenu: Update attributes button listener added");
        }
        else
        {
            Debug.LogError("StartMenu: Update attributes button is null!");
        }

        if (submitEmailButton != null)
        {
            submitEmailButton.onClick.AddListener(OnSubmitEmail);
            Debug.Log("StartMenu: Submit button listener added");
        }
        else
        {
            Debug.LogError("StartMenu: Submit button is null!");
        }

        if (cancelEmailButton != null)
        {
            cancelEmailButton.onClick.AddListener(OnCancelEmail);
            Debug.Log("StartMenu: Cancel button listener added");
        }
        else
        {
            Debug.LogError("StartMenu: Cancel button is null!");
        }

        // Configure email input field
        if (emailInput != null)
        {
            // Basic configuration for email input
            emailInput.contentType = InputField.ContentType.EmailAddress;
            emailInput.keyboardType = TouchScreenKeyboardType.EmailAddress;
            emailInput.characterLimit = 100;

            // Add listeners
            emailInput.onValueChanged.AddListener(OnEmailInputChanged);
            emailInput.onEndEdit.AddListener(OnEmailSubmitted);

            Debug.Log("StartMenu: Email input field configured");
        }
        else
        {
            Debug.LogError("StartMenu: Email input field is null!");
        }

        // Hide email input panel initially
        if (emailInputPanel != null)
        {
            emailInputPanel.SetActive(false);
            Debug.Log("StartMenu: Email panel hidden initially");
        }
        else
        {
            Debug.LogError("StartMenu: Email panel is null!");
        }

        // Show main buttons initially
        ShowMainButtons();

        // Clear error text
        if (errorText != null)
        {
            errorText.text = "";
        }
    }

    private void ShowMainButtons()
    {
        if (playButton != null) playButton.gameObject.SetActive(true);
        if (loginButton != null) loginButton.gameObject.SetActive(true);
        if (updateAttributesButton != null) updateAttributesButton.gameObject.SetActive(true);
    }

    private void HideMainButtons()
    {
        if (playButton != null) playButton.gameObject.SetActive(false);
        if (loginButton != null) loginButton.gameObject.SetActive(false);
        if (updateAttributesButton != null) updateAttributesButton.gameObject.SetActive(false);
    }

    private void OnPlayClicked()
    {
        Debug.Log("StartMenu: Play button clicked");
        // Load the main game scene
        SceneManager.LoadScene("SampleScene");
    }

    private void OnLoginClicked()
    {
        Debug.Log("StartMenu: Login button clicked");
        if (emailInputPanel != null)
        {
            emailInputPanel.SetActive(true);
            HideMainButtons();
            emailInput.text = ""; // Clear any previous input
            emailInput.ActivateInputField();
            Debug.Log("StartMenu: Email panel shown and input field activated");
        }
        else
        {
            Debug.LogError("StartMenu: Email input panel not assigned!");
        }
    }

    private void OnUpdateAttributesClicked()
    {
        Debug.Log("StartMenu: Update attributes button clicked");
        if (IntercomManager.Instance != null)
        {
            IntercomManager.Instance.UpdateVerificationAttributes();
        }
        else
        {
            Debug.LogError("StartMenu: IntercomManager instance not found!");
        }
    }

    private void OnEmailInputChanged(string value)
    {
        // Clear error text when user types
        if (errorText != null)
        {
            errorText.text = "";
        }
    }

    private void OnEmailSubmitted(string value)
    {
        Debug.Log("StartMenu: Email submitted via keyboard");
        OnSubmitEmail();
    }

    private bool IsValidEmail(string email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        try
        {
            // Use System.Net.Mail.MailAddress for robust email validation
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void OnSubmitEmail()
    {
        Debug.Log("StartMenu: Submit email button clicked");
        string email = emailInput.text.Trim();

        if (!IsValidEmail(email))
        {
            if (errorText != null)
            {
                errorText.text = "Please enter a valid email address";
            }
            Debug.Log("StartMenu: Invalid email entered");
            return;
        }

        // Trigger Intercom login using IntercomManager with the provided email
        if (IntercomManager.Instance != null)
        {
            Debug.Log("StartMenu: Registering user with email: " + email);
            IntercomManager.Instance.RegisterUserWithEmail(email);
            emailInputPanel.SetActive(false);
            ShowMainButtons();
        }
        else
        {
            Debug.LogError("StartMenu: IntercomManager instance not found!");
        }
    }

    private void OnCancelEmail()
    {
        Debug.Log("StartMenu: Cancel button clicked");
        if (emailInputPanel != null)
        {
            emailInputPanel.SetActive(false);
            ShowMainButtons();
            emailInput.text = ""; // Clear input
        }
    }

    private void Update()
    {
        // Handle escape key to cancel email input
        if (emailInputPanel != null && emailInputPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape))
        {
            OnCancelEmail();
        }
    }

    private void OnDestroy()
    {
        Debug.Log("StartMenu: OnDestroy called");
        // Remove listeners to prevent memory leaks
        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayClicked);
        if (loginButton != null)
            loginButton.onClick.RemoveListener(OnLoginClicked);
        if (updateAttributesButton != null)
            updateAttributesButton.onClick.RemoveListener(OnUpdateAttributesClicked);
        if (submitEmailButton != null)
            submitEmailButton.onClick.RemoveListener(OnSubmitEmail);
        if (cancelEmailButton != null)
            cancelEmailButton.onClick.RemoveListener(OnCancelEmail);
        if (emailInput != null)
        {
            emailInput.onValueChanged.RemoveListener(OnEmailInputChanged);
            emailInput.onEndEdit.RemoveListener(OnEmailSubmitted);
        }
    }
}
