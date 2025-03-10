using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

using Fusion;
using NSMB.Extensions;
using NSMB.Utils;

public class PlayerListEntry : MonoBehaviour {

    //---Public Variables
    public PlayerRef player;
    public float typingCounter;

    //---Serialized Variables
    [SerializeField] private TMP_Text nameText, pingText, winsText;
    [SerializeField] private Image colorStrip;
    [SerializeField] private RectTransform background, options;
    [SerializeField] private GameObject blockerTemplate, firstButton, chattingIcon;
    [SerializeField] private Canvas rootCanvas;
    [SerializeField] private LayoutElement layout;
    [SerializeField] private GameObject[] adminOnlyOptions;

    //---Private Variables
    private GameObject blockerInstance;
    private bool rainbow;

    private void OnDestroy() {
        if (blockerInstance)
            Destroy(blockerInstance);
    }

    public void Update() {
        if (rainbow) {
            nameText.color = Utils.GetRainbowColor(NetworkHandler.Instance.runner);
        }

        if (typingCounter > 0) {
            chattingIcon.SetActive(true);
            typingCounter -= Time.deltaTime;
        } else {
            chattingIcon.SetActive(false);
            typingCounter = 0;
        }
    }

    public void UpdateText() {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        PlayerData data = player.GetPlayerData(runner);

        rainbow = player.HasRainbowName();

        colorStrip.color = Utils.GetPlayerColor(runner, player);

        if (data.Wins == 0) {
            winsText.text = "";
        } else {
            winsText.text = "<sprite=55>" + data.Wins;
        }

        string permissionSymbol = "";
        if (data.IsRoomOwner) {
            permissionSymbol += "<sprite=5>";
            pingText.text = "";
        } else {
            int ping = data.Ping;
            pingText.text = ping + " " + Utils.GetPingSymbol(ping);
        }

        string characterSymbol = data.GetCharacterData().uistring;
        nameText.text = permissionSymbol + characterSymbol + data.GetNickname();

        Transform parent = transform.parent;
        int childIndex = 0;
        for (int i = 0; i < parent.childCount; i++) {
            if (parent.GetChild(i) != gameObject)
                continue;

            childIndex = i;
            break;
        }

        layout.layoutPriority = transform.parent.childCount - childIndex;
    }

    public void ShowDropdown() {
        NetworkRunner runner = NetworkHandler.Instance.runner;
        if (blockerInstance)
            Destroy(blockerInstance);

        bool admin = runner.IsServer && runner.LocalPlayer != player;
        foreach (GameObject option in adminOnlyOptions) {
            option.SetActive(admin);
        }

        Canvas.ForceUpdateCanvases();

        blockerInstance = Instantiate(blockerTemplate, rootCanvas.transform);
        RectTransform blockerTransform = blockerInstance.GetComponent<RectTransform>();
        blockerTransform.offsetMax = blockerTransform.offsetMin = Vector2.zero;
        blockerInstance.SetActive(true);

        background.offsetMin = new(background.offsetMin.x, -options.rect.height);
        options.anchoredPosition = new(options.anchoredPosition.x, -options.rect.height);

        EventSystem.current.SetSelectedGameObject(firstButton);
        MainMenuManager.Instance.sfx.PlayOneShot(Enums.Sounds.UI_Cursor);
    }

    public void HideDropdown(bool didAction) {
        Destroy(blockerInstance);

        background.offsetMin = new(background.offsetMin.x, 0);
        options.anchoredPosition = new(options.anchoredPosition.x, 0);

        MainMenuManager.Instance.sfx.PlayOneShot(didAction ? Enums.Sounds.UI_Decide : Enums.Sounds.UI_Back);
    }

    public void BanPlayer() {
        MainMenuManager.Instance.Ban(player);
        HideDropdown(true);
    }

    public void KickPlayer() {
        MainMenuManager.Instance.Kick(player);
        HideDropdown(true);
    }

    public void MutePlayer() {
        MainMenuManager.Instance.Mute(player);
        HideDropdown(true);
    }

    public void PromotePlayer() {
        MainMenuManager.Instance.Promote(player);
        HideDropdown(true);
    }

    public void CopyPlayerId() {
        TextEditor te = new();
        te.text = player.GetPlayerData(NetworkHandler.Instance.runner).GetUserIdString();
        te.SelectAll();
        te.Copy();
        HideDropdown(true);
    }
}