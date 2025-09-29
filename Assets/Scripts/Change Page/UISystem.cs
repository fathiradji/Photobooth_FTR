using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
//using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Debug = UnityEngine.Debug;

public class UISystem : MonoBehaviour
{
    public static UISystem Instance { get; private set; }

    [Header("Page Management")]
    [SerializeField] private List<CanvasGroup> pages = new List<CanvasGroup>();
    [SerializeField] private int currentPage = 0;
    [SerializeField] private float animationDuration = 1.25f;

    [Header("Events")]
    public UnityEvent onStart;

    //[Header("Script Preview")]
    //public Previewing previewScript;

    private bool isChanging = false;

    private void Start()
    {
        InitializePages();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;
    }

    private void InitializePages()
    {
        for (int i = 0; i < pages.Count; i++)
        {
            pages[i].alpha = (i == 0) ? 1 : 0;
            pages[i].blocksRaycasts = (i == 0) ? true : false;
            pages[i].interactable = (i == 0) ? true : false;
        }

        pages[0].transform.SetAsFirstSibling();
        currentPage = 0;
        isChanging = false;
    }

    // --- Page Navigation Methods ---
    public void NextPage()
    {
        if (!isChanging && currentPage < pages.Count - 1)
        {
            isChanging = true;
            StartCoroutine(ChangePageRoutine(currentPage + 1));
        }
    }

    public void PrevPage()
    {
        if (!isChanging && currentPage > 0)
        {
            //previewScript.availableText.Clear();
            isChanging = true;
            StartCoroutine(ChangePageRoutine(currentPage - 1, isReversing: true));
        }
    }

    public void ResetPage()
    {
        if (!isChanging)
        {
            isChanging = true;
            StartCoroutine(ResetRoutine());
        }
    }

    public void GoToPageOrNext(bool ya)
    {
        if (ya)
        {
            if (!isChanging && currentPage < pages.Count - 1)
            {
                isChanging = true;
                StartCoroutine(ChangePageRoutine(currentPage + 1));
            }
        }
        else
        {
            if (!isChanging && currentPage < pages.Count - 1)
            {
                isChanging = true;
                StartCoroutine(ChangePageRoutine(currentPage + 2));
            }
        }
    }

    private IEnumerator ChangePageRoutine(int targetPage, bool isReversing = false)
    {
        if (isReversing)
        {
            LeanTween.alphaCanvas(pages[currentPage], 0, animationDuration).setEaseInOutSine();
            pages[currentPage].blocksRaycasts = false;
            pages[currentPage].interactable = false;
            //LeanTween.moveLocalX(pages[currentPage].gameObject, offscreenXPosition, animationDuration).setEaseInOutQuart();
            yield return new WaitForSeconds(animationDuration);
            currentPage = targetPage;
        }
        else
        {
            currentPage = targetPage;
            LeanTween.alphaCanvas(pages[currentPage], 1, animationDuration)
                .setOnComplete(() =>
                {
                    pages[currentPage].blocksRaycasts = true;
                    pages[currentPage].interactable = true;
                })
                .setEaseInOutSine();
            Debug.Log(currentPage);
            //LeanTween.moveLocalX(pages[currentPage].gameObject, 0, animationDuration).setEaseInOutQuart();
            yield return new WaitForSeconds(animationDuration);
            pages[currentPage].GetComponent<ThisPageAnimation>()?.StartAnim();
        }
        isChanging = false;
    }

    private IEnumerator ResetRoutine()
    {
        //pages[0].transform.localPosition = new Vector3(-offscreenXPosition, 0, 0);
        pages[0].transform.SetAsLastSibling();
        //LeanTween.moveLocalX(pages[0].gameObject, 0, animationDuration).setEaseInOutQuart();
        LeanTween.alphaCanvas(pages[0], 1, animationDuration).setEaseInOutSine();
        yield return new WaitForSeconds(animationDuration);
        InitializePages();
        yield return new WaitForSeconds(0.5f);
    }
}
