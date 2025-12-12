using System.Collections;
using System.Net;
using UnityEngine;
using UnityEngine.Networking;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public class VoiceManager : MonoBehaviour
{
    private AudioClip recording;
    private string microphoneDevice;
    private bool isRecording = false;

    //buraya backend url adresi gelecek
    private string backendUrl = "websocketbridge.duckdns.org/voice-message";

    void Start()
    {
        // İlk mikrofonu seç
        if (Microphone.devices.Length > 0)
        {
            microphoneDevice = Microphone.devices[0];
        }
        else
        {
            Debug.LogError("Mikrofon bulunamadı!");
        }
    }

    void Update()
    {
        // V tuşuna basınca kaydı başlat
        if (Input.GetKeyDown(KeyCode.V) && !isRecording)
        {
            StartRecording();
        }

        // V tuşunu bırakınca kaydı durdur ve gönder
        if (Input.GetKeyUp(KeyCode.V) && isRecording)
        {
            StopRecordingAndSend();
        }
    }

    void StartRecording()
    {
        isRecording = true;
        // Maksimum 10 saniyelik, 24000 Hz örnekleme hızıyla kayıt
        recording = Microphone.Start(microphoneDevice, false, 10, 24000);
        Debug.Log("Kayıt Başladı...");
    }

    void StopRecordingAndSend()
    {
        isRecording = false;
        Microphone.End(microphoneDevice);
        Debug.Log("Kayıt Bitti, sunucuya gönderiliyor...");

        // Sesi WAV formatına çevir (SavWav kütüphanesi veya manuel byte dönüştürme gerekir)
        // Kolaylık olması için basit bir byte array gönderimi yapıyoruz:
        byte[] audioBytes = WavUtility.FromAudioClip(recording);

        StartCoroutine(SendAudioToBackend(audioBytes));
    }

    IEnumerator SendAudioToBackend(byte[] audioData)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        // DownloadHandlerAudioClip kullanarak direkt ses dosyasını indiriyoruz
        using (UnityWebRequest www = UnityWebRequest.Post(backendUrl, form))
        {
            DownloadHandlerAudioClip downloadHandler = new DownloadHandlerAudioClip(www.uri, AudioType.WAV);
            www.downloadHandler = downloadHandler;

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Hata: " + www.error);
            }
            else
            {
                // Gelen veriyi ses klibine çevir
                AudioClip receivedClip = downloadHandler.audioClip;

                if (receivedClip != null)
                {
                    Debug.Log("Ses dosyası başarıyla alındı ve oynatılıyor.");
                    
                    // Sesi oynatmak için bir AudioSource bileşeni oluştur veya var olanı kullan
                    AudioSource audioSource = GetComponent<AudioSource>();
                    if (audioSource == null)
                    {
                        audioSource = gameObject.AddComponent<AudioSource>();
                    }

                    audioSource.clip = receivedClip;
                    audioSource.Play();
                }
                else
                {
                    Debug.LogWarning("Ses dosyası alınamadı veya bozuk.");
                }
            }
        }
    }
}