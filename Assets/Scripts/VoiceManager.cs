using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Networking;
using AW;
using System.Linq;

public class VoiceManager : MonoBehaviour
{
	public static VoiceManager Instance
	{
		get { return _instance = _instance ?? FindObjectOfType<VoiceManager>() ?? new VoiceManager { }; }
	}
	private static VoiceManager _instance;

	private const string CACHE_LOCATION = "Assets/Resources/Cache/";
	private const string CACHE_FILE_SUFFIX = ".cachedata";
	private const string CACHE_API_KEY_FILE = "apikey.txt";

	private string _apiKey;

	private Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

	private AudioSource _audioSource;

	public void Speak(Element source)
    {
		Interrupt();

		StartCoroutine(SpeakCoroutine(source));
	}

    private void Awake()
    {
		_audioSource = GetComponent<AudioSource>();

		LoadAPIKey();
	}

	private void Interrupt()
    {
		StopAllCoroutines();
		_audioSource.Stop();
	}

	private IEnumerator SpeakCoroutine(Element source)
    {
		//default fallback voice
		var voice = "en-US-Wavenet-A";

		var sourceComponentId = source.components.FirstOrDefault()?.id;
		var component = ArcweaveManager.Instance.Project.components.FirstOrDefault(c => c.id == sourceComponentId);

		if (component?.realName == "Milton")
        {
			voice = "en-GB-Wavenet-B";
		}
		else if (component?.realName == "Stella")
        {
			voice = "en-US-Wavenet-F";
		}
		else if (component?.realName == "Sally")
		{
			voice = "en-GB-Wavenet-C";
		}
		else if (component?.realName == "Rusty")
		{
			voice = "en-GB-Standard-B";
		}

		//todo - look at element
		// decide voice type
		var text = source.content;

		if (!string.IsNullOrEmpty(text))
        {
			

			var data = "{ \"audioConfig\": { \"audioEncoding\": \"LINEAR16\", \"pitch\": 0, \"speakingRate\": 1 }, \"input\": { \"text\": \"" + text + "\" }, \"voice\": { \"languageCode\": \"" + voice.Substring(0, 5) + "\", \"name\": \"" + voice + "\" } }";

			var key = data.ToMD5();

			if (!_clips.ContainsKey(key))
			{
				LoadLocalData(key);
			}

			if (!_clips.ContainsKey(key))
			{
				yield return DownloadClip(data, key);
			}

			if (!_clips.ContainsKey(key))
			{
				Debug.LogWarning("Unable to load audio from local cache or fetch online. Check API key or other errors");
			}

			_audioSource.PlayOneShot(_clips[key]);
		}
	}

	private IEnumerator DownloadClip(string data, string key)
	{
		var url = @"https://texttospeech.googleapis.com/v1/text:synthesize?fields=audioContent&key=" + _apiKey;

        using (var webRequest = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
		{
			webRequest.SetRequestHeader("Content-Type", "application/json");
			webRequest.SetRequestHeader("charset", "utf-8");
			webRequest.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(data));
			webRequest.downloadHandler = new DownloadHandlerBuffer();

			yield return webRequest.SendWebRequest();

			if (webRequest.result != UnityWebRequest.Result.Success)
			{
				Debug.LogWarning(webRequest.error);
			}
			else
			{
				var result = webRequest.downloadHandler.text;
				var response = JsonConvert.DeserializeObject<ResponseData>(result);
				var responseData = response?.audioContent;

				if (responseData != null)
				{
					SaveLocalData(key, responseData);
					_clips[key] = RawDataToClip(responseData);
				}
                else
                {
					Debug.LogWarning("webRequest returned empty result");
				}
			}
		}
	}

	private void LoadAPIKey()
	{
		var path = CACHE_LOCATION + CACHE_API_KEY_FILE;

		if (!System.IO.Directory.Exists(CACHE_LOCATION)) System.IO.Directory.CreateDirectory(CACHE_LOCATION);

		if (System.IO.File.Exists(path))
		{
			var rawData = System.IO.File.ReadAllText(path);
			rawData.Trim();
			_apiKey = rawData;
		}
        else
        {
			Debug.LogWarning("No API key file detected. Please create " + path + " with api key in the file contents.");
        }
	}

	private void LoadLocalData(string key)
	{
		var path = CACHE_LOCATION + key + CACHE_FILE_SUFFIX;

		if (!System.IO.Directory.Exists(CACHE_LOCATION)) System.IO.Directory.CreateDirectory(CACHE_LOCATION);
		if (System.IO.File.Exists(path))
		{
			var rawData = System.IO.File.ReadAllText(path);

			var clip = RawDataToClip(rawData);

			_clips.Add(key, clip);
		}
	}

	private void SaveLocalData(string hash, string data)
	{
		var path = CACHE_LOCATION + hash + CACHE_FILE_SUFFIX;

		if (!System.IO.Directory.Exists(CACHE_LOCATION)) System.IO.Directory.CreateDirectory(CACHE_LOCATION);
		if (!System.IO.File.Exists(path)) System.IO.File.Create(path).Close();

		System.IO.File.WriteAllText(path, data);
	}

	private AudioClip RawDataToClip(string data)
	{
		byte[] converted = Convert.FromBase64String(data);
		return WavHelper.ToAudioClip(converted);
	}
}

public class ResponseData
{
	public string audioContent { get; set; }
}