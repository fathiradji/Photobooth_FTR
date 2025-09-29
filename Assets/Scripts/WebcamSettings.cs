using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using RenderHeads.Media.AVProLiveCamera;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System.Collections.Concurrent;
using Debug = UnityEngine.Debug;
using PimDeWitte.UnityMainThreadDispatcher;


public class WebcamSettings : MonoBehaviour
{
    public enum Orientation { Landscape, Portrait }

    [Header("Webcam Settings")]
    [OnValueChanged("OnEnumValueChanged")]
    public Orientation webcamOrientation;
    public RawImage webcamImage;
    public bool useFrame;
    public bool savePicture;
    public bool postPicture;
    public string formatName = "Randomize";

    [Header("UI Elements")]
    public GameObject countdownFrame;
    public Button buttonCapture;
    public CanvasGroup flashEffect;
    public TMP_Text counterText;
    [ShowIf("useFrame")] public GameObject webcamFrame;
    public Previewing previewImage;

    [Header("Capture Settings")]
    public Shader rotateShader;
    public float rotateTo;
    public int countDown = 3;
    public int captureAmount = 4;
    private Material rotateMaterial;

    [Header("Camera Settings")]
    public Camera camCanvas;

    //[Header("RFID Info")]
    //[ReadOnly] public string lastRFID;

    private ConcurrentQueue<System.Action> mainThreadActions = new ConcurrentQueue<System.Action>();
    public HashSet<string> knownFiles = new HashSet<string>();
    public List<Texture2D> newCapturedNoBG;
    public FileSystemWatcher watcher;
    public CanvasGroup loadingScren;
    public GameObject notif;
    public Button[] homeButton;
    public string newPictPath;
    public string outputFolder;
    public string scriptPath;
    public bool isRemoveBGDone;

    private Animator animatorController;
    //private AVProLiveCamera liveCam;
    //[ShowInInspector, ReadOnly] private List<Texture2D> capturedImages = new List<Texture2D>();
    private Coroutine currentCaptureCoroutine;
    private bool isCapturing = false;

    //[Button]
    //public void UpdateWebcamList()
    //{
    //    capturedImages.Clear();
    //    foreach (var device in WebCamTexture.devices)
    //    {
    //        Debug.Log("Webcam ditemukan: " + device.name);
    //    }
    //}

    private void OnEnumValueChanged()
    {
        switch (webcamOrientation)
        {
            case Orientation.Portrait:
                SetWebcamOrientation(90, new Vector2(3840, 2160));
                break;
            case Orientation.Landscape:
                SetWebcamOrientation(0, new Vector2(3840 * 16 / 9, 3840));
                break;
        }
    }

    private void SetWebcamOrientation(float rotation, Vector2 size)
    {
        webcamImage.transform.eulerAngles = new Vector3(0, 0, rotation);
        webcamImage.rectTransform.sizeDelta = size;
    }

    private void Start()
    {
        rotateMaterial = new Material(rotateShader);
        liveCam = GetComponent<AVProLiveCamera>();
        scriptPath = Directory.GetCurrentDirectory() + "\\python\\removebg.py";
        outputFolder = Directory.GetCurrentDirectory() + "\\outputs";

        //watcher = new FileSystemWatcher(outputFolder, "*.png");
        //watcher.Created += OnFileCreated;
        //watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
        //watcher.EnableRaisingEvents = true;

        if (webcamFrame != null)
        {
            animatorController = webcamFrame.GetComponent<Animator>();
            if (animatorController == null)
                Debug.LogWarning("Animator tidak ditemukan pada webcamFrame.");
        }
    }

    private void Update()
    {
        if (webcamImage.texture == null && liveCam != null)
            webcamImage.texture = liveCam.OutputTexture;

        //if (isRemoveBGDone) StartCoroutine(StartNewPict(newPictPath));
    }

    //private void OnFileCreated(object sender, FileSystemEventArgs e)
    //{
    //    newPictPath = e.FullPath;
    //    Debug.Log("Path full: " + newPictPath);

    //    isRemoveBGDone = true;
    //}
    IEnumerator StartNewPict(string path)
    {
        path = path.Trim();

        if (!File.Exists(path))
        {
            Debug.LogError($"File not found at {path}");
            yield break;
        }

        byte[] bytes = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        newCapturedNoBG.Add(tex);

        Debug.Log($"Loaded texture: {path}, size: {tex.width}x{tex.height}");
    }


    private Texture2D LoadImageToTexture2D(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogWarning("File tidak ditemukan: " + filePath);
            return null;
        }

        try
        {
            byte[] imageData = File.ReadAllBytes(filePath);

            Texture2D texture = new Texture2D(2, 2); // Ukuran awal bisa apa saja, akan di-replace
            if (texture.LoadImage(imageData))
            {
                Debug.Log("Berhasil memuat gambar: " + filePath);
                return texture;
            }
            else
            {
                Debug.LogWarning("Gagal memuat gambar: " + filePath);
                return null;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError("Error saat membaca file: " + ex.Message);
            return null;
        }
    }

    private IEnumerator DoCountdown(int seconds)
    {
        LeanTween.scale(countdownFrame, Vector3.one, 0f);
        for (int i = seconds; i > 0; i--)
        {
            counterText.text = i.ToString();
            yield return new WaitForSeconds(1f);
        }
        LeanTween.scale(countdownFrame, Vector3.zero, 0f)
            .setOnComplete(() => counterText.text = string.Empty);
    }

    private void CaptureOnce(List<Texture2D> captureList)
    {
        Texture2D tex = CaptureRenderTexture(camCanvas);
        string name = GetName();
        if (savePicture) SaveCapturedImage(tex, name);
        FlashAndStore(captureList, tex, name);
    }

    private void FlashAndStore(List<Texture2D> list, Texture2D tex, string name)
{
    list.Add(tex);  // langsung tambahkan sebelum animasi flash

    var seq = LeanTween.sequence();
    seq.append(LeanTween.alphaCanvas(flashEffect, 1, 0.1f));
    seq.append(0.2f);
    seq.append(LeanTween.alphaCanvas(flashEffect, 0, 0.5f));
}


    public void Capture()
    {
        if (isCapturing)
        {
            Debug.LogWarning("Capture already in progress!");
            return;
        }
        if (currentCaptureCoroutine != null)
        {
            StopCoroutine(currentCaptureCoroutine);
        }
        currentCaptureCoroutine = StartCoroutine(CaptureRoutine());
    }

    private IEnumerator CaptureRoutine()
    {
        isCapturing = true;
        buttonCapture.interactable = false;
        LeanTween.scale(buttonCapture.gameObject, Vector3.zero, 0.25f).setEaseInOutSine();

        newCapturedNoBG.Clear();
        List<Texture2D> newCapture = new List<Texture2D>();

        for (int i = 0; i < homeButton.Length; i++)
        {
            homeButton[i].interactable = false;
        }
        LeanTween.scale(notif, Vector2.one, 0.5f).setEaseInOutSine();

        yield return new WaitForSeconds(3f);
        LeanTween.scale(notif, Vector2.zero, 0.5f).setEaseInOutSine();

        yield return new WaitForSeconds(1f);
        try
        {
            animatorController.enabled = true;
            animatorController.Play("pose4k");
            yield return StartCoroutine(DoCountdown(5));
            yield return new WaitForSeconds(0.1f);
            CaptureOnce(newCapture); Debug.Log("next 1");

            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(DoCountdown(3));
            yield return new WaitForSeconds(0.1f);
            CaptureOnce(newCapture); Debug.Log("next 2");

            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(DoCountdown(3));
            yield return new WaitForSeconds(0.1f);
            CaptureOnce(newCapture); Debug.Log("next 3");

            yield return new WaitForSeconds(0.5f);

            yield return StartCoroutine(DoCountdown(3));
            yield return new WaitForSeconds(0.1f);
            CaptureOnce(newCapture); Debug.Log("next 4");

            yield return new WaitForSeconds(0.25f);
            Debug.Log("next 5");
            UISystem.Instance.NextPage();

            yield return new WaitUntil(() =>
            {
                return newCapturedNoBG.Count > 3;
            });
            previewImage.UpdateTexture(newCapturedNoBG);
            previewImage.StartPreview();

            yield return new WaitForSeconds(0.5f);
            Debug.Log($"Captured Images: {newCapturedNoBG.Count}");

            //yield return StartCoroutine(APIConnection.Instance.UploadImages(newCapturedNoBG, lastRFID, (uploadResponse) =>
            //{
            //    if (uploadResponse != null && uploadResponse.success)
            //    {
            //        EnableHome();
            //        Debug.Log("[WebcamSettings] Upload berhasil: " + uploadResponse.message);
            //    }
            //    else
            //    {
            //        Debug.LogError("[WebcamSettings] Upload gagal: " + uploadResponse?.message);
            //    }
            //}));

            yield return new WaitForSeconds(1f);
            UISystem.Instance.NextPage();
        }
        finally
        {
            animatorController.enabled = false;
            isCapturing = false;
            currentCaptureCoroutine = null;

            LeanTween.scale(buttonCapture.gameObject, Vector3.one, 1.5f)
                .setOnComplete(() =>
                {
                    EnableHome();
                    LeanTween.scale(countdownFrame, Vector3.zero, 0f);
                    buttonCapture.interactable = true;
                })
                .setEaseInOutSine();
        }
    }

    public void EnableHome()
    {
        for (int i = 0; i < homeButton.Length; i++)
        {
            homeButton[i].interactable = true;
        }
    }

    private Texture2D CaptureRenderTexture(Camera camera)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture targetRT = camera.targetTexture;

        if (targetRT == null)
        {
            Debug.LogError("Camera does not have a target RenderTexture!");
            return null;
        }

        camera.Render();
        RenderTexture.active = targetRT;

        Texture2D texture = new Texture2D(targetRT.width, targetRT.height, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
        texture.Apply(false);

        RenderTexture.active = currentRT;
        return texture;
    }

    private void SaveCapturedImage(Texture2D texture, string name)
    {
        string path = $"{Directory.GetCurrentDirectory()}\\Captured";
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        //Texture2D correctedTexture = RotateTextureGPU(texture, rotateTo);
        string fileName = $"{path}\\{name}";
        
        File.WriteAllBytes(fileName, texture.EncodeToPNG());
        //RemoveBackground(fileName);

        Debug.Log($"Saved to {fileName}");
    }

    //public void RemoveBackground(string inputImagePath)
    //{
    //    Thread thread = new Thread(() =>
    //    {
    //        try
    //        {
    //            ProcessStartInfo start = new ProcessStartInfo
    //            {
    //                FileName = @"C:\Users\PhotoboothTGAA\AppData\Local\Programs\Python\Python313\python.exe",
    //                Arguments = $"\"{scriptPath}\" \"{inputImagePath}\"",
    //                UseShellExecute = false,
    //                RedirectStandardOutput = true,
    //                RedirectStandardError = true,
    //                CreateNoWindow = true
    //            };

    //            using (Process process = Process.Start(start))
    //            {
    //                string result = process.StandardOutput.ReadToEnd();
    //                string error = process.StandardError.ReadToEnd();
    //                process.WaitForExit();

    //                UnityEngine.Debug.Log($"[PythonBridge] Output: {result}");
    //                UnityEngine.Debug.Log($"{result}");

    //                Debug.Log($"[PythonBridge] Cleaned result: {result.Trim()}");
    //                Debug.Log($"Exists after WaitForExit: {File.Exists(result.Trim())}");

    //                UnityMainThreadDispatcher.Instance().Enqueue(StartNewPict(result));


    //                if (!string.IsNullOrEmpty(error))
    //                    UnityEngine.Debug.LogError($"[PythonBridge] Error: {error}");
    //            }
    //        }
    //        catch (System.Exception ex)
    //        {
    //            UnityEngine.Debug.LogError("[PythonBridge] Exception: " + ex.Message);
    //        }
    //    });

    //    thread.Start();
    //}

    public Texture2D RotateTextureGPU(Texture2D original, float angle)
    {
        if (Mathf.Abs(angle) < 1f) // Tidak perlu rotasi
            return original;

        Texture2D rotated = new Texture2D(original.height, original.width);
        Color32[] pixels = original.GetPixels32();

        for (int x = 0; x < original.width; x++)
        {
            for (int y = 0; y < original.height; y++)
            {
                rotated.SetPixel(y, original.width - x - 1, pixels[x + y * original.width]);
            }
        }
        rotated.Apply();
        return rotated;
    }

    private string GetName()
    {
        string timestamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"{formatName}_{timestamp}.png";
    }

    //void OnDisable()
    //{
    //    if (watcher != null)
    //    {
    //        watcher.EnableRaisingEvents = false;
    //        watcher.Dispose();
    //    }
    //}

    //void Dispose()
    //{
    //    watcher.Created -= OnFileCreated;
    //    this.watcher.Dispose();
    //}
}