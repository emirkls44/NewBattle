using TMPro;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuFlow : MonoBehaviour
{
    private GameObject _mainMenu;
    private GameObject _modePanel;
    private Button _playButton;
    private Button _botsButton;
    private Button _everyoneSoloButton;
    private Button _squadButton;
    private Button _backButton;
    private TextMeshProUGUI _statusText;
    private NetworkBootstrap _bootstrap;

    private void Start()
    {
        _mainMenu = FindNamedObject("MainMenu");
        _modePanel = FindNamedObject("ModePanel");
        _playButton = FindNamedComponent<Button>("PlayButton");
        _botsButton = FindNamedComponent<Button>("BotsButton");
        _everyoneSoloButton = FindNamedComponent<Button>("EveryoneSoloButton");
        _squadButton = FindNamedComponent<Button>("SquadButton");
        _backButton = FindNamedComponent<Button>("BackButton");
        _statusText = FindNamedComponent<TextMeshProUGUI>("Status");
        _bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>(FindObjectsInactive.Include);

        if (_mainMenu == null || _modePanel == null || _playButton == null ||
            _botsButton == null || _everyoneSoloButton == null ||
            _squadButton == null || _backButton == null || _bootstrap == null)
        {
            Debug.LogError(
                "MainMenuFlow eksik nesne buldu. Gerekli isimler: MainMenu, PlayButton, ModePanel, " +
                "BotsButton, EveryoneSoloButton, SquadButton, BackButton ve NetworkBootstrap."
            );
            return;
        }

        // Inspector'da kalmis eski tiklama olaylari bu akisi bozamaz.
        _playButton.onClick = new Button.ButtonClickedEvent();
        _botsButton.onClick = new Button.ButtonClickedEvent();
        _everyoneSoloButton.onClick = new Button.ButtonClickedEvent();
        _squadButton.onClick = new Button.ButtonClickedEvent();
        _backButton.onClick = new Button.ButtonClickedEvent();

        _playButton.onClick.AddListener(OpenModePage);
        _botsButton.onClick.AddListener(() => StartMode("Bots"));
        _everyoneSoloButton.onClick.AddListener(() => StartMode("EveryoneSolo"));
        _squadButton.onClick.AddListener(() => StartMode("Squad"));
        _backButton.onClick.AddListener(OpenHomePage);

        _bootstrap.StatusChanged += SetStatus;
        _bootstrap.ConnectionCompleted += HandleConnectionCompleted;

        _mainMenu.SetActive(true);
        OpenHomePage();
    }

    private void OnDestroy()
    {
        if (_bootstrap == null)
            return;

        _bootstrap.StatusChanged -= SetStatus;
        _bootstrap.ConnectionCompleted -= HandleConnectionCompleted;
    }

    private void OpenHomePage()
    {
        _modePanel.SetActive(false);
        _playButton.gameObject.SetActive(true);
        SetButtonsInteractable(true);
        SetStatus("HAZIR");
    }

    private void OpenModePage()
    {
        _playButton.gameObject.SetActive(false);
        _modePanel.SetActive(true);
        _mainMenu.transform.SetAsLastSibling();
        _modePanel.transform.SetAsLastSibling();
        PrepareModePanelForInput();
        SetButtonsInteractable(true);
        SetStatus("MODUNU SEC");
        Debug.Log("MainMenuFlow: mode selection page opened.");
    }

    private void StartMode(string modeName)
    {
        Debug.Log($"MainMenuFlow: {modeName} button clicked.");
        SetStatus(modeName == "Bots"
            ? "BOT MACI HAZIRLANIYOR..."
            : "ESLESME ARANIYOR...");

        MethodInfo modeMethod = null;
        MethodInfo[] methods = typeof(NetworkBootstrap).GetMethods(BindingFlags.Instance | BindingFlags.Public);

        foreach (MethodInfo candidate in methods)
        {
            if (candidate.Name != "StartMatchmaking")
                continue;

            ParameterInfo[] parameters = candidate.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum)
            {
                modeMethod = candidate;
                break;
            }
        }

        if (modeMethod == null)
        {
            Debug.LogError(
                "NetworkBootstrap icinde tek enum parametreli StartMatchmaking metodu bulunamadi. " +
                "NetworkBootstrap surumu mod secimini desteklemiyor."
            );
            SetStatus("NETWORKBOOTSTRAP MOD DESTEGI EKSIK");
            return;
        }

        Type modeType = modeMethod.GetParameters()[0].ParameterType;

        try
        {
            object modeValue = Enum.Parse(modeType, modeName, true);
            modeMethod.Invoke(_bootstrap, new[] { modeValue });
        }
        catch (Exception exception)
        {
            Debug.LogError($"{modeName} modu baslatilamadi: {exception}");
            SetStatus("MOD BASLATILAMADI");
        }
    }

    private void PrepareModePanelForInput()
    {
        Canvas panelCanvas = _modePanel.GetComponent<Canvas>();
        if (panelCanvas == null)
            panelCanvas = _modePanel.AddComponent<Canvas>();

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 1000;

        if (_modePanel.GetComponent<GraphicRaycaster>() == null)
            _modePanel.AddComponent<GraphicRaycaster>();

        CanvasGroup canvasGroup = _modePanel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = _modePanel.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;

        Button[] buttons = _modePanel.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            button.gameObject.SetActive(true);
            button.interactable = true;

            if (button.targetGraphic != null)
                button.targetGraphic.raycastTarget = true;
        }
    }

    private void HandleConnectionCompleted(bool success)
    {
        if (success)
        {
            _mainMenu.SetActive(false);
            return;
        }

        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool value)
    {
        _botsButton.interactable = value;
        _everyoneSoloButton.interactable = value;
        _squadButton.interactable = value;
        _backButton.interactable = value;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }

    private GameObject FindNamedObject(string objectName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in transforms)
        {
            if (candidate.name == objectName)
                return candidate.gameObject;
        }

        return null;
    }

    private T FindNamedComponent<T>(string objectName) where T : Component
    {
        GameObject found = FindNamedObject(objectName);
        return found != null ? found.GetComponent<T>() : null;
    }
}
