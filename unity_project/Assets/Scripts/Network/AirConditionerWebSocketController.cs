using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic;

public class AirConditionerWebSocketController : MonoBehaviour
{
    [Header("AWS Connection Settings")]
    public string uniqueClientID = "air_conditioner_client";
    public string authToken = "{your_auth_key}";

    [Header("Device Specific Control")]
    public AudioSource acSound;
    private ClientWebSocket clientWebSocket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private string wsUrl;
    private bool isACOn = false;

    // --- UNITY DONGUSU---

    void Start()
    {
        string cleanedBaseUrl = Constants.url.Trim();
        string cleanedClientID = uniqueClientID.Trim();

        wsUrl = $"wss://{cleanedBaseUrl}/ws/{cleanedClientID}";
        Debug.Log($"Klima URL: {wsUrl}");

        if (acSound != null)
        {
            acSound.loop = true;
            acSound.Play();
            acSound.mute = true;
        }

        ConnectToAWS();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // --- CIHAZA OZGU MESAJ ISLEYICI ---

    private void HandleServerMessage(string stateValue)
    {
        if (stateValue == "1" || stateValue == "0")
        {
            bool newState = stateValue == "1";
            isACOn = newState;

            if (acSound != null)
            {
                acSound.mute = !newState;
            }

            Debug.Log($"[Klima] Server komutu alýndý: {(isACOn ? "AÇIK" : "KAPALI")}");
        }
    }

    // --- WEBSOCKET BAGLANTI FONKSIYONLARI ---

    // 1. BAGLANTI KUR (ConnectToAWS)
    public async void ConnectToAWS()
    {
        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning($"[Klima] Zaten baðlýsýnýz ({uniqueClientID}).");
            return;
        }

        clientWebSocket = new ClientWebSocket();
        try
        {
            string cleanedAuthToken = authToken.Trim();
            if (!string.IsNullOrEmpty(cleanedAuthToken))
            {
                clientWebSocket.Options.SetRequestHeader("X-Auth-Token", cleanedAuthToken);
            }

            Uri serverUri = new Uri(wsUrl);
            await clientWebSocket.ConnectAsync(serverUri, cts.Token);

            if (clientWebSocket.State == WebSocketState.Open)
            {
                Debug.Log($"[Klima] Baðlantý Baþarýlý: {uniqueClientID}");
                _ = ReceiveMessages();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Klima] Baðlantý Hatasý ({uniqueClientID}): " + e.Message);
        }
    }

    // 2. MESAJ GONDER (SendMessage)
    public async void SendMessage(string message)
    {
        if (clientWebSocket == null || clientWebSocket.State != WebSocketState.Open)
        {
            Debug.LogError($"[Klima] Baðlantý açýk deðil. Mesaj gönderilemedi ({uniqueClientID}).");
            return;
        }

        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await clientWebSocket.SendAsync(
                new ArraySegment<byte>(buffer),
                WebSocketMessageType.Text,
                true,
                cts.Token
            );
            Debug.Log($"[Klima] Mesaj Gönderildi: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Klima] Mesaj gönderme hatasý ({uniqueClientID}): " + e.Message);
        }
    }

    // 3. MESAJ AL (ReceiveMessages)
    private async Task ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        while (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await clientWebSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, result.Count).Trim();
                    HandleServerMessage(receivedMessage);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Kapatma isteði alýndý.", CancellationToken.None);
                    Debug.Log($"[Klima] Sunucu baðlantýyý kapattý ({uniqueClientID}).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Klima] Mesaj alma hatasý veya baðlantý kesildi ({uniqueClientID}): " + e.Message);
                break;
            }
        }
    }

    // 4. BAGLANTIYI KES (Disconnect)
    public async void Disconnect()
    {
        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            cts.Cancel();
            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unity istemcisi kapandý.", CancellationToken.None);
            Debug.Log($"[Klima] WebSocket baðlantýsý kapatýldý ({uniqueClientID}).");
        }
        cts = new CancellationTokenSource();
    }
}