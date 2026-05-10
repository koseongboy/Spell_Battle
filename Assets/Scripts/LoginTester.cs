using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoginTester : MonoBehaviour
{
    public ServerConnector connector;
    public TMP_InputField idInput;
    public TMP_InputField pwInput;

    public void OnLoginClick()
    {
        connector.Login(idInput.text, pwInput.text);
    }
}