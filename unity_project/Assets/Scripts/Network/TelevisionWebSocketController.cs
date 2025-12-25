using UnityEngine;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class TelevisionWebSocketController : MonoBehaviour
{
    [Header("AWS Connection Settings")]
    public string uniqueClientID = "television_client";
    public string authToken = "{your_auth_key}";

    [Header("Device Specific Control")]
    public GameObject screenObject;
    public UnityEngine.Video.VideoPlayer videoPlayer; 

    private ClientWebSocket clientWebSocket;
    private CancellationTokenSource cts = new CancellationTokenSource();
    private string wssUrl;

    // --- UNITY DONGUSU ---

    void Start()
    {
        string cleanedBaseUrl = Constants.url.Trim();
        string cleanedClientID = uniqueClientID.Trim();

        wssUrl = $"wss://{cleanedBaseUrl}/ws/{cleanedClientID}";
        Debug.Log($"Televizyon URL: {wssUrl}");

        if (screenObject != null)
        {
            screenObject.SetActive(false);
        }

        // Uygulama basladýgýnda sunucumuza otomatik baglanýyoruz.
        ConnectToAWS();
    }

    void OnDestroy()
    {
        Disconnect();
    }

    // --- CIHAZA OZGU MESAJ ISLEYICI ---

    private void HandleServerMessage(string stateValue)
    {
        if (screenObject == null || videoPlayer == null) return;

        bool newState = stateValue == "1";

        if (newState)
        {
            screenObject.SetActive(true);
            videoPlayer.enabled = true;
            videoPlayer.Play();
            Debug.Log("[TV] Server komutu: AÇIK ve OYNATILIYOR");
        }
        else
        {
            videoPlayer.Stop();
            videoPlayer.enabled = false; 
            screenObject.SetActive(false);
            Debug.Log("[TV] Server komutu: KAPALI");
        }
    }

    // --- WEBSOCKET BAGLANTI FONKSIYONLARI ---
    public async void ConnectToAWS()
    {
        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            Debug.LogWarning($"[TV] Zaten baðlýsýnýz ({uniqueClientID}).");
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

            Uri serverUri = new Uri(wssUrl);
            await clientWebSocket.ConnectAsync(serverUri, cts.Token);

            if (clientWebSocket.State == WebSocketState.Open)
            {
                Debug.Log($"[TV] Baðlantý Baþarýlý: {uniqueClientID}");
                // Baglanti kurulduktan sonra gelen mesajlarý dinlemeye baslýyoruz.
                _ = ReceiveMessages();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[TV] Baðlantý Hatasý ({uniqueClientID}): " + e.Message);
        }
    }

    public async void SendMessage(string message)
    {
        if (clientWebSocket == null || clientWebSocket.State != WebSocketState.Open)
        {
            Debug.LogError($"[TV] Baðlantý açýk deðil. Mesaj gönderilemedi ({uniqueClientID}).");
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
            Debug.Log($"[TV] Mesaj Gönderildi: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TV] Mesaj gönderme hatasý ({uniqueClientID}): " + e.Message);
        }
    }

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
                    Debug.Log($"[TV] Sunucu baðlantýyý kapattý ({uniqueClientID}).");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[TV] Mesaj alma hatasý veya baðlantý kesildi ({uniqueClientID}): " + e.Message);
                break;
            }
        }
    }

    public async void Disconnect()
    {
        if (clientWebSocket != null && clientWebSocket.State == WebSocketState.Open)
        {
            cts.Cancel();
            await clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Unity istemcisi kapandý.", CancellationToken.None);
            Debug.Log($"[TV] WebSocket baðlantýsý kapatýldý ({uniqueClientID}).");
        }
      
        cts = new CancellationTokenSource();
    }
}