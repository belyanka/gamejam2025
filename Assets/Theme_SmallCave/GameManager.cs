using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public Collider2D floorCollider;
    public CanvasGroup finishPanel;
    public CanvasGroup startPanel;
    public GameObject explosionVfx;

    private List<Draggable2D> itemsList;
    private Collider2D[] allColliders;
    private bool finished = false;
    
    private void Awake()
    {
        // Если экземпляр уже существует — уничтожаем дубликат
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Назначаем текущий экземпляр
        Instance = this;

        // 🔹 Сохраняем объект при смене сцен
        //DontDestroyOnLoad(gameObject);
        
        
        int layerToFind = LayerMask.NameToLayer("Item"); // имя слоя

        Draggable2D[] allObjects = FindObjectsOfType<Draggable2D>();
        itemsList = new List<Draggable2D>();

        foreach (Draggable2D obj in allObjects)
        {
            if (obj.gameObject.layer == layerToFind)
                itemsList.Add(obj);
        }
        
        allColliders =  FindObjectsOfType<Collider2D>();

        Debug.Log($"Найдено {itemsList.Count} объектов на слое {LayerMask.LayerToName(layerToFind)}");
        
        finished = false;
        finishPanel.gameObject.SetActive(false);

        startPanel.gameObject.SetActive(true);
        startPanel.DOFade(1f, 0.4f);
    }

    public void Update()
    {
        if (finished == false)
        {
            finished = true;
            foreach (var item in itemsList)
            {
                finished = item.CheckStable() && finished;
            }

            if (finished)
            {
                ShowFinishScreen();
            }
        }
    }

    public Collider2D GetFloorCollider()
    {
        return floorCollider;
    }

    public Collider2D[] GetAllColliders()
    {
        return allColliders;
    }

    private void ShowFinishScreen()
    {
        finishPanel.gameObject.SetActive(true);
        finishPanel.alpha = 0f;
        finishPanel.DOFade(1f,0.4f);
    }

    public void StartGame()
    {
        startPanel.DOFade(0f, 0.4f).OnComplete(() =>
        {
            startPanel.gameObject.SetActive(false);
        });
    }

    public void RestartScene()
    {
        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.buildIndex);
    }

    public void SpawnExplosion(Vector3 position)
    {
        Instantiate(explosionVfx, position, Quaternion.identity);
    }
}
