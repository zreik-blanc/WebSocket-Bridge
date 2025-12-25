using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Collections.Generic; 

public class RoomLightWebSocketController : MonoBehaviour
{
    [Header("AWS Connection Settings")]
    
    public string uniqueClientID = "all_house_lights";
    public string authToken = "{your_auth_key}";

    [Header("Device Specific Control")]
 
    public List<GameObject> allLights; 
    private ClientWebSocket clientWebSocket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private string wsUrl;
    private bool areLightsOn = false;

    // --- UNITY DONGUSU ---

    void Start()
    {
        string cleanedBaseUrl = Constants.url.Trim();
        string cleanedClientID = uniqueClientID.Trim();

        wsUrl = $"wss://{cleanedBaseUrl}/ws/{cleanedClientID}";
        Debug.Log($"Tüm Iþýklar URL: {wsUrl}");

        if (allLights != null)
        {
            foreach (GameObject lightObject in allLights)
            {
                if (lightObject != null)
                {
                    lightObject.SetActive(false);
                }
            }
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
            areLightsOn = newState;

            if (allLights != null)
            {
                foreach (GameObject lightObject in allLights)
                {
                    if (lightObject != null)
                    {
                        lightObject.SetActive(newState);
                    }
                }
            }

            Debug.Log($"[Tüm Iþýklar] Server komutu alýndý: {(areLightsOn ? "AÇIK" : "KAPALI")}");
        }
    }

    // --- WEBSOCKET BAGLANTI FONKSIYONLARI ---

    // 1. BAGLANTI KUR (ConnectToAWS)
    public async void ConnectToAWS()
    {
        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning($"[Tüm Iþýklar] Zaten baðlýsýnýz ({uniqueClientID}).");
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
                Debug.Log($"[Tüm Iþýklar] Baðlantý Baþarýlý: {uniqueClientID}");
                _ = ReceiveMessages();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[Tüm Iþýklar] Baðlantý Hatasý ({uniqueClientID}): " + e.Message);
        }
    }

    // 2. MESAJ GONDER (SendMessage)
    public async void SendMessage(string message)
    {
        if (clientWebSocket == null || clientWebSocket.State != WebSocketState.Open)
        {
            Debug.LogError($"[Tüm Iþýklar] Baðlantý açýk deðil. Mesaj gönderilemedi ({uniqueClientID}).");
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
            Debug.Log($"[Tüm Iþýklar] Mesaj Gönderildi: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Tüm Iþýklar] Mesaj gönderme hatasý ({uniqueClientID}): " + e.Message);
        }
    }

    // 3. MESAJ AL (ReceiveMessages)
    private async Task ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        while (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            try
            // ... (Geri kalan ReceiveMessages kodu aynýdýr)
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
                    Debug.Log($"[Tüm Iþýklar] Sunucu baðlantýyý kapattý ({uniqueClientID}).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[Tüm Iþýklar] Mesaj alma hatasý veya baðlantý kesildi ({uniqueClientID}): " + e.Message);
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
            Debug.Log($"[Tüm Iþýklar] WebSocket baðlantýsý kapatýldý ({uniqueClientID}).");
        }
        cts = new CancellationTokenSource();
    }
}